using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;
using SqlPad.Oracle.DataDictionary;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;
using SqlPad.Oracle.ToolTips;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		internal const int OracleErrorCodeUserInvokedCancellation = 1013;
		internal const int OracleErrorCodeNotConnectedToOracle = 3114;
		internal const int OracleErrorCodeEndOfFileOnCommunicationChannel = 3113;
		internal const int OracleErrorCodeSuccessWithCompilationError = 24344;
		internal const int InitialLongFetchSize = 131072;
		private const string ModuleNameSqlPadDatabaseModel = "SQLPad database model";
		private const string OracleDataAccessRegistryPath = @"Software\Oracle\ODP.NET";
		private const string OracleDataAccessComponents = "Oracle Data Access Components";

		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly Timer _timer = new Timer();
		private bool _isInitialized;
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private bool _isExecuting;
		private bool _databaseOutputEnabled;
		private Task _backgroundTask;
		private readonly CancellationTokenSource _backgroundTaskCancellationTokenSource = new CancellationTokenSource();
		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> _allFunctionMetadata = Enumerable.Empty<OracleProgramMetadata>().ToLookup(m => m.Identifier);
		private readonly ConnectionStringSettings _connectionString;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private readonly DataDictionaryMapper _dataDictionaryMapper;
		private OracleDataDictionary _dataDictionary = OracleDataDictionary.EmptyDictionary;
		private OracleConnection _userConnection;
		private OracleDataReader _userDataReader;
		private OracleCommand _userCommand;
		private OracleTransaction _userTransaction;
		//private readonly OracleCustomTypeGenerator _customTypeGenerator;
		private int _userSessionId;
		private string _userTransactionId;
		private IsolationLevel _userTransactionIsolationLevel;
		private bool _userCommandHasCompilationErrors;
		private SessionExecutionStatisticsDataProvider _executionStatisticsDataProvider;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		private static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();
		private static readonly Dictionary<string, DatabaseProperty> DatabaseProperties = new Dictionary<string, DatabaseProperty>();
		private static readonly HashSet<string> ActiveDataModelRefresh = new HashSet<string>();
		private static readonly Dictionary<string, List<RefreshModel>> WaitingDataModelRefresh = new Dictionary<string, List<RefreshModel>>();

		private OracleDatabaseModel(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;
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

			InitializeUserConnection();

			InternalRefreshStarted += InternalRefreshStartedHandler;
			InternalRefreshCompleted += InternalRefreshCompletedHandler;

			//_customTypeGenerator = OracleCustomTypeGenerator.GetCustomTypeGenerator(connectionString.Name);
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


		private void InitializeUserConnection()
		{
			DisposeUserConnection();

			_isInitialized = false;
			_databaseOutputEnabled = false;

			_userConnection = new OracleConnection(_connectionString.ConnectionString);
			_userConnection.InfoMessage += UserConnectionInfoMessageHandler;
		}

		private void UserConnectionInfoMessageHandler(object sender, OracleInfoMessageEventArgs args)
		{
			var containsCompilationError = args.Errors.Cast<OracleError>().Any(e => e.Number == OracleErrorCodeSuccessWithCompilationError);
			if (containsCompilationError)
			{
				_userCommandHasCompilationErrors = true;
			}
			else
			{
				Trace.WriteLine(args.ToString());
			}
		}

		private void DisposeUserConnection()
		{
			if (_userConnection == null)
			{
				return;
			}
			
			_userConnection.InfoMessage -= UserConnectionInfoMessageHandler;
			_userConnection.Dispose();
		}

		private void TimerElapsedHandler(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			SetRefreshTimerInterval();
			RefreshIfNeeded();
		}

		private void SetRefreshTimerInterval()
		{
			_timer.Interval = ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod * 60000;
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

		public static OracleDatabaseModel GetDatabaseModel(ConnectionStringSettings connectionString)
		{
			OracleDatabaseModel databaseModel;
			if (DatabaseModels.TryGetValue(connectionString.ConnectionString, out databaseModel))
			{
				databaseModel = databaseModel.Clone();
			}
			else
			{
				DatabaseModels[connectionString.ConnectionString] = databaseModel = new OracleDatabaseModel(connectionString);
			}

			return databaseModel;
		}

		private void ExecuteActionAsync(Action action)
		{
			if (_isRefreshing)
				return;

			_backgroundTask = Task.Factory.StartNew(action);
		}

		internal string UserCommandSqlId { get; private set; }
		
		internal int UserCommandChildNumber { get; private set; }

		public override ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllFunctionMetadata { get { return _allFunctionMetadata; } }

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata { get { return _dataDictionary.NonSchemaFunctionMetadata; } }

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata { get { return _dataDictionary.BuiltInPackageFunctionMetadata; } }

		public override ConnectionStringSettings ConnectionString { get { return _connectionString; } }

		public override bool IsInitialized { get { return _isInitialized; } }

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set
			{
				_currentSchema = value;
				
				if (!_isInitialized || _userConnection.State != ConnectionState.Open)
				{
					return;
				}

				InitializeSession(CancellationToken.None).Wait();
			}
		}

		public override string DatabaseDomainName { get { return DatabaseProperties[_connectionString.ConnectionString].DomainName; } }

		public override bool HasActiveTransaction { get { return !String.IsNullOrEmpty(_userTransactionId); } }

		public override void CommitTransaction()
		{
			ExecuteUserTransactionAction(t => t.Commit());
		}

		public override Task RollbackTransaction()
		{
			return Task.Factory.StartNew(() => ExecuteUserTransactionAction(t => t.Rollback()));
		}

		private void ExecuteUserTransactionAction(Action<OracleTransaction> action)
		{
			if (!HasActiveTransaction)
			{
				return;
			}

			try
			{
				_isExecuting = true;
				action(_userTransaction);
			}
			finally
			{
				_isExecuting = false;
			}

			_userTransactionId = null;
			_userTransactionIsolationLevel = IsolationLevel.Unspecified;

			DisposeUserTransaction();
		}

		private void DisposeUserTransaction()
		{
			_userTransaction.Dispose();
			_userTransaction = null;
		}

		public override void CloseActiveReader()
		{
			DisposeCommandAndReader();
		}

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

		public override bool EnableDatabaseOutput { get; set; }

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

		public override async Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			await UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionEndExecutionStatisticsDataProvider);
			return _executionStatisticsDataProvider.ExecutionStatistics;
		}

		public async override Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			var cursorExecutionStatisticsDataProvider = new CursorExecutionStatisticsDataProvider(UserCommandSqlId, UserCommandChildNumber);
			var displayCursorDataProvider = new DisplayCursorDataProvider(UserCommandSqlId, UserCommandChildNumber);
			await UpdateModelAsync(cancellationToken, true, cursorExecutionStatisticsDataProvider, displayCursorDataProvider);
			cursorExecutionStatisticsDataProvider.ItemCollection.PlanText = displayCursorDataProvider.PlanText;
			return cursorExecutionStatisticsDataProvider.ItemCollection;
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

		private async Task UpdateModelAsync(CancellationToken cancellationToken, bool suppressException, params IModelDataProvider[] updaters)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;

					OracleTransaction transaction = null;

					try
					{
						foreach (var updater in updaters)
						{
							command.Parameters.Clear();
							command.CommandText = String.Empty;
							command.CommandType = CommandType.Text;
							updater.InitializeCommand(command);

							try
							{
								if (updater.IsValid)
								{
									if (await EnsureConnectionOpen(connection, cancellationToken))
									{
										connection.ModuleName = ModuleNameSqlPadDatabaseModel;
										connection.ActionName = "Model data provider";

										using (var setSchemaCommand = connection.CreateCommand())
										{
											await SetCurrentSchema(setSchemaCommand, cancellationToken);
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
											updater.MapReaderData(reader);
										}
									}
								}
							}
							catch (OracleException exception)
							{
								if (exception.Number == OracleErrorCodeUserInvokedCancellation)
								{
									break;
								}
								
								throw;
							}
						}
					}
					catch (Exception e)
					{
						Trace.WriteLine(String.Format("Update model failed: {0}", e));

						if (!suppressException)
						{
							throw;
						}
					}
					finally
					{
						if (transaction != null)
						{
							transaction.Rollback();
							transaction.Dispose();
						}
					}
				}
			}
		}

		public override event EventHandler Initialized;
		
		public override event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;
		
		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshCompleted;

		public override bool IsExecuting { get { return _isExecuting; } }

		public override bool CanFetch
		{
			get { return CanFetchFromReader(_userDataReader) && !_isExecuting; }
		}

		public override void Dispose()
		{
			_timer.Stop();
			_timer.Dispose();

			RollbackTransaction();

			DisposeCommandAndReader();

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
			InitializationFailed = null;
			RefreshStarted = null;
			RefreshCompleted = null;

			DisposeUserConnection();

			if (DatabaseModels.ContainsValue(this))
			{
				DatabaseModels.Remove(_connectionString.ConnectionString);
			}
		}

		private void DisposeCommandAndReader()
		{
			if (_userDataReader != null)
			{
				_userDataReader.Dispose();
			}

			if (_userCommand != null)
			{
				_userCommand.Dispose();
			}
		}

		public OracleDatabaseModel Clone()
		{
			var clone =
				new OracleDatabaseModel(ConnectionString)
				{
					_currentSchema = _currentSchema,
					_dataDictionary = _dataDictionary,
					_allFunctionMetadata = _allFunctionMetadata
				};

			return clone;
		}

		private async Task InitializeSession(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				await SetCurrentSchema(command, cancellationToken);

				command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') SID FROM SYS.DUAL";
				_userSessionId = Convert.ToInt32(await command.ExecuteScalarAsynchronous(cancellationToken));

				var startupScript = OracleConfiguration.Configuration.StartupScript;
				if (String.IsNullOrWhiteSpace(startupScript))
				{
					return;
				}
				
				var statements = await new OracleSqlParser().ParseAsync(startupScript, cancellationToken);
				foreach (var statement in statements)
				{
					command.CommandText = statement.RootNode.GetText(startupScript);

					try
					{
						await command.ExecuteNonQueryAsynchronous(cancellationToken);
						Trace.WriteLine(String.Format("Startup script command '{0}' executed successfully. ", command.CommandText));
					}
					catch (Exception e)
					{
						Trace.WriteLine(String.Format("Startup script command '{0}' failed: {1}", command.CommandText, e));
					}
				}
			}
		}

		private async Task SetCurrentSchema(OracleCommand command, CancellationToken cancellationToken)
		{
			command.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = \"{0}\"", _currentSchema);
			await command.ExecuteNonQueryAsynchronous(cancellationToken);
		}

		private async Task<bool> EnsureUserConnectionOpen(CancellationToken cancellationToken)
		{
			var isConnectionStateChanged = await EnsureConnectionOpen(_userConnection, cancellationToken);
			if (isConnectionStateChanged)
			{
				_userConnection.ModuleName = ModuleNameSqlPadDatabaseModel;
				_userConnection.ActionName = "User query";
			}

			return isConnectionStateChanged;
		}

		private static async Task<bool> EnsureConnectionOpen(OracleConnection connection, CancellationToken cancellationToken)
		{
			if (connection.State == ConnectionState.Open)
			{
				return false;
			}

			await connection.OpenAsynchronous(cancellationToken);
			return true;
		}

		private async Task EnsureDatabaseOutput(CancellationToken cancellationToken)
		{
			if ((EnableDatabaseOutput && _databaseOutputEnabled) ||
				!EnableDatabaseOutput && !_databaseOutputEnabled)
			{
				return;
			}

			_userCommand.CommandText = String.Format("CALL DBMS_OUTPUT.{0}", EnableDatabaseOutput ? "ENABLE(1)" : "DISABLE()");
			await _userCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			_databaseOutputEnabled = EnableDatabaseOutput;
		}

		private static void SetOracleGlobalization()
		{
			var info = OracleGlobalization.GetClientInfo();
			var numberFormat = CultureInfo.CurrentCulture.NumberFormat;
			var decimalSeparator = numberFormat.NumberDecimalSeparator;
			var groupSeparator = numberFormat.NumberGroupSeparator;
			info.NumericCharacters = String.Format("{0}{1}", decimalSeparator.Length == 1 ? decimalSeparator : ".", groupSeparator.Length == 1 ? groupSeparator : " ");
			OracleGlobalization.SetThreadInfo(info);
		}

		private async Task<StatementExecutionResult> ExecuteUserStatement(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			_userCommandHasCompilationErrors = false;

			SetOracleGlobalization();

			if (await EnsureUserConnectionOpen(cancellationToken))
			{
				await InitializeSession(cancellationToken);
			}

			_userCommand = _userConnection.CreateCommand();

			await EnsureDatabaseOutput(cancellationToken);

			_userCommand.BindByName = true;
			_userCommand.AddToStatementCache = false;

			if (_userTransaction == null)
			{
				_userTransaction = _userConnection.BeginTransaction();
			}

			_userCommand.CommandText = executionModel.StatementText.Replace("\r\n", "\n");
			_userCommand.InitialLONGFetchSize = InitialLongFetchSize;

			foreach (var variable in executionModel.BindVariables)
			{
				_userCommand.AddSimpleParameter(variable.Name, variable.Value, variable.DataType);
			}

			if (executionModel.GatherExecutionStatistics)
			{
				_executionStatisticsDataProvider = new SessionExecutionStatisticsDataProvider(StatisticsKeys, _userSessionId);
				await UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionBeginExecutionStatisticsDataProvider);
			}

			//var debuggerSession = new OracleDebuggerSession(_userConnection);
			//Task.Factory.StartNew(debuggerSession.Start);

			_userDataReader = await _userCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);

			var exception = await ResolveExecutionPlanIdentifiersAndTransactionStatus(cancellationToken);
			if (exception != null)
			{
				await SafeResolveTransactionStatus(cancellationToken);
			}
			
			UpdateBindVariables(executionModel);

			return
				new StatementExecutionResult
				{
					AffectedRowCount = _userDataReader.RecordsAffected,
					DatabaseOutput = await RetrieveDatabaseOutput(cancellationToken),
					ExecutedSuccessfully = true,
					ColumnHeaders = GetColumnHeadersFromReader(_userDataReader),
					InitialResultSet = await FetchRecordsFromReader(_userDataReader, executionModel.InitialFetchRowCount, false).EnumerateAsync(cancellationToken),
					CompilationErrors = _userCommandHasCompilationErrors ? await RetrieveCompilationErrors(executionModel.Statement, cancellationToken) : new CompilationError[0]
				};
		}

		private async Task<Exception> ResolveExecutionPlanIdentifiersAndTransactionStatus(CancellationToken cancellationToken)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;
					command.CommandText = DatabaseCommands.SelectExecutionPlanIdentifiersCommandText;
					command.AddSimpleParameter("SID", _userSessionId);

					try
					{
						connection.Open();

						using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
						{
							if (reader.Read())
							{
								UserCommandSqlId = (string)reader["SQL_ID"];
								UserCommandChildNumber = Convert.ToInt32(reader["SQL_CHILD_NUMBER"]);
								_userTransactionId = OracleReaderValueConvert.ToString(reader["TRANSACTION_ID"]);
								_userTransactionIsolationLevel = (IsolationLevel)Convert.ToInt32(reader["TRANSACTION_ISOLATION_LEVEL"]);
							}
							else
							{
								UserCommandSqlId = null;
							}
						}

						return null;
					}
					catch (OracleException e)
					{
						Trace.WriteLine(String.Format("Execution plan identifers and transaction status could not been fetched: {0}", e));
						return e;
					}
				}
			}
		}

		private async Task SafeResolveTransactionStatus(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = DatabaseCommands.SelectLocalTransactionIdCommandText;
				_userTransactionId = OracleReaderValueConvert.ToString(await command.ExecuteScalarAsynchronous(cancellationToken));
				_userTransactionIsolationLevel = String.IsNullOrEmpty(_userTransactionId) ? IsolationLevel.Unspecified : IsolationLevel.ReadCommitted;
			}
		}

		private void UpdateBindVariables(StatementExecutionModel executionModel)
		{
			var bindVariableModels = executionModel.BindVariables.ToDictionary(v => v.Name, v => v);
			foreach (OracleParameter parameter in _userCommand.Parameters)
			{
				var value = parameter.Value;
				if (parameter.Value is OracleDecimal)
				{
					var oracleNumber = new OracleNumber((OracleDecimal)parameter.Value);
					value = oracleNumber.IsNull ? String.Empty : oracleNumber.ToSqlLiteral();
				}

				if (parameter.Value is OracleString)
				{
					var oracleString = (OracleString)parameter.Value;
					value = oracleString.IsNull ? String.Empty : oracleString.Value;
				}
				
				if (parameter.Value is OracleDate)
				{
					var oracleDate = (OracleDate)parameter.Value;
					value = oracleDate.IsNull ? (DateTime?)null : oracleDate.Value;
				}

				if (parameter.Value is OracleTimeStamp)
				{
					var oracleTimeStamp = (OracleTimeStamp)parameter.Value;
					value = oracleTimeStamp.IsNull ? (DateTime?)null : oracleTimeStamp.Value;
				}

				bindVariableModels[parameter.ParameterName].Value = value;
			}
		}

		public override StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel)
		{
			var executionTask = ExecuteStatementAsync(executionModel, CancellationToken.None);
			executionTask.Wait();
			
			return executionTask.Result;
		}

		public override async Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			PreInitialize();

			try
			{
				_isExecuting = true;
				return await ExecuteUserStatement(executionModel, cancellationToken);
			}
			catch (OracleException exception)
			{
				if (exception.Number.In(OracleErrorCodeEndOfFileOnCommunicationChannel, OracleErrorCodeNotConnectedToOracle))
				{
					OracleSchemaResolver.Unregister(this);

					InitializeUserConnection();

					if (Disconnected != null)
					{
						Disconnected(this, new DatabaseModelConnectionErrorArgs(exception));
					}

					throw;
				}
				else if (exception.Number != OracleErrorCodeUserInvokedCancellation)
				{
					throw;
				}
			}
			finally
			{
				_isExecuting = false;
			}

			return StatementExecutionResult.Empty;
		}

		private async Task<IReadOnlyList<CompilationError>> RetrieveCompilationErrors(StatementBase statement, CancellationToken cancellationToken)
		{
			var compilationErrorUpdater = new CompilationErrorDataProvider(statement, _currentSchema);
			await UpdateModelAsync(cancellationToken, true, compilationErrorUpdater);
			return compilationErrorUpdater.Errors;
		}

		private async Task<string> RetrieveDatabaseOutput(CancellationToken cancellationToken)
		{
			if (!_databaseOutputEnabled)
			{
				return null;
			}

			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = DatabaseCommands.FetchDatabaseOutputCommandText;

				using (var parameter = command.CreateParameter())
				{
					parameter.OracleDbType = OracleDbType.Clob;
					parameter.Direction = System.Data.ParameterDirection.Output;
					command.Parameters.Add(parameter);

					await command.ExecuteNonQueryAsynchronous(cancellationToken);

					var oracleClob = (OracleClob)parameter.Value;
					return oracleClob.IsNull ? String.Empty : oracleClob.Value;
				}
			}
		}

		private void PreInitialize()
		{
			if (_isExecuting)
				throw new InvalidOperationException("Another statement is executing right now. ");

			DisposeCommandAndReader();
		}

		internal static IReadOnlyList<ColumnHeader> GetColumnHeadersFromReader(IDataRecord reader)
		{
			var columnTypes = new ColumnHeader[reader.FieldCount];
			for (var i = 0; i < reader.FieldCount; i++)
			{
				columnTypes[i] =
					new ColumnHeader
					{
						ColumnIndex = i,
						Name = reader.GetName(i),
						DataType = reader.GetFieldType(i),
						DatabaseDataType = reader.GetDataTypeName(i)
					};
			}

			return columnTypes;
		}

		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			return FetchRecordsFromReader(_userDataReader, rowCount, false);
		}

		private static bool CanFetchFromReader(IDataReader reader)
		{
			return reader != null && !reader.IsClosed;
		}

		internal static IEnumerable<object[]> FetchRecordsFromReader(OracleDataReader reader, int rowCount, bool prefetch)
		{
			if (!CanFetchFromReader(reader))
			{
				yield break;
			}

			var fieldTypes = new string[reader.FieldCount];
			for (var i = 0; i < reader.FieldCount; i++)
			{
				var fieldType = reader.GetDataTypeName(i);
				fieldTypes[i] = fieldType;
				//Trace.Write(i + ". " + fieldType + "; ");
			}

			//Trace.WriteLine(String.Empty);

			for (var i = 0; i < rowCount; i++)
			{
				if (!CanFetchFromReader(reader))
				{
					yield break;
				}

				object[] values;

				try
				{
					if (reader.Read())
					{
						values = BuildValueArray(reader, fieldTypes, prefetch);
					}
					else
					{
						reader.Close();
						yield break;
					}
				}
				catch
				{
					if (!reader.IsClosed)
					{
						reader.Close();
					}

					throw;
				}

				yield return values;
			}
		}

		private static object[] BuildValueArray(OracleDataReader reader, IList<string> fieldTypes, bool prefetch)
		{
			var columnData = new object[fieldTypes.Count];

			for (var i = 0; i < fieldTypes.Count; i++)
			{
				var fieldType = fieldTypes[i];
				object value;
				switch (fieldType)
				{
					case "Blob":
						value = new OracleBlobValue(reader.GetOracleBlob(i));
						break;
					case "Clob":
					case "NClob":
						value = new OracleClobValue(fieldType.ToUpperInvariant(), reader.GetOracleClob(i));
						break;
					case "Long":
						var oracleString = reader.GetOracleString(i);
						value = oracleString.IsNull
							? String.Empty
							: String.Format("{0}{1}", oracleString.Value, oracleString.Value.Length == InitialLongFetchSize ? OracleLargeTextValue.Ellipsis : null);
						break;
					case "Raw":
						value = new OracleRawValue(reader.GetOracleBinary(i));
						break;
					case "LongRaw":
						value = new OracleLongRawValue(reader.GetOracleBinary(i));
						break;
					case "TimeStamp":
						value = new OracleTimestamp(reader.GetOracleTimeStamp(i));
						break;
					case "TimeStampTZ":
						value = new OracleTimestampWithTimeZone(reader.GetOracleTimeStampTZ(i));
						break;
					case "TimeStampLTZ":
						value = new OracleTimestampWithLocalTimeZone(reader.GetOracleTimeStampLTZ(i));
						break;
					case "Decimal":
						value = new OracleNumber(reader.GetOracleDecimal(i));
						break;
					#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
					case "XmlType":
						value = new OracleXmlValue(reader.GetOracleXmlType(i));
						break;
					#endif
					case "Object":
					case "Array":
						value = reader.GetOracleValue(i);
						break;
					case "Date":
						var oracleDate = reader.GetOracleDate(i);
						value = oracleDate.IsNull
							? new OracleDateTime()
							: new OracleDateTime(oracleDate.Year, oracleDate.Month, oracleDate.Day, oracleDate.Hour, oracleDate.Minute, oracleDate.Second);
						break;
					case "Char":
					case "NChar":
					case "Varchar":
					case "Varchar2":
					case "NVarchar":
					case "NVarchar2":
						value = new OracleSimpleValue(reader.GetValue(i));
						break;
					default:
						value = reader.GetValue(i);
						break;
				}

				if (prefetch)
				{
					var largeValue = value as ILargeValue;
					if (largeValue != null)
					{
						largeValue.Prefetch();
					}
				}

				columnData[i] = value;
			}

			return columnData;
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

		internal IEnumerable<T> ExecuteReader<T>(Func<Version, string> getCommandTextFunction, Func<OracleDataReader, T> formatFunction)
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

					connection.ModuleName = ModuleNameSqlPadDatabaseModel;
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
								yield return formatFunction(reader);
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
			_timer.Elapsed += TimerElapsedHandler;
			_timer.Start();
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
}
