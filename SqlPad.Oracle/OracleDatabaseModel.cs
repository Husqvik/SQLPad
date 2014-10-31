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
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using SqlPad.Oracle.ToolTips;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		internal const int OracleErrorCodeUserInvokedCancellation = 1013;
		internal const int InitialLongFetchSize = 131072;
		private const string ModuleNameSqlPadDatabaseModel = "SQLPad database model";

		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly Timer _timer = new Timer();
		private bool _isInitialized;
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private bool _isExecuting;
		private Task _backgroundTask;
		private readonly CancellationTokenSource _backgroundTaskCancellationTokenSource = new CancellationTokenSource();
		private ILookup<OracleFunctionIdentifier, OracleFunctionMetadata> _allFunctionMetadata = Enumerable.Empty<OracleFunctionMetadata>().ToLookup(m => m.Identifier);
		private readonly ConnectionStringSettings _connectionString;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private readonly DataDictionaryMapper _dataDictionaryMapper;
		private OracleDataDictionary _dataDictionary = OracleDataDictionary.EmptyDictionary;
		private readonly OracleConnection _userConnection;
		private OracleDataReader _userDataReader;
		private OracleCommand _userCommand;
		private OracleTransaction _userTransaction;
		private int _userSessionId;
		private string _userCommandSqlId;
		private string _userTransactionId;
		private IsolationLevel _userTransactionIsolationLevel;
		private int _userCommandChildNumber;
		private SessionExecutionStatisticsUpdater _executionStatisticsUpdater;
		private string _oracleVersion;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		private static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();
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

			_userConnection = new OracleConnection(connectionString.ConnectionString);
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

		public override ILookup<OracleFunctionIdentifier, OracleFunctionMetadata> AllFunctionMetadata { get { return _allFunctionMetadata; } }

		protected override IDictionary<string, OracleFunctionMetadata> NonSchemaBuiltInFunctionMetadata { get { return _dataDictionary.NonSchemaFunctionMetadata; } }

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

				InitializeSession();
			}
		}

		public override bool HasActiveTransaction { get { return !String.IsNullOrEmpty(_userTransactionId); } }

		public override void CommitTransaction()
		{
			ExecuteUserTransactionAction(t => t.Commit());
		}

		public override void RollbackTransaction()
		{
			ExecuteUserTransactionAction(t => t.Rollback());
		}

		private void ExecuteUserTransactionAction(Action<OracleTransaction> action)
		{
			if (!HasActiveTransaction)
			{
				return;
			}

			action(_userTransaction);

			_userTransactionId = null;
			_userTransactionIsolationLevel = IsolationLevel.Unspecified;

			DisposeUserTransaction();
		}

		private void DisposeUserTransaction()
		{
			_userTransaction.Dispose();
			_userTransaction = null;
		}

		public override ICollection<string> Schemas { get { return _schemas; } }
		
		public override ICollection<string> AllSchemas { get { return _allSchemas; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _dataDictionary.AllObjects; } }

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get { return _dataDictionary.DatabaseLinks; } }

		public override ICollection<string> CharacterSets { get { return _dataDictionary.CharacterSets; } }

		public override IDictionary<int, string> StatisticsKeys { get { return _dataDictionary.StatisticsKeys; } }

		public override IDictionary<string, string> SystemParameters { get { return _dataDictionary.SystemParameters; } }

		public override int VersionMajor { get { return Convert.ToInt32(_oracleVersion.Split('.')[0]); } }
		
		public override string VersionString { get { return _oracleVersion; } }

		public override void RefreshIfNeeded()
		{
			if (IsRefreshNeeded)
			{
				Refresh();
			}
		}

		public override bool IsModelFresh
		{
			get { return !IsRefreshNeeded; }
		}

		private bool IsRefreshNeeded
		{
			get { return _dataDictionary.Timestamp.AddMinutes(ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod) < DateTime.Now; }
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
					return taskCompletionSource.Task.ContinueWith(t => RefreshTaskFinishedHandler(t.Result));
				}

				ActiveDataModelRefresh.Add(CachedConnectionStringName);

				if (_backgroundTask == null)
				{
					ExecuteActionAsync(() => LoadSchemaObjectMetadata(force));
				}
				else
				{
					_backgroundTask = _backgroundTask.ContinueWith(
						t =>
						{
							t.Dispose();
							LoadSchemaObjectMetadata(force);
						});
				}
			}

			return _backgroundTask;
		}

		private void RefreshTaskFinishedHandler(OracleDataDictionary dataDictionary)
		{
			_dataDictionary = dataDictionary;
			BuildAllFunctionMetadata();

			RaiseEvent(RefreshFinished);

			Trace.WriteLine(String.Format("{0} - Metadata for '{1}' has been retrieved from the cache. ", DateTime.Now, CachedConnectionStringName));
		}

		public async override Task<ExplainPlanResult> ExplainPlanAsync(string statement, CancellationToken cancellationToken)
		{
			if (String.IsNullOrEmpty(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name))
			{
				throw new InvalidOperationException("OracleConfiguration/ExecutionPlan/TargetTable[Name] is missing. ");
			}

			var planKey = Convert.ToString(statement.GetHashCode());
			var targetTableIdentifier = OracleObjectIdentifier.Create(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Schema, OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name);
			var explainPlanUpdater = new ExplainPlanUpdater(statement, planKey, targetTableIdentifier);

			try
			{
				_isExecuting = true;
				await UpdateModelAsync(cancellationToken, true, explainPlanUpdater.CreateExplainPlanUpdater, explainPlanUpdater.LoadExplainPlanUpdater);
			}
			finally
			{
				_isExecuting = false;
			}

			return explainPlanUpdater.ExplainPlanResult;
		}

		public override async Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			await UpdateModelAsync(cancellationToken, true, _executionStatisticsUpdater.SessionEndExecutionStatisticsUpdater);
			return _executionStatisticsUpdater.ExecutionStatistics;
		}

		public async override Task<string> GetActualExecutionPlanAsync(CancellationToken cancellationToken)
		{
			var displayCursorUpdater = new DisplayCursorUpdater(_userCommandSqlId, _userCommandChildNumber);
			await UpdateModelAsync(cancellationToken, true, displayCursorUpdater);
			return displayCursorUpdater.PlanText;
		}

		public async override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			var scriptUpdater = new ObjectScriptUpdater(schemaObject);
			await UpdateModelAsync(cancellationToken, false, scriptUpdater);
			return scriptUpdater.ScriptText;
		}

		public async override Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var tableDetailsUpdater = new TableDetailsModelUpdater(dataModel, objectIdentifier);
			var tableSpaceAllocationUpdater = new TableSpaceAllocationModelUpdater(dataModel, objectIdentifier);
			var tableInMemorySpaceAllocationUpdater = new TableInMemorySpaceAllocationModelUpdater(dataModel, objectIdentifier, _oracleVersion);
			await UpdateModelAsync(cancellationToken, true, tableDetailsUpdater, tableSpaceAllocationUpdater, tableInMemorySpaceAllocationUpdater);
		}

		public async override Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var columnDetailsUpdater = new ColumnDetailsModelUpdater(dataModel, objectIdentifier, columnName.Trim('"'));
			var columnHistogramUpdater = new ColumnDetailsHistogramUpdater(dataModel, objectIdentifier, columnName.Trim('"'));
			var columnInMemoryDetailsUpdater = new ColumnInMemoryDetailsModelUpdater(dataModel, objectIdentifier, columnName.Trim('"'), _oracleVersion);
			await UpdateModelAsync(cancellationToken, true, columnDetailsUpdater, columnHistogramUpdater, columnInMemoryDetailsUpdater);
		}

		private async Task UpdateModelAsync(CancellationToken cancellationToken, bool suppressException, params IDataModelUpdater[] updaters)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;

					connection.Open();

					using (var transaction = connection.BeginTransaction())
					{
						SetCurrentSchema(command);

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
									if (updater.HasScalarResult)
									{
										var result = await command.ExecuteScalarAsynchronous(cancellationToken);
										updater.MapScalarData(result);
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
							catch (Exception exception)
							{
								var oracleException = exception as OracleException;
								if (oracleException != null && oracleException.Number == OracleErrorCodeUserInvokedCancellation)
								{
									continue;
								}

								Trace.WriteLine("Update model failed: " + exception);

								if (!suppressException)
								{
									throw;
								}
							}
						}

						transaction.Rollback();
					}
				}
			}
		}

		public override event EventHandler Initialized;
		
		public override event EventHandler<DatabaseModelInitializationFailedArgs> InitializationFailed;
		
		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshFinished;

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

			DisposeCommandAndReaderAndCloseConnection();

			if (_backgroundTask != null)
			{
				if (_isRefreshing)
				{
					_isRefreshing = false;
					RaiseEvent(RefreshFinished);
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
			RefreshFinished = null;
			
			_userConnection.Dispose();

			DatabaseModels.Remove(_connectionString.ConnectionString);
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

		private void DisposeCommandAndReaderAndCloseConnection()
		{
			DisposeCommandAndReader();

			if (_userConnection.State != ConnectionState.Closed)
			{
				_userConnection.Close();
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

		private void InitializeSession()
		{
			using (var command = _userConnection.CreateCommand())
			{
				SetCurrentSchema(command);

				command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') SID FROM SYS.DUAL";
				_userSessionId = Convert.ToInt32(command.ExecuteScalar());
			}
		}

		private void SetCurrentSchema(IDbCommand command)
		{
			command.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = {0}", _currentSchema);
			command.ExecuteNonQuery();
		}

		private bool EnsureUserConnectionOpen()
		{
			if (_userConnection.State == ConnectionState.Open)
			{
				return false;
			}

			_userConnection.Open();

			_userConnection.ModuleName = ModuleNameSqlPadDatabaseModel;
			_userConnection.ActionName = "User query";

			return true;
		}

		private async Task<OracleDataReader> ExecuteUserStatement(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			if (EnsureUserConnectionOpen())
			{
				InitializeSession();
			}

			_userCommand = _userConnection.CreateCommand();
			_userCommand.BindByName = true;

			if (_userTransaction == null)
			{
				_userTransaction = _userConnection.BeginTransaction();
			}

			/*if (executionModel.GatherExecutionStatistics)
			{
				_userCommand.CommandText = "ALTER SESSION SET STATISTICS_LEVEL = ALL";
				_userCommand.ExecuteNonQuery();
			}*/

			_userCommand.CommandText = executionModel.StatementText;
			_userCommand.InitialLONGFetchSize = InitialLongFetchSize;

			foreach (var variable in executionModel.BindVariables)
			{
				_userCommand.AddSimpleParameter(variable.Name, variable.Value, variable.DataType);
			}

			_executionStatisticsUpdater = new SessionExecutionStatisticsUpdater(StatisticsKeys, _userSessionId);

			if (executionModel.GatherExecutionStatistics)
			{
				await UpdateModelAsync(cancellationToken, true, _executionStatisticsUpdater.SessionBeginExecutionStatisticsUpdater);
			}

			var reader = await _userCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);

			if (!ResolveExecutionPlanIdentifiersAndTransactionStatus())
			{
				SafeResolveTransactionStatus();
			}

			UpdateBindVariables(executionModel);

			if (!HasActiveTransaction && _userTransaction != null)
			{
				DisposeUserTransaction();
			}

			return reader;
		}

		private bool ResolveExecutionPlanIdentifiersAndTransactionStatus()
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;
					command.CommandText = DatabaseCommands.GetExecutionPlanIdentifiers;
					command.AddSimpleParameter("SID", _userSessionId);

					try
					{
						connection.Open();

						using (var reader = command.ExecuteReader())
						{
							if (reader.Read())
							{
								_userCommandSqlId = (string)reader["SQL_ID"];
								_userCommandChildNumber = Convert.ToInt32(reader["SQL_CHILD_NUMBER"]);
								_userTransactionId = OracleReaderValueConvert.ToString(reader["TRANSACTION_ID"]);
								_userTransactionIsolationLevel = (IsolationLevel)Convert.ToInt32(reader["TRANSACTION_ISOLATION_LEVEL"]);
							}
							else
							{
								_userCommandSqlId = null;
							}
						}

						return true;
					}
					catch (OracleException e)
					{
						Trace.WriteLine("Execution plan identifers and transaction status could not been fetched: " + e);
						return false;
					}
				}
			}
		}

		private void SafeResolveTransactionStatus()
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = DatabaseCommands.GetLocalTransactionId;
				_userTransactionId = OracleReaderValueConvert.ToString(command.ExecuteScalar());
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
					value = OracleNumber.SetOutputFormat((OracleDecimal)parameter.Value);
				}

				if (parameter.Value is OracleDate)
				{
					value = ((OracleDate)parameter.Value).Value;
				}

				if (parameter.Value is OracleString)
				{
					var oracleString = (OracleString)parameter.Value;
					value = oracleString.IsNull ? String.Empty : oracleString.Value;
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

			var result = new StatementExecutionResult();

			try
			{
				_isExecuting = true;
				_userDataReader = await ExecuteUserStatement(executionModel, cancellationToken);
				result.AffectedRowCount = _userDataReader.RecordsAffected;
				result.ExecutedSucessfully = true;
				result.ColumnHeaders = GetColumnHeadersFromReader(_userDataReader);
			}
			catch (Exception exception)
			{
				var oracleException = exception as OracleException;
				if (oracleException == null || oracleException.Number != OracleErrorCodeUserInvokedCancellation)
				{
					throw;
				}
			}
			finally
			{
				_isExecuting = false;
			}

			return result;
		}

		private void PreInitialize()
		{
			if (_isExecuting)
				throw new InvalidOperationException("Another statement is executing right now. ");

			DisposeCommandAndReader();
		}

		internal static ICollection<ColumnHeader> GetColumnHeadersFromReader(IDataRecord reader)
		{
			var columnTypes = new ColumnHeader[reader.FieldCount];
			for (var i = 0; i < reader.FieldCount; i++)
			{
				var columnHeader = new ColumnHeader
				{
					ColumnIndex = i,
					Name = reader.GetName(i),
					DataType = reader.GetFieldType(i),
					DatabaseDataType = reader.GetDataTypeName(i)
				};

				columnHeader.ValueConverter = new OracleColumnValueConverter(columnHeader);

				columnTypes[i] = columnHeader;
			}

			return columnTypes;
		}

		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			return FetchRecordsFromReader(_userDataReader, rowCount);
		}

		private static bool CanFetchFromReader(IDataReader reader)
		{
			return reader != null && !reader.IsClosed;
		}

		internal static IEnumerable<object[]> FetchRecordsFromReader(OracleDataReader reader, int rowCount)
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
						values = BuildValueArray(reader, fieldTypes);
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

		private static object[] BuildValueArray(OracleDataReader reader, IList<string> fieldTypes)
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
					case "LongRaw":
						value = new OracleLongRawValue(reader, i);
						break;
					case "TimeStamp":
						var oracleTimestamp = new OracleTimestamp(reader, i);
						value = oracleTimestamp.IsNull ? (object)DBNull.Value : oracleTimestamp;
						break;
					case "TimeStampTZ":
						var oracleTimestampWithTimeZone = new OracleTimestampWithTimeZone(reader, i);
						value = oracleTimestampWithTimeZone.IsNull ? (object)DBNull.Value : oracleTimestampWithTimeZone;
						break;
					case "Decimal":
						var oracleDecimal = new OracleNumber(reader, i);
						value = oracleDecimal.IsNull ? (object)DBNull.Value : oracleDecimal;
						break;
					case "XmlType":
						value = new OracleXmlValue(reader.GetOracleXmlType(i));
						break;
					default:
						value = reader.GetValue(i);
						break;
				}

				columnData[i] = value;
			}

			return columnData;
		}

		internal IEnumerable<T> ExecuteReader<T>(string commandText, Func<OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = commandText;
					command.BindByName = true;

					connection.Open();

					if (_oracleVersion == null)
					{
						_oracleVersion = connection.ServerVersion;
					}

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

		private void RaiseRefreshEvents()
		{
			List<RefreshModel> refreshModels;
			if (!WaitingDataModelRefresh.TryGetValue(CachedConnectionStringName, out refreshModels))
				return;

			foreach (var refreshModel in refreshModels)
			{
				refreshModel.DatabaseModel._dataDictionary = _dataDictionary;
				refreshModel.DatabaseModel.BuildAllFunctionMetadata();
				refreshModel.DatabaseModel.RaiseEvent(refreshModel.DatabaseModel.RefreshFinished);
				refreshModel.DatabaseModel.RaiseEvent(refreshModel.DatabaseModel.RefreshStarted);
			}
		}

		private void SetRefreshTaskResults()
		{
			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(CachedConnectionStringName, out refreshModels))
				{
					foreach (var refreshModel in refreshModels)
					{
						refreshModel.TaskCompletionSource.SetResult(_dataDictionary);
					}

					refreshModels.Clear();
				}

				ActiveDataModelRefresh.Remove(CachedConnectionStringName);
			}
		}

		private void LoadSchemaObjectMetadata(bool force)
		{
			TryLoadSchemaObjectMetadataFromCache();

			var isRefreshDone = !IsRefreshNeeded && !force;
			if (isRefreshDone)
			{
				SetRefreshTaskResults();
				return;
			}

			RaiseRefreshEvents();

			var reason = force ? "has been forced to refresh" : (_dataDictionary.Timestamp > DateTime.MinValue ? "has expired" : "does not exist or is corrupted");
			Trace.WriteLine(String.Format("{0} - Cache for '{1}' {2}. Cache refresh started. ", DateTime.Now, CachedConnectionStringName, reason));

			RaiseEvent(RefreshStarted);
			_isRefreshing = true;

			var isRefreshSuccessful = RefreshSchemaObjectMetadata();

			SetRefreshTaskResults();

			if (isRefreshSuccessful)
			{
				MetadataCache.StoreDatabaseModelCache(CachedConnectionStringName, stream => _dataDictionary.Serialize(stream));
			}

			_isRefreshing = false;
			RaiseEvent(RefreshFinished);
		}

		private bool RefreshSchemaObjectMetadata()
		{
			var lastRefresh = DateTime.Now;

			try
			{
				var allObjects = _dataDictionaryMapper.BuildDataDictionary();

				var userFunctions = _dataDictionaryMapper.GetUserFunctionMetadata().SelectMany(g => g);
				var builtInFunctions = _dataDictionaryMapper.GetBuiltInFunctionMetadata().SelectMany(g => g);
				_allFunctionMetadata = builtInFunctions.Concat(userFunctions)
					.ToLookup(m => m.Identifier);

				var nonSchemaBuiltInFunctionMetadata = new Dictionary<string, OracleFunctionMetadata>();

				foreach (var functionMetadata in _allFunctionMetadata.SelectMany(g => g))
				{
					if (String.IsNullOrEmpty(functionMetadata.Identifier.Owner))
					{
						nonSchemaBuiltInFunctionMetadata.Add(functionMetadata.Identifier.Name, functionMetadata);
						continue;
					}

					if (functionMetadata.IsPackageFunction)
					{
						OracleSchemaObject packageObject;
						if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Package), out packageObject))
						{
							var package = (OraclePackage)packageObject;
							package.Functions.Add(functionMetadata);
							functionMetadata.Owner = package;
						}
					}
					else
					{
						OracleSchemaObject functionObject;
						if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Name), out functionObject))
						{
							var function = (OracleFunction)functionObject;
							function.Metadata = functionMetadata;
							functionMetadata.Owner = function;
						}
					}
				}

				var databaseLinks = _dataDictionaryMapper.GetDatabaseLinks();
				var characterSets = _dataDictionaryMapper.GetCharacterSets();
				var statisticsKeys = SafeFetchDictionary(_dataDictionaryMapper.GetStatisticsKeys, "DataDictionaryMapper.GetStatisticsKeys failed: ");
				var systemParameters = SafeFetchDictionary(_dataDictionaryMapper.GetSystemParameters, "DataDictionaryMapper.GetSystemParameters failed: ");

				_dataDictionary = new OracleDataDictionary(allObjects, databaseLinks, nonSchemaBuiltInFunctionMetadata, characterSets, statisticsKeys, systemParameters, lastRefresh);

				CachedDataDictionaries[CachedConnectionStringName] = _dataDictionary;
				return true;
			}
			catch(Exception e)
			{
				Trace.WriteLine("Oracle data dictionary refresh failed: " + e);
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
			}
			else if (MetadataCache.TryLoadDatabaseModelCache(CachedConnectionStringName, out stream))
			{
				try
				{
					RaiseEvent(RefreshStarted);
					var stopwatch = Stopwatch.StartNew();
					_dataDictionary = CachedDataDictionaries[CachedConnectionStringName] = OracleDataDictionary.Deserialize(stream);
					Trace.WriteLine(String.Format("{0} - Cache for '{1}' loaded in {2}", DateTime.Now, CachedConnectionStringName, stopwatch.Elapsed));
				}
				catch (Exception e)
				{
					Trace.WriteLine("Oracle data dictionary cache deserialization failed: " + e);
				}
				finally
				{
					stream.Dispose();
					RaiseEvent(RefreshFinished);
				}
			}

			BuildAllFunctionMetadata();

			_cacheLoaded = true;
		}

		private void BuildAllFunctionMetadata()
		{
			var functionMetadata = _dataDictionary.AllObjects.Values
				.OfType<IFunctionCollection>()
				.SelectMany(o => o.Functions)
				//.Where(m => m != null) // Enable this line if NullReferenceException occures. At some Oracle environments can happen that function metadata is not available for schema functions with reasonable explanation.
				.Concat(_dataDictionary.NonSchemaFunctionMetadata.Values);

			_allFunctionMetadata = functionMetadata.ToLookup(m => m.Identifier);
		}

		private void RaiseEvent(EventHandler eventHandler)
		{
			if (eventHandler != null)
			{
				eventHandler(this, EventArgs.Empty);
			}
		}

		public override void Initialize()
		{
			Task.Factory.StartNew(InitializeInternal);
		}

		private void InitializeInternal()
		{
			try
			{
				LoadSchemaNames();
			}
			catch(Exception e)
			{
				Trace.WriteLine("Database model initialization failed: " + e);

				if (InitializationFailed != null)
				{
					InitializationFailed(this, new DatabaseModelInitializationFailedArgs(e));
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
			_schemas = new HashSet<string>(_dataDictionaryMapper.GetSchemaNames());
			_allSchemas = new HashSet<string>(_schemas.Select(DataDictionaryMapper.QualifyStringObject)) { SchemaPublic };
		}

		private struct RefreshModel
		{
			public TaskCompletionSource<OracleDataDictionary> TaskCompletionSource { get; set; }
			
			public OracleDatabaseModel DatabaseModel { get; set; }
		}
	}
}
