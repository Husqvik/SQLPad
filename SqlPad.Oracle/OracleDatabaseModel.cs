using System;
using System.Collections.Generic;
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
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;
using SqlPad.Oracle.ToolTips;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		internal const int InitialLongFetchSize = 131072;
		private const string ModuleNameSqlPadDatabaseModelBase = "Database model";
		private const string OracleDataAccessRegistryPath = @"Software\Oracle\ODP.NET";
		private const string OracleDataAccessComponents = "Oracle Data Access Components";

		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly Timer _refreshTimer = new Timer();
		private readonly List<OracleConnectionAdapter> _connectionAdapters = new List<OracleConnectionAdapter>();
		private bool _isInitialized;
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private Task _backgroundTask;
		private readonly CancellationTokenSource _backgroundTaskCancellationTokenSource = new CancellationTokenSource();
		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> _allFunctionMetadata = Enumerable.Empty<OracleProgramMetadata>().ToLookup(m => m.Identifier);
		private readonly ConnectionStringSettings _connectionString;
		private readonly string _moduleName;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private readonly DataDictionaryMapper _dataDictionaryMapper;
		private OracleDataDictionary _dataDictionary = OracleDataDictionary.EmptyDictionary;

		//private readonly OracleCustomTypeGenerator _customTypeGenerator;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		private static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();
		private static readonly Dictionary<string, DatabaseProperty> DatabaseProperties = new Dictionary<string, DatabaseProperty>();
		private static readonly HashSet<string> ActiveDataModelRefresh = new HashSet<string>();
		private static readonly Dictionary<string, List<RefreshModel>> WaitingDataModelRefresh = new Dictionary<string, List<RefreshModel>>();

		private OracleDatabaseModel(ConnectionStringSettings connectionString, string identifier)
		{
			_connectionString = connectionString;
			_moduleName = String.Format("{0}/{1}", ModuleNameSqlPadDatabaseModelBase, identifier);
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);
			_currentSchema = _oracleConnectionString.UserID;

			lock (ActiveDataModelRefresh)
			{
				if (!WaitingDataModelRefresh.ContainsKey(CachedConnectionStringName))
				{
					WaitingDataModelRefresh[CachedConnectionStringName] = new List<RefreshModel>();
				}
			}

			_dataDictionaryMapper = new DataDictionaryMapper(this);

			InternalRefreshStarted += InternalRefreshStartedHandler;
			InternalRefreshCompleted += InternalRefreshCompletedHandler;

			//_customTypeGenerator = OracleCustomTypeGenerator.GetCustomTypeGenerator(connectionString.Name);

			_connectionAdapters.Add(new OracleConnectionAdapter(this, identifier));
		}

		private static event EventHandler InternalRefreshStarted = delegate { };
		private static event EventHandler InternalRefreshCompleted = delegate { };

		private void InternalRefreshStartedHandler(object sender, EventArgs eventArgs)
		{
			var refreshedModel = GetDatabaseModelIfDifferentCompatibleInstance(sender);
			if (refreshedModel == null)
			{
				return;
			}

			_isRefreshing = true;
			RaiseEvent(RefreshStarted);
		}

		private void InternalRefreshCompletedHandler(object sender, EventArgs eventArgs)
		{
			var refreshedModel = GetDatabaseModelIfDifferentCompatibleInstance(sender);
			if (refreshedModel == null)
			{
				return;
			}

			_dataDictionary = refreshedModel._dataDictionary;
			_allFunctionMetadata = refreshedModel._allFunctionMetadata;

			Trace.WriteLine(String.Format("{0} - Metadata for '{1}' has been retrieved from the cache. ", DateTime.Now, CachedConnectionStringName));

			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(CachedConnectionStringName, out refreshModels))
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
			return sender == this || ConnectionString.ConnectionString != refreshedModel.ConnectionString.ConnectionString
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
			Trace.WriteLine(String.Format("Data model refresh timer set: {0} minute(s). ", ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod));
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
				? String.Format("{0} registry entry was not found. ", OracleDataAccessComponents)
				: String.Format("{0} version {1} found. ", OracleDataAccessComponents, odacVersion);

			Trace.WriteLine(traceMessage);

			Trace.WriteLine(String.Format("{0} assembly version {1}", OracleDataAccessComponents, typeof(OracleConnection).Assembly.FullName));
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
			if (_isRefreshing)
				return;

			_backgroundTask = Task.Factory.StartNew(action);
		}

		public override ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllFunctionMetadata { get { return _allFunctionMetadata; } }

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata { get { return _dataDictionary.NonSchemaFunctionMetadata; } }

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata { get { return _dataDictionary.BuiltInPackageFunctionMetadata; } }

		public override ConnectionStringSettings ConnectionString { get { return _connectionString; } }

		public override bool IsInitialized { get { return _isInitialized; } }

		public override bool IsMetadataAvailable { get { return _dataDictionary != OracleDataDictionary.EmptyDictionary; } }

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set
			{
				_currentSchema = value;

				foreach (var connectionAdapter in _connectionAdapters)
				{
					connectionAdapter.SwitchCurrentSchema();
				}
			}
		}

		public override string DatabaseDomainName { get { return DatabaseProperties[_connectionString.ConnectionString].DomainName; } }

		public override ICollection<string> Schemas { get { return _schemas; } }
		
		public override ICollection<string> AllSchemas { get { return _allSchemas; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _dataDictionary.AllObjects; } }

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get { return _dataDictionary.DatabaseLinks; } }

		public override ICollection<string> CharacterSets { get { return _dataDictionary.CharacterSets; } }

		public override IDictionary<int, string> StatisticsKeys { get { return _dataDictionary.StatisticsKeys; } }

		public override IDictionary<string, string> SystemParameters { get { return _dataDictionary.SystemParameters; } }

		public override Version Version { get { return DatabaseProperties[_connectionString.ConnectionString].Version; } }

		public override void RefreshIfNeeded()
		{
			if (IsRefreshNeeded)
			{
				Refresh();
			}
		}

		public override bool IsFresh
		{
			get { return !IsRefreshNeeded; }
		}

		private bool IsRefreshNeeded
		{
			get { return DataDictionaryValidityTimestamp < DateTime.Now; }
		}

		private DateTime DataDictionaryValidityTimestamp
		{
			get { return _dataDictionary.Timestamp.AddMinutes(ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod); }
		}

		private string CachedConnectionStringName
		{
			get { return _oracleConnectionString.DataSource + "_" + _oracleConnectionString.UserID; }
		}

		public override ILookup<string, string> ContextData
		{
			get { return _dataDictionaryMapper.GetContextData(); }
		}

		public override Task Refresh(bool force = false)
		{
			lock (ActiveDataModelRefresh)
			{
				if (ActiveDataModelRefresh.Contains(CachedConnectionStringName))
				{
					var taskCompletionSource = new TaskCompletionSource<OracleDataDictionary>();
					WaitingDataModelRefresh[CachedConnectionStringName].Add(new RefreshModel { DatabaseModel = this, TaskCompletionSource = taskCompletionSource });

					Trace.WriteLine(String.Format("{0} - Cache for '{1}' is being loaded by other requester. Waiting until operation finishes. ", DateTime.Now, CachedConnectionStringName));

					RaiseEvent(RefreshStarted);
					return taskCompletionSource.Task;
				}

				ActiveDataModelRefresh.Add(CachedConnectionStringName);
			}

			ExecuteActionAsync(() => LoadSchemaObjectMetadata(force));

			return _backgroundTask;
		}

		public async override Task<ExecutionPlanItemCollection> ExplainPlanAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			if (String.IsNullOrEmpty(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name))
			{
				throw new InvalidOperationException("OracleConfiguration/ExecutionPlan/TargetTable[Name] is missing. ");
			}

			var planKey = Convert.ToString(executionModel.StatementText.GetHashCode());
			var targetTableIdentifier = OracleObjectIdentifier.Create(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Schema, OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name);
			var explainPlanDataProvider = new ExplainPlanDataProvider(executionModel.StatementText, planKey, targetTableIdentifier);

			await UpdateModelAsync(cancellationToken, false, explainPlanDataProvider.CreateExplainPlanUpdater, explainPlanDataProvider.LoadExplainPlanUpdater);

			return explainPlanDataProvider.ItemCollection;
		}

		public async override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			var scriptDataProvider = new ObjectScriptDataProvider(schemaObject);
			await UpdateModelAsync(cancellationToken, false, scriptDataProvider);
			return scriptDataProvider.ScriptText;
		}

		public async override Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var partitionDataProvider = new PartitionDataProvider(dataModel, Version);
			var tableSpaceAllocationUpdater = new TableSpaceAllocationDataProvider(dataModel, dataModel.Owner, dataModel.Name);
			await UpdateModelAsync(cancellationToken, true, partitionDataProvider.PartitionDetailDataProvider, partitionDataProvider.SubPartitionDetailDataProvider, tableSpaceAllocationUpdater);
		}

		public async override Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var partitionDataProvider = new PartitionDataProvider(dataModel, Version);
			var tableSpaceAllocationUpdater = new TableSpaceAllocationDataProvider(dataModel, dataModel.Owner, dataModel.Name);
			await UpdateModelAsync(cancellationToken, true, partitionDataProvider.SubPartitionDetailDataProvider, tableSpaceAllocationUpdater);
		}

		public async override Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var tableDetailDataProvider = new TableDetailDataProvider(dataModel, objectIdentifier);
			var tableSpaceAllocationUpdater = new TableSpaceAllocationDataProvider(dataModel, objectIdentifier, String.Empty);
			var tableCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, null);
			var tableInMemorySpaceAllocationUpdater = new TableInMemorySpaceAllocationDataProvider(dataModel, objectIdentifier, Version);
			var indexDetailDataProvider = new IndexDetailDataProvider(dataModel, objectIdentifier, null);
			var indexColumnDataProvider = new IndexColumnDataProvider(dataModel, objectIdentifier, null);
			var partitionDataProvider = new PartitionDataProvider(dataModel, objectIdentifier, Version);
			await UpdateModelAsync(cancellationToken, true, tableDetailDataProvider, tableCommentDataProvider, tableSpaceAllocationUpdater, tableInMemorySpaceAllocationUpdater, indexDetailDataProvider, indexColumnDataProvider, partitionDataProvider.PartitionDetailDataProvider, partitionDataProvider.SubPartitionDetailDataProvider);
		}

		public async override Task UpdateViewDetailsAsync(OracleObjectIdentifier objectIdentifier, ViewDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var viewCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, null);
			var columnConstraintDataProvider = new ConstraintDataProvider(dataModel, objectIdentifier, null);
			await UpdateModelAsync(cancellationToken, true, viewCommentDataProvider, columnConstraintDataProvider);
		}

		public async override Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var columnDetailDataProvider = new ColumnDetailDataProvider(dataModel, objectIdentifier, columnName);
			var columnCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, columnName);
			var columnConstraintDataProvider = new ConstraintDataProvider(dataModel, objectIdentifier, columnName);
			var columnIndexesDataProvider = new IndexDetailDataProvider(dataModel, objectIdentifier, columnName);
			var indexColumnDataProvider = new IndexColumnDataProvider(dataModel, objectIdentifier, columnName);
			var columnHistogramUpdater = new ColumnDetailHistogramDataProvider(dataModel, objectIdentifier, columnName);
			var columnInMemoryDetailsUpdater = new ColumnDetailInMemoryDataProvider(dataModel, objectIdentifier, columnName, Version);
			await UpdateModelAsync(cancellationToken, true, columnDetailDataProvider, columnCommentDataProvider, columnConstraintDataProvider, columnIndexesDataProvider, indexColumnDataProvider, columnHistogramUpdater, columnInMemoryDetailsUpdater);
		}

		public async override Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken)
		{
			var remoteTableColumnDataProvider = new RemoteTableColumnDataProvider(databaseLink, schemaObject);
			await UpdateModelAsync(cancellationToken, false, remoteTableColumnDataProvider);
			return remoteTableColumnDataProvider.Columns;
		}

		private Task UpdateModelAsync(CancellationToken cancellationToken, bool suppressException, params IModelDataProvider[] updaters)
		{
			return _connectionAdapters[0].UpdateModelAsync(cancellationToken, suppressException, updaters);
		}

		public override event EventHandler Initialized;
		
		public override event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;
		
		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshCompleted;

		public override void Dispose()
		{
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
			Disconnected = null;
			InitializationFailed = null;
			RefreshStarted = null;
			RefreshCompleted = null;

			foreach (var adapter in _connectionAdapters)
			{
				adapter.Dispose();
			}

			_connectionAdapters.Clear();

			lock (DatabaseModels)
			{
				if (DatabaseModels.ContainsValue(this))
				{
					DatabaseModels.Remove(_connectionString.ConnectionString);
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
					_allFunctionMetadata = _allFunctionMetadata
				};

			return clone;
		}

		public override async Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var connectionAdapter = _connectionAdapters[0];

			try
			{
				return await connectionAdapter.ExecuteStatementAsync(executionModel, cancellationToken);
			}
			catch (OracleException exception)
			{
				var errorCode = (OracleErrorCode)exception.Number;
				if (errorCode.In(OracleErrorCode.EndOfFileOnCommunicationChannel, OracleErrorCode.NotConnectedToOracle))
				{
					OracleSchemaResolver.Unregister(this);

					_isInitialized = false;

					if (Disconnected != null)
					{
						Disconnected(this, new DatabaseModelConnectionErrorArgs(exception));
					}
				}

				throw;
			}
		}

		private void EnsureDatabaseVersion(OracleConnection connection)
		{
			DatabaseProperty property;
			if (!DatabaseProperties.TryGetValue(_connectionString.ConnectionString, out property))
			{
				var versionString = connection.ServerVersion.Remove(connection.ServerVersion.LastIndexOf('.'));

				DatabaseProperties[_connectionString.ConnectionString] =
					new DatabaseProperty
					{
						DomainName = connection.DatabaseDomainName == "null" ? null : connection.DatabaseDomainName,
						Version = Version.Parse(versionString)
					};
			}
		}

		internal IEnumerable<T> ExecuteReader<T>(Func<Version, string> getCommandTextFunction, Func<Version, OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					connection.Open();

					EnsureDatabaseVersion(connection);

					command.CommandText = getCommandTextFunction(Version);
					command.BindByName = true;
					command.InitialLONGFetchSize = DataDictionaryMapper.LongFetchSize;

					connection.ModuleName = _moduleName;
					connection.ActionName = "Fetch data dictionary metadata";

					using (var task = command.ExecuteReaderAsynchronous(CommandBehavior.CloseConnection, _backgroundTaskCancellationTokenSource.Token))
					{
						try
						{
							task.Wait();
						}
						catch (AggregateException)
						{
							if (task.IsCanceled)
							{
								yield break;
							}
							
							throw;
						}

						using (var reader = task.Result)
						{
							while (reader.Read())
							{
								yield return formatFunction(Version, reader);
							}
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
				ActiveDataModelRefresh.Remove(CachedConnectionStringName);
			}
		}

		private void LoadSchemaObjectMetadata(bool force)
		{
			TryLoadSchemaObjectMetadataFromCache();

			var isRefreshDone = !IsRefreshNeeded && !force;
			if (isRefreshDone)
			{
				Trace.WriteLine(String.Format("{0} - Cache for '{1}' is valid until {2}. ", DateTime.Now, CachedConnectionStringName, DataDictionaryValidityTimestamp));
				return;
			}

			var reason = force ? "has been forced to refresh" : (_dataDictionary.Timestamp > DateTime.MinValue ? "has expired" : "does not exist or is corrupted");
			Trace.WriteLine(String.Format("{0} - Cache for '{1}' {2}. Cache refresh started. ", DateTime.Now, CachedConnectionStringName, reason));

			_isRefreshing = true;
			RaiseEvent(InternalRefreshStarted);
			RaiseEvent(RefreshStarted);

			var isRefreshSuccessful = RefreshSchemaObjectMetadata();

			RemoveActiveRefreshTask();

			if (isRefreshSuccessful)
			{
				try
				{
					MetadataCache.StoreDatabaseModelCache(CachedConnectionStringName, stream => _dataDictionary.Serialize(stream));
				}
				catch (Exception e)
				{
					Trace.WriteLine(String.Format("Storing metadata cache failed: {0}", e));
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
				var allObjects = _dataDictionaryMapper.BuildDataDictionary();

				var userFunctions = _dataDictionaryMapper.GetUserFunctionMetadata().SelectMany(g => g).ToArray();
				var builtInFunctions = _dataDictionaryMapper.GetBuiltInFunctionMetadata().SelectMany(g => g).ToArray();
				_allFunctionMetadata = builtInFunctions.Concat(userFunctions.Where(f => f.Type == ProgramType.Function))
					.ToLookup(m => m.Identifier);

				var stopwatch = Stopwatch.StartNew();

				var nonSchemaBuiltInFunctionMetadata = new List<OracleProgramMetadata>();

				foreach (var programMetadata in builtInFunctions.Concat(userFunctions))
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
						((OraclePackage)schemaObject).Functions.Add(programMetadata);
						programMetadata.Owner = schemaObject;
					}
					else
					{
						var programIdentifier = OracleObjectIdentifier.Create(programMetadata.Identifier.Owner, programMetadata.Identifier.Name);
						if (allObjects.TryGetFirstValue(out schemaObject, programIdentifier))
						{
							if (programMetadata.Type == ProgramType.Function)
							{
								((OracleFunction)schemaObject).Metadata = programMetadata;
							}
							else
							{
								((OracleProcedure)schemaObject).Metadata = programMetadata;
							}
							
							programMetadata.Owner = schemaObject;
						}
					}
				}

				Trace.WriteLine(String.Format("Function and procedure metadata schema object mapping finished in {0}. ", stopwatch.Elapsed));

				var databaseLinks = _dataDictionaryMapper.GetDatabaseLinks();
				var characterSets = _dataDictionaryMapper.GetCharacterSets();
				var statisticsKeys = SafeFetchDictionary(_dataDictionaryMapper.GetStatisticsKeys, "DataDictionaryMapper.GetStatisticsKeys failed: ");
				var systemParameters = SafeFetchDictionary(_dataDictionaryMapper.GetSystemParameters, "DataDictionaryMapper.GetSystemParameters failed: ");

				_dataDictionary = new OracleDataDictionary(allObjects, databaseLinks, nonSchemaBuiltInFunctionMetadata, characterSets, statisticsKeys, systemParameters, lastRefresh);

				Trace.WriteLine(String.Format("{0} - Data dictionary metadata cache has been initialized successfully. ", DateTime.Now));

				//_customTypeGenerator.GenerateCustomTypeAssembly(_dataDictionary);

				CachedDataDictionaries[CachedConnectionStringName] = _dataDictionary;

				return true;
			}
			catch(Exception e)
			{
				Trace.WriteLine(String.Format("Oracle data dictionary refresh failed: {0}", e));
				return false;
			}
		}

		private Dictionary<TKey, TValue> SafeFetchDictionary<TKey, TValue>(Func<IEnumerable<KeyValuePair<TKey, TValue>>> fetchKeyValuePairFunction, string traceMessage)
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
				return;

			Stream stream;
			OracleDataDictionary dataDictionary;
			if (CachedDataDictionaries.TryGetValue(CachedConnectionStringName, out dataDictionary))
			{
				_dataDictionary = dataDictionary;
				BuildAllFunctionMetadata();
			}
			else if (MetadataCache.TryLoadDatabaseModelCache(CachedConnectionStringName, out stream))
			{
				try
				{
					RaiseEvent(RefreshStarted);
					var stopwatch = Stopwatch.StartNew();
					_dataDictionary = CachedDataDictionaries[CachedConnectionStringName] = OracleDataDictionary.Deserialize(stream);
					Trace.WriteLine(String.Format("{0} - Cache for '{1}' loaded in {2}", DateTime.Now, CachedConnectionStringName, stopwatch.Elapsed));
					BuildAllFunctionMetadata();
					RemoveActiveRefreshTask();
				}
				catch (Exception e)
				{
					Trace.WriteLine(String.Format("Oracle data dictionary cache deserialization failed: {0}", e));
				}
				finally
				{
					stream.Dispose();
					RaiseEvent(RefreshCompleted);
				}
			}

			_cacheLoaded = true;
		}

		private void BuildAllFunctionMetadata()
		{
			try
			{
				var functionMetadata = _dataDictionary.AllObjects.Values
					.OfType<IFunctionCollection>()
					.SelectMany(o => o.Functions);

				functionMetadata = FilterFunctionsWithUnavailableMetadata(functionMetadata)
					.Concat(_dataDictionary.NonSchemaFunctionMetadata.SelectMany(g => g));

				_allFunctionMetadata = functionMetadata.ToLookup(m => m.Identifier);
			}
			catch (Exception e)
			{
				_dataDictionary = OracleDataDictionary.EmptyDictionary;
				Trace.WriteLine(String.Format("All function metadata lookup initialization from cache failed: {0}", e));
			}
		}

		private IEnumerable<OracleProgramMetadata> FilterFunctionsWithUnavailableMetadata(IEnumerable<OracleProgramMetadata> functions)
		{
			return functions.Where(m => m != null && m.Type != ProgramType.Procedure);
		} 

		private void RaiseEvent(EventHandler eventHandler)
		{
			if (eventHandler != null)
			{
				eventHandler(this, EventArgs.Empty);
			}
		}

		public override Task Initialize()
		{
			return Task.Factory.StartNew(InitializeInternal);
		}

		private void InitializeInternal()
		{
			try
			{
				LoadSchemaNames();
			}
			catch(Exception e)
			{
				Trace.WriteLine(String.Format("Database model initialization failed: {0}", e));

				if (InitializationFailed != null)
				{
					InitializationFailed(this, new DatabaseModelConnectionErrorArgs(e));
				}

				return;
			}
			
			_isInitialized = true;
			
			RaiseEvent(Initialized);

			RefreshIfNeeded();

			SetRefreshTimerInterval();
			_refreshTimer.Elapsed += RefreshTimerElapsedHandler;
			_refreshTimer.Start();
		}

		private void LoadSchemaNames()
		{
			_schemas = new HashSet<string>(OracleSchemaResolver.ResolveSchemas(this));
			_allSchemas = new HashSet<string>(_schemas.Select(DataDictionaryMapper.QualifyStringObject)) { SchemaPublic };
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

			private readonly OracleDatabaseModel _databaseModel;
			private IReadOnlyCollection<string> _schemas;

			private OracleSchemaResolver(OracleDatabaseModel databaseModel)
			{
				_databaseModel = databaseModel;
			}

			public static void Unregister(OracleDatabaseModel databaseModel)
			{
				lock (ActiveResolvers)
				{
					ActiveResolvers.Remove(databaseModel.ConnectionString.ConnectionString);
				}
			}

			public static IReadOnlyCollection<string> ResolveSchemas(OracleDatabaseModel databaseModel)
			{
				OracleSchemaResolver resolver;
				lock (ActiveResolvers)
				{
					if (!ActiveResolvers.TryGetValue(databaseModel.ConnectionString.ConnectionString, out resolver))
					{
						resolver = new OracleSchemaResolver(databaseModel);
						ActiveResolvers.Add(databaseModel.ConnectionString.ConnectionString, resolver);
					}
				}

				resolver.ResolveSchemas();
				return resolver._schemas;
			}

			private void ResolveSchemas()
			{
				lock (this)
				{
					if (_schemas != null)
					{
						return;
					}

					_schemas = _databaseModel._dataDictionaryMapper.GetSchemaNames().ToList().AsReadOnly();
				}
			}
		}
	}

	internal enum OracleErrorCode
	{
		UserInvokedCancellation = 1013,
		NotConnectedToOracle = 3114,
		EndOfFileOnCommunicationChannel = 3113,
		SuccessWithCompilationError = 24344
	}
}
