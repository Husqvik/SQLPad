using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;
using SqlPad.Oracle.DataDictionary;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.ModelDataProviders;
using SqlPad.Oracle.ToolTips;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		private const string ModuleNameSqlPadDatabaseModelBase = "DbModel";
		private const string OracleDataAccessRegistryPath = @"Software\Oracle\ODP.NET";
		private const string OracleDataAccessComponents = "Oracle Data Access Components";

		internal const int InitialLongFetchSize = 131072;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		private static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();
		private static readonly Dictionary<string, DatabaseProperty> DatabaseProperties = new Dictionary<string, DatabaseProperty>();
		private static readonly HashSet<string> ActiveDataModelRefresh = new HashSet<string>();
		private static readonly Dictionary<string, List<RefreshModel>> WaitingDataModelRefresh = new Dictionary<string, List<RefreshModel>>();

		private readonly string _connectionStringName;
		private readonly string _moduleName;
		private readonly string _initialSchema;
		private readonly ConnectionStringSettings _connectionString;
		private readonly OracleDataDictionaryMapper _dataDictionaryMapper;

		private readonly Timer _refreshTimer = new Timer();
		private readonly HashSet<OracleConnectionAdapter> _connectionAdapters = new HashSet<OracleConnectionAdapter>();
		private readonly CancellationTokenSource _backgroundTaskCancellationTokenSource = new CancellationTokenSource();

		private bool _isInitialized;
		private bool _isDisposed;
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private string _currentSchema;
		private Task _backgroundTask;

		private OracleDataDictionary _dataDictionary = OracleDataDictionary.EmptyDictionary;
		private HashSet<string> _schemas = new HashSet<string>();
		private IReadOnlyDictionary<string, OracleSchema> _allSchemas = new Dictionary<string, OracleSchema>();
		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> _allProgramMetadata = Enumerable.Empty<OracleProgramMetadata>().ToLookup(m => m.Identifier);
		private ILookup<OracleObjectIdentifier, OracleReferenceConstraint> _uniqueConstraintReferringReferenceConstraints = Enumerable.Empty<OracleReferenceConstraint>().ToLookup(m => m.FullyQualifiedName);

		//private readonly OracleCustomTypeGenerator _customTypeGenerator;

		public string ConnectionIdentifier { get; }

		internal string BackgroundConnectionString => OracleConnectionStringRepository.GetBackgroundConnectionString(_connectionString.ConnectionString);

		private OracleDatabaseModel(ConnectionStringSettings connectionString, string identifier)
		{
			ObjectScriptExtractor = new OracleObjectScriptExtractor(this);

			_connectionString = connectionString;
			var connectionStringBuilder = new OracleConnectionStringBuilder(_connectionString.ConnectionString) { SelfTuning = false, MinPoolSize = 1, IncrPoolSize = 1 };
			if (String.IsNullOrWhiteSpace(connectionStringBuilder.UserID))
			{
				throw new ArgumentException("Connection string must contain USER ID");
			}

			ConnectionIdentifier = identifier;
			_moduleName = $"{ModuleNameSqlPadDatabaseModelBase}/{identifier}";
			_initialSchema = _currentSchema = connectionStringBuilder.UserID.ToQuotedIdentifier().Trim('"');
			_connectionStringName = $"{connectionStringBuilder.DataSource}_{_currentSchema}";

			HasDbaPrivilege = String.Equals(connectionStringBuilder.DBAPrivilege.ToUpperInvariant(), "SYSDBA");

			lock (ActiveDataModelRefresh)
			{
				if (!WaitingDataModelRefresh.ContainsKey(_connectionStringName))
				{
					WaitingDataModelRefresh[_connectionStringName] = new List<RefreshModel>();
				}
			}

			_dataDictionaryMapper = new OracleDataDictionaryMapper(this, RaiseRefreshStatusChanged);

			InternalRefreshStarted += InternalRefreshStartedHandler;
			InternalRefreshCompleted += InternalRefreshCompletedHandler;

			//_customTypeGenerator = OracleCustomTypeGenerator.GetCustomTypeGenerator(connectionString.Name);
		}

		private static event EventHandler InternalRefreshStarted;
		private static event EventHandler InternalRefreshCompleted;

		private void InternalRefreshStartedHandler(object sender, EventArgs eventArgs)
		{
			var refreshedModel = GetDatabaseModelIfDifferentCompatibleInstance(sender);
			if (refreshedModel == null)
			{
				return;
			}

			_isRefreshing = true;
			RaiseEvent(RefreshStarted);
			RaiseRefreshStatusWaitingForModelBeingRefreshed();
		}

		private void InternalRefreshCompletedHandler(object sender, EventArgs eventArgs)
		{
			var refreshedModel = GetDatabaseModelIfDifferentCompatibleInstance(sender);
			if (refreshedModel == null)
			{
				return;
			}

			_dataDictionary = refreshedModel._dataDictionary;
			_allProgramMetadata = refreshedModel._allProgramMetadata;
			_uniqueConstraintReferringReferenceConstraints = refreshedModel._uniqueConstraintReferringReferenceConstraints;
			_allSchemas = refreshedModel._allSchemas;
			_schemas = refreshedModel._schemas;

			Trace.WriteLine($"{DateTime.Now} - Metadata for '{_connectionStringName}/{ConnectionIdentifier}' has been retrieved from the cache. ");

			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(_connectionStringName, out refreshModels))
				{
					var modelIndex = refreshModels.FindIndex(m => m.DatabaseModel == this);
					if (modelIndex != -1)
					{
						refreshModels[modelIndex].TaskCompletionSource.SetResult(_dataDictionary);
						refreshModels.RemoveAt(modelIndex);
					}
				}
			}

			_isRefreshing = false;
			RaiseEvent(RefreshCompleted);
		}

		private OracleDatabaseModel GetDatabaseModelIfDifferentCompatibleInstance(object sender)
		{
			var refreshedModel = (OracleDatabaseModel)sender;
			return sender == this || !String.Equals(ConnectionString.ConnectionString, refreshedModel.ConnectionString.ConnectionString)
				? null
				: refreshedModel;
		}

		private void RefreshTimerElapsedHandler(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			SetRefreshTimerInterval();
			RefreshIfNeeded();
		}

		private void SetRefreshTimerInterval()
		{
			_refreshTimer.Interval = ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod * 60000;
			Trace.WriteLine($"{DateTime.Now} - Data model '{_connectionStringName}/{ConnectionIdentifier}' refresh timer set: {ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod} minute(s). ");
		}

		public static void ValidateConfiguration()
		{
			string odacVersion = null;
			var odacRegistryKey = Registry.LocalMachine.OpenSubKey(OracleDataAccessRegistryPath);
			if (odacRegistryKey != null)
			{
				odacVersion = odacRegistryKey.GetSubKeyNames().OrderByDescending(n => n).FirstOrDefault();
			}

			var traceMessage = odacVersion == null
				? $"{OracleDataAccessComponents} registry entry was not found. "
				: $"{OracleDataAccessComponents} version {odacVersion} found. ";

			Trace.WriteLine($"{DateTime.Now} - {traceMessage} {OracleDataAccessComponents} assembly version {typeof (OracleConnection).Assembly.FullName}");
		}

		public static OracleDatabaseModel GetDatabaseModel(ConnectionStringSettings connectionString, string identifier = null)
		{
			OracleDatabaseModel databaseModel;
			lock (DatabaseModels)
			{
				if (DatabaseModels.TryGetValue(connectionString.ConnectionString, out databaseModel))
				{
					databaseModel = databaseModel.Clone(identifier);
				}
				else
				{
					DatabaseModels[connectionString.ConnectionString] = databaseModel = new OracleDatabaseModel(connectionString, identifier);
				}
			}

			return databaseModel;
		}

		private void ExecuteActionAsync(Action action)
		{
			if (!_isRefreshing)
			{
				_backgroundTask = Task.Run(action);
			}
		}

		public override ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllProgramMetadata => _allProgramMetadata;

		public override ILookup<OracleObjectIdentifier, OracleReferenceConstraint> UniqueConstraintReferringReferenceConstraints => _uniqueConstraintReferringReferenceConstraints;

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata => _dataDictionary.NonSchemaFunctionMetadata;

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageProgramMetadata => _dataDictionary.BuiltInPackageProgramMetadata;

		public override ConnectionStringSettings ConnectionString => _connectionString;

		public override IOracleObjectScriptExtractor ObjectScriptExtractor { get; }

		public override bool IsInitialized => _isInitialized;

		public override bool IsMetadataAvailable => _dataDictionary != OracleDataDictionary.EmptyDictionary;

		public override bool HasDbaPrivilege { get; }

		public override string CurrentSchema
		{
			get
			{
				return _schemas.Contains(_currentSchema)
					? _currentSchema
					: _initialSchema;
			}
			set
			{
				_currentSchema = value;

				foreach (var connectionAdapter in _connectionAdapters)
				{
					connectionAdapter.SwitchCurrentSchema();
				}
			}
		}

		public override string DatabaseDomainName => DatabaseProperties[_connectionString.ConnectionString].DomainName;

		public override ICollection<string> Schemas => _schemas;

		public override IReadOnlyDictionary<string, OracleSchema> AllSchemas => _allSchemas;

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects => _dataDictionary.AllObjects;

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks => _dataDictionary.DatabaseLinks;

		public override IReadOnlyCollection<string> CharacterSets => _dataDictionary.CharacterSets;

		public override IDictionary<int, string> StatisticsKeys => _dataDictionary.StatisticsKeys;

		public override IDictionary<string, string> SystemParameters => _dataDictionary.SystemParameters;

		public override Version Version => DatabaseProperties[_connectionString.ConnectionString].Version;

		public override void RefreshIfNeeded()
		{
			if (IsRefreshNeeded)
			{
				Refresh();
			}
		}

		public override bool IsFresh => !IsRefreshNeeded;

		private bool IsRefreshNeeded => DataDictionaryValidityTimestamp < DateTime.Now;

		private DateTime DataDictionaryValidityTimestamp => _dataDictionary.Timestamp.AddMinutes(ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod);

		public override Task<ILookup<string, string>> GetContextData(CancellationToken cancellationToken)
		{
			return _dataDictionaryMapper.GetContextData(cancellationToken);
		}

		public override Task<IReadOnlyList<string>> GetWeekdayNames(CancellationToken cancellationToken)
		{
			return _dataDictionaryMapper.GetWeekdayNames(cancellationToken);
		}

		public override Task Refresh(bool force = false)
		{
			lock (ActiveDataModelRefresh)
			{
				if (!ActiveDataModelRefresh.Add(_connectionStringName))
				{
					var taskCompletionSource = new TaskCompletionSource<OracleDataDictionary>();
					WaitingDataModelRefresh[_connectionStringName].Add(new RefreshModel { DatabaseModel = this, TaskCompletionSource = taskCompletionSource });

					Trace.WriteLine($"{DateTime.Now} - Cache for '{_connectionStringName}' is being loaded by other requester. Waiting until operation finishes. ");

					RaiseEvent(RefreshStarted);
					RaiseRefreshStatusWaitingForModelBeingRefreshed();
					return taskCompletionSource.Task;
				}
			}

			ExecuteActionAsync(() => LoadSchemaObjectMetadata(force));

			return _backgroundTask;
		}

		internal void SetCurrentSchema(string schemaName)
		{
			if (CurrentSchema == schemaName)
			{
				return;
			}

			if (_schemas.Add(schemaName))
			{
				RefreshSchemas();
			}

			CurrentSchema = schemaName;
			RaiseEvent(CurrentSchemaChanged);
		}

		private void RaiseRefreshStatusWaitingForModelBeingRefreshed()
		{
			RaiseRefreshStatusChanged("Waiting for data dictionary metadata... ");
		}

		public override IConnectionAdapter CreateConnectionAdapter()
		{
			var connectionAdapter = new OracleConnectionAdapter(this);
			_connectionAdapters.Add(connectionAdapter);
			return connectionAdapter;
		}

		internal void RemoveConnectionAdapter(OracleConnectionAdapter connectionAdapter)
		{
			_connectionAdapters.Remove(connectionAdapter);
		}

		public override async Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var partitionDataProvider = new PartitionDataProvider(dataModel, Version);
			var spaceAllocationDataProvider = new TableSpaceAllocationDataProvider(dataModel, dataModel.Owner, dataModel.Name);
			await UpdateModelAsync(true, cancellationToken, partitionDataProvider.PartitionDetailDataProvider, partitionDataProvider.SubPartitionDetailDataProvider, spaceAllocationDataProvider);
		}

		public override async Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var partitionDataProvider = new PartitionDataProvider(dataModel, Version);
			var spaceAllocationDataProvider = new TableSpaceAllocationDataProvider(dataModel, dataModel.Owner, dataModel.Name);
			await UpdateModelAsync(true, cancellationToken, partitionDataProvider.SubPartitionDetailDataProvider, spaceAllocationDataProvider);
		}

		public override async Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var tableDetailDataProvider = new TableDetailDataProvider(dataModel, objectIdentifier);
			var spaceAllocationDataProvider = new TableSpaceAllocationDataProvider(dataModel, objectIdentifier, String.Empty);
			var tableCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, null);
			var tableInMemorySpaceAllocationDataProvider = new TableInMemorySpaceAllocationDataProvider(dataModel, objectIdentifier, Version);
			var indexDetailDataProvider = new IndexDetailDataProvider(dataModel, objectIdentifier, null);
			var indexColumnDataProvider = new IndexColumnDataProvider(dataModel, objectIdentifier, null);
			var partitionDataProvider = new PartitionDataProvider(dataModel, objectIdentifier, Version);
			var tablespaceDetailDataProvider = new TablespaceDetailDataProvider(dataModel.TablespaceDataModel);
			var datafileDataProvider = new TablespaceFilesDataProvider(dataModel.TablespaceDataModel);
			await UpdateModelAsync(true, cancellationToken, tableDetailDataProvider, tableCommentDataProvider, spaceAllocationDataProvider, tableInMemorySpaceAllocationDataProvider, indexDetailDataProvider, indexColumnDataProvider, partitionDataProvider.PartitionDetailDataProvider, partitionDataProvider.SubPartitionDetailDataProvider, tablespaceDetailDataProvider, datafileDataProvider);
		}

		public override async Task UpdateViewDetailsAsync(OracleObjectIdentifier objectIdentifier, ObjectDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var viewCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, null);
			var columnConstraintDataProvider = new ConstraintDataProvider(dataModel, objectIdentifier, null);
			await UpdateModelAsync(true, cancellationToken, viewCommentDataProvider, columnConstraintDataProvider);
		}

		public override async Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var columnDetailDataProvider = new ColumnDetailDataProvider(dataModel, objectIdentifier, columnName);
			var columnCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, columnName);
			var columnConstraintDataProvider = new ConstraintDataProvider(dataModel, objectIdentifier, columnName);
			var columnIndexesDataProvider = new IndexDetailDataProvider(dataModel, objectIdentifier, columnName);
			var indexColumnDataProvider = new IndexColumnDataProvider(dataModel, objectIdentifier, columnName);
			var detailHistogramDataProvider = new ColumnDetailHistogramDataProvider(dataModel, objectIdentifier, columnName);
			var columnInMemoryDetailsDataProvider = new ColumnDetailInMemoryDataProvider(dataModel, objectIdentifier, columnName, Version);
			await UpdateModelAsync(true, cancellationToken, columnDetailDataProvider, columnCommentDataProvider, columnConstraintDataProvider, columnIndexesDataProvider, indexColumnDataProvider, detailHistogramDataProvider, columnInMemoryDetailsDataProvider);
		}

		public override async Task UpdateUserDetailsAsync(OracleSchemaModel dataModel, CancellationToken cancellationToken)
		{
			var userDetailDataProvider = new UserDataProvider(dataModel);
			var defaultTablespaceDetailDataProvider = new TablespaceDetailDataProvider(dataModel.DefaultTablespaceModel);
			var defaultDatafileDataProvider = new TablespaceFilesDataProvider(dataModel.DefaultTablespaceModel);
			var temporaryTablespaceDetailDataProvider = new TablespaceDetailDataProvider(dataModel.TemporaryTablespaceModel);
			var temporaryDatafileDataProvider = new TablespaceFilesDataProvider(dataModel.TemporaryTablespaceModel);
			var profileDataProvider = new ProfileDetailsDataProvider(dataModel.ProfileModel);
			await UpdateModelAsync(true, cancellationToken, userDetailDataProvider, defaultTablespaceDetailDataProvider, temporaryTablespaceDetailDataProvider, defaultDatafileDataProvider, temporaryDatafileDataProvider, profileDataProvider);
		}

		public override async Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken)
		{
			var remoteTableColumnDataProvider = new RemoteTableColumnDataProvider(databaseLink, schemaObject);
			await UpdateModelAsync(false, cancellationToken, remoteTableColumnDataProvider);
			return remoteTableColumnDataProvider.Columns;
		}

		internal Task UpdateModelAsync(bool suppressException, CancellationToken cancellationToken, params IModelDataProvider[] updaters)
		{
			return UpdateModelAsync(BackgroundConnectionString, _currentSchema, suppressException, cancellationToken, updaters);
		}

		internal static async Task UpdateModelAsync(string connectionString, string currentSchema, bool suppressException, CancellationToken cancellationToken, params IModelDataProvider[] updaters)
		{
			using (var connection = new OracleConnection { ConnectionString = connectionString })
			{
				await UpdateModelAsync(connection, currentSchema, suppressException, cancellationToken, updaters);
				await connection.CloseAsynchronous(cancellationToken);
			}
		}

		internal static async Task UpdateModelAsync(OracleConnection connection, string currentSchema, bool suppressException, CancellationToken cancellationToken, params IModelDataProvider[] updaters)
		{
			using (var command = connection.CreateCommand())
			{
				command.BindByName = true;

				OracleTransaction transaction = null;

				try
				{
					foreach (var updater in updaters)
					{
						command.ResetParametersToAvoidOdacBug();
						command.CommandText = String.Empty;
						command.CommandType = CommandType.Text;
						updater.InitializeCommand(command);

						try
						{
							if (updater.IsValid)
							{
								if (connection.State == ConnectionState.Closed)
								{
									await connection.OpenAsynchronous(cancellationToken);
									connection.ModuleName = "SQLPad backround";
									connection.ActionName = "Model data provider";

									if (!String.IsNullOrEmpty(currentSchema))
									{
										using (var setSchemaCommand = connection.CreateCommand())
										{
											await setSchemaCommand.SetCurrentSchema(currentSchema, cancellationToken);
										}
									}

									transaction = connection.BeginTransaction();
								}

								if (updater.HasScalarResult)
								{
									var result = await command.ExecuteScalarAsynchronous(cancellationToken);
									updater.MapScalarValue(result);
								}
								else
								{
									using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
									{
										await updater.MapReaderData(reader, cancellationToken);
									}
								}
							}
						}
						catch (OracleException exception)
						{
							if (exception.Number == (int)OracleErrorCode.UserInvokedCancellation)
							{
								break;
							}

							throw;
						}
					}
				}
				catch (Exception e)
				{
					Trace.WriteLine($"{DateTime.Now} - Update model failed: {e}");

					if (!suppressException)
					{
						throw;
					}
				}
				finally
				{
					if (transaction != null)
					{
						await transaction.RollbackAsynchronous();
						transaction.Dispose();
					}
				}
			}
		}

		public override event EventHandler Initialized;

		public override event EventHandler CurrentSchemaChanged;

		public override event EventHandler<DatabaseModelPasswordArgs> PasswordRequired;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;
		
		public override event EventHandler RefreshStarted;

		public override event EventHandler<DatabaseModelRefreshStatusChangedArgs> RefreshStatusChanged;

		public override event EventHandler RefreshCompleted;

		public override void Dispose()
		{
			_isDisposed = true;

			InternalRefreshStarted -= InternalRefreshStartedHandler;
			InternalRefreshCompleted -= InternalRefreshCompletedHandler;

			_refreshTimer.Stop();
			_refreshTimer.Dispose();

			if (_backgroundTask != null)
			{
				if (_isRefreshing)
				{
					_isRefreshing = false;
					RaiseEvent(RefreshCompleted);
				}

				if (_backgroundTask.Status == TaskStatus.Running)
				{
					_backgroundTaskCancellationTokenSource.Cancel();
				}
				else
				{
					_backgroundTask.Dispose();
				}
			}

			Initialized = null;
			PasswordRequired = null;
			Disconnected = null;
			InitializationFailed = null;
			RefreshStarted = null;
			RefreshStatusChanged = null;
			RefreshCompleted = null;
			CurrentSchemaChanged = null;

			foreach (var adapter in _connectionAdapters.ToArray())
			{
				adapter.Dispose();
			}

			lock (DatabaseModels)
			{
				if (DatabaseModels.ContainsValue(this))
				{
					DatabaseModels.Remove(_connectionString.ConnectionString);
				}
			}

			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(_connectionStringName, out refreshModels))
				{
					refreshModels.RemoveAll(m => m.DatabaseModel == this);
				}
			}
		}

		private OracleDatabaseModel Clone(string modulePrefix)
		{
			var clone =
				new OracleDatabaseModel(ConnectionString, modulePrefix)
				{
					_currentSchema = _currentSchema,
					_dataDictionary = _dataDictionary,
					_allProgramMetadata = _allProgramMetadata,
					_uniqueConstraintReferringReferenceConstraints = _uniqueConstraintReferringReferenceConstraints
				};

			return clone;
		}

		internal void Disconnect(OracleException exception)
		{
			OracleSchemaResolver.Unregister(this);

			if (!_isInitialized)
			{
				return;
			}

			_isInitialized = false;

			Disconnected?.Invoke(this, new DatabaseModelConnectionErrorArgs(exception));
		}

		private void EnsureDatabaseProperties()
		{
			DatabaseProperty property;
			if (DatabaseProperties.TryGetValue(_connectionString.ConnectionString, out property))
			{
				return;
			}

			using (var connection = new OracleConnection(BackgroundConnectionString))
			{
				connection.Open();

				var versionString = connection.ServerVersion.Remove(connection.ServerVersion.LastIndexOf('.'));

				DatabaseProperties[_connectionString.ConnectionString] =
					new DatabaseProperty
					{
						DomainName = String.Equals(connection.DatabaseDomainName, "null") ? null : connection.DatabaseDomainName,
						Version = Version.Parse(versionString)
					};
			}
		}

		internal IEnumerable<T> ExecuteReader<T>(Func<string> getCommandTextFunction, Func<OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(BackgroundConnectionString))
			{
				connection.Open();

				using (var command = connection.CreateCommand())
				{
					command.CommandText = getCommandTextFunction();
					command.BindByName = true;
					command.InitialLONGFetchSize = OracleDataDictionaryMapper.LongFetchSize;

					connection.ModuleName = $"{_moduleName}/MetadataMapper";
					connection.ActionName = "Fetch data dictionary metadata";

					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							_backgroundTaskCancellationTokenSource.Token.ThrowIfCancellationRequested();

							yield return formatFunction(reader);
						}
					}
				}
			}
		}

		private void RemoveActiveRefreshTask()
		{
			lock (ActiveDataModelRefresh)
			{
				RaiseEvent(InternalRefreshCompleted);
				ActiveDataModelRefresh.Remove(_connectionStringName);
			}
		}

		private void LoadSchemaObjectMetadata(bool force)
		{
			TryLoadSchemaObjectMetadataFromCache();

			var isRefreshDone = !IsRefreshNeeded && !force;
			if (isRefreshDone)
			{
				Trace.WriteLine($"{DateTime.Now} - Cache for '{_connectionStringName}' is valid until {DataDictionaryValidityTimestamp}. ");
				RemoveActiveRefreshTask();
				return;
			}

			RaiseEvent(InternalRefreshCompleted);

			var reason = force ? "has been forced to refresh" : (_dataDictionary.Timestamp > DateTime.MinValue ? "has expired" : "does not exist or is corrupted");
			Trace.WriteLine($"{DateTime.Now} - Cache for '{_connectionStringName}' {reason}. Cache refresh started. ");

			_isRefreshing = true;
			RaiseEvent(InternalRefreshStarted);
			RaiseEvent(RefreshStarted);

			var isRefreshSuccessful = RefreshSchemaObjectMetadata();

			RemoveActiveRefreshTask();

			if (isRefreshSuccessful)
			{
				try
				{
					MetadataCache.StoreDatabaseModelCache(_connectionStringName, stream => _dataDictionary.Serialize(stream));
				}
				catch (Exception e)
				{
					Trace.WriteLine($"{DateTime.Now} - Storing metadata cache failed: {e}");
				}
			}

			_isRefreshing = false;
			RaiseEvent(RefreshCompleted);
		}

		private bool RefreshSchemaObjectMetadata()
		{
			var lastRefresh = DateTime.Now;

			try
			{
				RefreshSchemas();

				var allObjects = _dataDictionaryMapper.BuildDataDictionary();

				var userPrograms = _dataDictionaryMapper.GetUserFunctionMetadata().SelectMany(g => g).ToArray();
				var builtInPrograms = _dataDictionaryMapper.GetBuiltInFunctionMetadata().SelectMany(g => g).ToArray();
				_allProgramMetadata = builtInPrograms
					.Concat(userPrograms)
					.ToLookup(m => m.Identifier);

				var stopwatch = Stopwatch.StartNew();

				var nonSchemaBuiltInFunctionMetadata = new List<OracleProgramMetadata>();

				foreach (var programMetadata in builtInPrograms.Concat(userPrograms))
				{
					if (String.IsNullOrEmpty(programMetadata.Identifier.Owner))
					{
						nonSchemaBuiltInFunctionMetadata.Add(programMetadata);
						continue;
					}

					OracleSchemaObject schemaObject;
					var packageIdentifier = OracleObjectIdentifier.Create(programMetadata.Identifier.Owner, programMetadata.Identifier.Package);
					if (allObjects.TryGetFirstValue(out schemaObject, packageIdentifier))
					{
						((OraclePackage)schemaObject).Programs.Add(programMetadata);
						programMetadata.Owner = schemaObject;
					}
					else
					{
						var programIdentifier = OracleObjectIdentifier.Create(programMetadata.Identifier.Owner, programMetadata.Identifier.Name);
						if (allObjects.TryGetFirstValue(out schemaObject, programIdentifier))
						{
							((OracleSchemaProgram)schemaObject).Metadata = programMetadata;
							programMetadata.Owner = schemaObject;
						}
					}
				}

				OracleDataDictionaryMapper.WriteTrace(ConnectionString.Name, $"Function and procedure metadata schema object mapping finished in {stopwatch.Elapsed}. ");

				stopwatch.Reset();

				_uniqueConstraintReferringReferenceConstraints = BuildUniqueConstraintReferringReferenceConstraintLookup(allObjects.Values);

				var databaseLinks = _dataDictionaryMapper.GetDatabaseLinks();
				var characterSets = _dataDictionaryMapper.GetCharacterSets();
				var statisticsKeys = SafeFetchDictionary(_dataDictionaryMapper.GetStatisticsKeys, "OracleDataDictionaryMapper.GetStatisticsKeys failed: ");
				var systemParameters = SafeFetchDictionary(_dataDictionaryMapper.GetSystemParameters, "OracleDataDictionaryMapper.GetSystemParameters failed: ");

				OracleDataDictionaryMapper.WriteTrace(ConnectionString.Name, $"Unique constraint, database link, character sets, statistics keys and system parameter mapping finished in {stopwatch.Elapsed}. ");

				_dataDictionary = new OracleDataDictionary(allObjects, databaseLinks, nonSchemaBuiltInFunctionMetadata, characterSets, statisticsKeys, systemParameters, lastRefresh);

				OracleDataDictionaryMapper.WriteTrace(ConnectionString.Name, "Data dictionary metadata cache has been initialized successfully. ");

				//_customTypeGenerator.GenerateCustomTypeAssembly(_dataDictionary);

				CachedDataDictionaries[_connectionStringName] = _dataDictionary;

				return true;
			}
			catch(Exception e)
			{
				OracleDataDictionaryMapper.WriteTrace(ConnectionString.Name, $"Oracle data dictionary refresh failed: {e}");
				return false;
			}
		}

		private void RefreshSchemas()
		{
			var stopwatch = Stopwatch.StartNew();
			RefreshSchemas(_dataDictionaryMapper.GetSchemaNames());
			Trace.WriteLine($"{DateTime.Now} - Fetch schema metadata finished in {stopwatch.Elapsed}. ");
		}

		private static Dictionary<TKey, TValue> SafeFetchDictionary<TKey, TValue>(Func<IEnumerable<KeyValuePair<TKey, TValue>>> fetchKeyValuePairFunction, string traceMessage)
		{
			try
			{
				return fetchKeyValuePairFunction().ToDictionary(k => k.Key, k => k.Value);
			}
			catch (Exception e)
			{
				Trace.WriteLine(traceMessage + e);
				return new Dictionary<TKey, TValue>();
			}
		}

		private void TryLoadSchemaObjectMetadataFromCache()
		{
			if (_cacheLoaded)
			{
				return;
			}

			Stream stream;
			OracleDataDictionary dataDictionary;
			if (CachedDataDictionaries.TryGetValue(_connectionStringName, out dataDictionary))
			{
				_dataDictionary = dataDictionary;
				BuildSupportLookups();
			}
			else if (MetadataCache.TryLoadDatabaseModelCache(_connectionStringName, out stream))
			{
				try
				{
					RaiseEvent(RefreshStarted);
					RaiseRefreshStatusChanged("Loading data dictionary metadata cache... ");
					Trace.WriteLine($"{DateTime.Now} - Attempt to load metadata for '{_connectionStringName}/{ConnectionIdentifier}' from cache. ");
					var stopwatch = Stopwatch.StartNew();
					_dataDictionary = CachedDataDictionaries[_connectionStringName] = OracleDataDictionary.Deserialize(stream);
					Trace.WriteLine($"{DateTime.Now} - Metadata for '{_connectionStringName}/{ConnectionIdentifier}' loaded from cache in {stopwatch.Elapsed}");
					BuildSupportLookups();
				}
				catch (Exception e)
				{
					Trace.WriteLine($"{DateTime.Now} - Oracle data dictionary cache deserialization failed: {e}");
				}
				finally
				{
					stream.Dispose();
					RaiseEvent(RefreshCompleted);
				}
			}

			_cacheLoaded = true;
		}

		private void BuildSupportLookups()
		{
			try
			{
				var functionMetadata = _dataDictionary.AllObjects.Values
					.OfType<IProgramCollection>()
					.SelectMany(o => o.Programs);

				_allProgramMetadata = FilterFunctionsWithUnavailableMetadata(functionMetadata)
					.Concat(_dataDictionary.NonSchemaFunctionMetadata.SelectMany(g => g))
					.ToLookup(m => m.Identifier);

				_uniqueConstraintReferringReferenceConstraints = BuildUniqueConstraintReferringReferenceConstraintLookup(_dataDictionary.AllObjects.Values);
			}
			catch (Exception e)
			{
				_dataDictionary = OracleDataDictionary.EmptyDictionary;
				Trace.WriteLine($"{DateTime.Now} - All function metadata or unique constraint referring reference constraint lookup initialization from cache failed: {e}");
			}
		}

		private static IEnumerable<OracleProgramMetadata> FilterFunctionsWithUnavailableMetadata(IEnumerable<OracleProgramMetadata> functions)
		{
			return functions.Where(m => m != null);
		}

		private void RaiseRefreshStatusChanged(string message)
		{
			RefreshStatusChanged?.Invoke(this, new DatabaseModelRefreshStatusChangedArgs(message));
		}

		private void RaiseEvent(EventHandler eventHandler)
		{
			eventHandler?.Invoke(this, EventArgs.Empty);
		}

		private bool TryGetPassword()
		{
			if (PasswordRequired == null)
			{
				return false;
			}

			var args = new DatabaseModelPasswordArgs();
			PasswordRequired(this, args);

			if (args.CancelConnection)
			{
				return false;
			}

			OracleConnectionStringRepository.ModifyConnectionString(_connectionString.ConnectionString, b => b.Password = args.Password.GetPlainText());

			return true;
		}

		public override Task Initialize()
		{
			return Task.Run((Action)InitializeInternal);
		}

		private void InitializeInternal()
		{
			try
			{
				RefreshSchemas(OracleSchemaResolver.ResolveSchemas(this));
			}
			catch (Exception e)
			{
				Trace.WriteLine($"{DateTime.Now} - Database model for connection '{ConnectionString.Name}' initialization failed: {e}");

				InitializationFailed?.Invoke(this, new DatabaseModelConnectionErrorArgs(e));

				return;
			}
			finally
			{
				OracleSchemaResolver.UnregisterActiveModel(this);
			}
			
			_isInitialized = true;
			
			RaiseEvent(Initialized);

			RefreshIfNeeded();

			if (_isDisposed)
			{
				return;
			}

			SetRefreshTimerInterval();
			_refreshTimer.Elapsed += RefreshTimerElapsedHandler;
			_refreshTimer.Start();
		}

		private void RefreshSchemas(IEnumerable<OracleSchema> schemas)
		{
			var allSchemas = schemas.ToDictionary(s => s.Name);
			_schemas = allSchemas.Values.Select(s => s.Name.Trim('"')).ToHashSet();
			allSchemas.Add(OracleObjectIdentifier.SchemaPublic, OracleSchema.Public);
			_allSchemas = new ReadOnlyDictionary<string, OracleSchema>(allSchemas);
		}

		private struct RefreshModel
		{
			public TaskCompletionSource<OracleDataDictionary> TaskCompletionSource { get; set; }
			
			public OracleDatabaseModel DatabaseModel { get; set; }
		}

		private struct DatabaseProperty
		{
			public Version Version { get; set; }
			public string DomainName { get; set; }
		}

		private class OracleSchemaResolver
		{
			private static readonly Dictionary<string, OracleSchemaResolver> ActiveResolvers = new Dictionary<string, OracleSchemaResolver>();
			private static readonly Dictionary<string, HashSet<OracleDatabaseModel>> ActiveDatabaseModels = new Dictionary<string, HashSet<OracleDatabaseModel>>();

			private readonly OracleDatabaseModel _databaseModel;
			private IReadOnlyCollection<OracleSchema> _schemas;

			private bool _enablePasswordRetrieval = true;

			private OracleSchemaResolver(OracleDatabaseModel databaseModel)
			{
				_databaseModel = databaseModel;
			}

			public static void Unregister(OracleDatabaseModel databaseModel)
			{
				lock (ActiveResolvers)
				{
					var connectionString = databaseModel.ConnectionString.ConnectionString;
					ActiveResolvers.Remove(connectionString);
					GetActiveModels(connectionString).Clear();
				}
			}

			public static void UnregisterActiveModel(OracleDatabaseModel databaseModel)
			{
				lock (ActiveResolvers)
				{
					var connectionString = databaseModel.ConnectionString.ConnectionString;
					var activeModels = GetActiveModels(connectionString);
					activeModels.Remove(databaseModel);

					OracleSchemaResolver resolver;
					if (activeModels.Count == 0 && ActiveResolvers.TryGetValue(connectionString, out resolver))
					{
						resolver._enablePasswordRetrieval = true;
					}
				}
			}

			public static IReadOnlyCollection<OracleSchema> ResolveSchemas(OracleDatabaseModel databaseModel)
			{
				OracleSchemaResolver resolver;
				lock (ActiveResolvers)
				{
					var connectionString = databaseModel.ConnectionString.ConnectionString;

					GetActiveModels(connectionString).Add(databaseModel);

					if (!ActiveResolvers.TryGetValue(connectionString, out resolver))
					{
						resolver = new OracleSchemaResolver(databaseModel);
						ActiveResolvers.Add(connectionString, resolver);
					}
				}

				resolver.ResolveSchemas();
				return resolver._schemas;
			}

			private static HashSet<OracleDatabaseModel> GetActiveModels(string connectionString)
			{
				HashSet<OracleDatabaseModel> activeModels;
				if (!ActiveDatabaseModels.TryGetValue(connectionString, out activeModels))
				{
					ActiveDatabaseModels.Add(connectionString, activeModels = new HashSet<OracleDatabaseModel>());
				}

				return activeModels;
			}

			private void ResolveSchemas()
			{
				lock (this)
				{
					if (_schemas != null)
					{
						return;
					}

					do
					{
						try
						{
							_databaseModel.EnsureDatabaseProperties();
							break;
						}
						catch (OracleException exception)
						{
							var exceptionCode = (OracleErrorCode)exception.Number;
							if (_enablePasswordRetrieval &&
							    exceptionCode.In(OracleErrorCode.NullPasswordGiven, OracleErrorCode.InvalidUsernameOrPassword) &&
							    (_enablePasswordRetrieval = _databaseModel.TryGetPassword()))
							{
								continue;
							}

							throw;
						}
					} while (true);

					_schemas = _databaseModel._dataDictionaryMapper.GetSchemaNames().ToList().AsReadOnly();
					Trace.WriteLine($"{DateTime.Now} - Connection string '{_databaseModel._connectionStringName}' schema metadata loaded. ");
				}
			}
		}
	}

	internal static class OracleConnectionStringRepository
	{
		public static Dictionary<string, OracleConnectionStringBuilder> ConnectionStringBuilders { get; } = new Dictionary<string, OracleConnectionStringBuilder>();

		public static void ModifyConnectionString(string connectionString, Action<OracleConnectionStringBuilder> modifyAction)
		{
			lock (ConnectionStringBuilders)
			{
				modifyAction(GetEntry(connectionString));
			}
		}

		public static string GetUserConnectionString(string connectionString)
		{
			return
				new OracleConnectionStringBuilder(GetEntry(connectionString).ConnectionString)
				{
					Pooling = false,
					SelfTuning = false
				}.ToString();
		}

		public static string GetBackgroundConnectionString(string connectionString)
		{
			return
				new OracleConnectionStringBuilder(GetEntry(connectionString).ConnectionString)
				{
					SelfTuning = false,
					MinPoolSize = 1,
					IncrPoolSize = 1
				}.ToString();
		}

		private static OracleConnectionStringBuilder GetEntry(string connectionString)
		{
			lock (ConnectionStringBuilders)
			{
				OracleConnectionStringBuilder connectionStringBuilder;
				if (!ConnectionStringBuilders.TryGetValue(connectionString, out connectionStringBuilder))
				{
					ConnectionStringBuilders[connectionString] = connectionStringBuilder = new OracleConnectionStringBuilder(connectionString);
				}

				return connectionStringBuilder;
			}
		}
	}

	internal enum OracleErrorCode
	{
		NullPasswordGiven = 1005,
		UserInvokedCancellation = 1013,
		InvalidUsernameOrPassword = 1017,
		NotConnectedToOracle = 3114,
		EndOfFileOnCommunicationChannel = 3113,
		TnsPacketWriterFailure = 12571,
		UnableToSendBreak = 12152,
		SuccessWithCompilationError = 24344,
		SessionTerminatedByDebugger = 30687
	}
}
