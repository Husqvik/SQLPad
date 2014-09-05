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
using SqlPad.Oracle.ToolTips;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		internal const int OracleErrorCodeUserInvokedCancellation = 1013;
		internal const int InitialLongFetchSize = 131072;

		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly Timer _timer = new Timer();
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
		private int _userSessionId;
		private SessionExecutionStatisticsUpdater _executionStatisticsUpdater;

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

			LoadSchemaNames();

			SetRefreshTimerInterval();
			_timer.Elapsed += TimerElapsedHandler;
			_timer.Start();
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

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set { _currentSchema = value; }
		}

		public override ICollection<string> Schemas { get { return _schemas; } }
		
		public override ICollection<string> AllSchemas { get { return _allSchemas; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _dataDictionary.AllObjects; } }

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get { return _dataDictionary.DatabaseLinks; } }

		public override ICollection<string> CharacterSets { get { return _dataDictionary.CharacterSets; } }

		public override IDictionary<int, string> StatisticsKeys { get { return _dataDictionary.StatisticsKeys; } }

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

			Trace.WriteLine(String.Format("{0} - Cache for '{1}' has been retrieved from the cache. ", DateTime.Now, CachedConnectionStringName));
		}

		public async override Task<StatementExecutionModel> ExplainPlanAsync(string statement, CancellationToken cancellationToken)
		{
			if (String.IsNullOrEmpty(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name))
			{
				throw new InvalidOperationException("OracleConfiguration/ExecutionPlan/TargetTable[Name] is missing. ");
			}

			var planKey = Convert.ToString(statement.GetHashCode());
			var targetTableIdentifier = OracleObjectIdentifier.Create(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Schema, OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name);
			var explainPlanUpdater = new ExplainPlanUpdater(statement, planKey, targetTableIdentifier);
			await UpdateModelAsync(cancellationToken, true, explainPlanUpdater);
			return
				new StatementExecutionModel
				{
					StatementText = String.Format(DatabaseCommands.ExplainPlanBase, targetTableIdentifier),
					BindVariables = new[] { new BindVariableModel(new BindVariableConfiguration { DataType = "Varchar2", Name = "STATEMENT_ID", Value = planKey }) }
				};
		}

		public override async Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			await UpdateModelAsync(cancellationToken, true, _executionStatisticsUpdater.SessionEndExecutionStatisticsUpdater);
			return _executionStatisticsUpdater.ExecutionStatistics;
		}

		public async override Task<string> GetActualExecutionPlanAsync(CancellationToken cancellationToken)
		{
			var displayCursorUpdater = new DisplayCursorUpdater(_userSessionId);
			await UpdateModelAsync(cancellationToken, true, displayCursorUpdater.ActiveCommandIdentifierUpdater, displayCursorUpdater.DisplayCursorOutputUpdater);
			return displayCursorUpdater.PlanText;
		}

		public async override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			var scriptUpdater = new ObjectScriptUpdater(schemaObject);
			await UpdateModelAsync(cancellationToken, true, scriptUpdater);
			return scriptUpdater.ScriptText;
		}

		public async override Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var tableDetailsUpdater = new TableDetailsModelUpdater(dataModel, objectIdentifier);
			var tableSpaceAllocationUpdater = new TableSpaceAllocationModelUpdater(dataModel, objectIdentifier);
			await UpdateModelAsync(cancellationToken, true, tableDetailsUpdater, tableSpaceAllocationUpdater);
		}

		public async override Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var columnDetailsUpdater = new ColumnDetailsModelUpdater(dataModel, objectIdentifier, columnName.Trim('"'));
			var columnHistogramUpdater = new ColumnDetailsHistogramUpdater(dataModel, objectIdentifier, columnName.Trim('"'));
			await UpdateModelAsync(cancellationToken, true, columnDetailsUpdater, columnHistogramUpdater);
		}

		private async Task UpdateModelAsync(CancellationToken cancellationToken, bool suppressException, params IDataModelUpdater[] updaters)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;

					connection.Open();

					foreach (var updater in updaters)
					{
						command.Parameters.Clear();
						command.CommandText = String.Empty;
						command.CommandType = CommandType.Text;
						updater.InitializeCommand(command);

						try
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

							if (!updater.CanContinue)
							{
								break;
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
				}
			}
		}

		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshFinished;

		public override bool IsExecuting { get { return _isExecuting; } }

		public override bool CanFetch
		{
			get { return _userDataReader != null && !_userDataReader.IsClosed; }
		}

		public override void Dispose()
		{
			_timer.Stop();
			_timer.Dispose();

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

			RefreshStarted = null;
			RefreshFinished = null;
			
			_userConnection.Dispose();

			DatabaseModels.Remove(_connectionString.ConnectionString);
		}

		private void DisposeCommandAndReaderAndCloseConnection()
		{
			if (_userDataReader != null)
			{
				_userDataReader.Dispose();
			}

			if (_userCommand != null)
			{
				_userCommand.Dispose();
			}

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

		private async Task<OracleDataReader> ExecuteUserStatement(StatementExecutionModel executionModel, CancellationToken cancellationToken, bool closeConnection = false)
		{
			_userCommand = _userConnection.CreateCommand();
			_userCommand.BindByName = true;
			_userCommand.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = {0}", _currentSchema);

			try
			{
				_isExecuting = true;
				_userConnection.Open();

				_userConnection.ModuleName = "SQLPad database model";
				_userConnection.ActionName = "User query";

				_userCommand.ExecuteNonQuery();

				_userCommand.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') SID FROM SYS.DUAL";
				_userSessionId = Convert.ToInt32(_userCommand.ExecuteScalar());

				_userCommand.CommandText = executionModel.StatementText;
				_userCommand.InitialLONGFetchSize = InitialLongFetchSize;

				if (executionModel.BindVariables != null)
				{
					foreach (var variable in executionModel.BindVariables)
					{
						_userCommand.AddSimpleParameter(variable.Name, variable.Value, variable.DataType);
					}
				}

				_executionStatisticsUpdater = new SessionExecutionStatisticsUpdater(StatisticsKeys, _userSessionId);

				if (executionModel.GatherExecutionStatistics)
				{
					await UpdateModelAsync(cancellationToken, true, _executionStatisticsUpdater.SessionBeginExecutionStatisticsUpdater);
				}

				return await _userCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);
			}
			finally
			{
				if (closeConnection && _userConnection.State != ConnectionState.Closed)
				{
					_userConnection.Close();
				}
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
				_userDataReader = await ExecuteUserStatement(executionModel, cancellationToken);
				result.AffectedRowCount = _userDataReader.RecordsAffected;
				result.ExecutedSucessfully = true;
			}
			catch (Exception exception)
			{
				SafeCloseUserConnection();

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

		private void SafeCloseUserConnection()
		{
			try
			{
				if (_userConnection.State != ConnectionState.Closed)
				{
					_userConnection.Close();
				}
			}
			catch (Exception e)
			{
				Trace.WriteLine("Connection closing failed: " + e);
			}
		}

		private void PreInitialize()
		{
			if (_isExecuting)
				throw new InvalidOperationException("Another statement is executing right now. ");

			DisposeCommandAndReaderAndCloseConnection();
		}

		public override ICollection<ColumnHeader> GetColumnHeaders()
		{
			CheckCanFetch();

			var columnTypes = new ColumnHeader[_userDataReader.FieldCount];
			for (var i = 0; i < _userDataReader.FieldCount; i++)
			{
				var columnHeader = new ColumnHeader
				{
					ColumnIndex = i,
					Name = _userDataReader.GetName(i),
					DataType = _userDataReader.GetFieldType(i),
					DatabaseDataType = _userDataReader.GetDataTypeName(i)
				};

				columnHeader.ValueConverter = new OracleColumnValueConverter(columnHeader);
				
				columnTypes[i] = columnHeader;
			}

			return columnTypes;
		}

		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			CheckCanFetch();

			var fieldTypes = new string[_userDataReader.FieldCount];
			for (var i = 0; i < _userDataReader.FieldCount; i++)
			{
				var fieldType = _userDataReader.GetDataTypeName(i);
				fieldTypes[i] = fieldType;
				Trace.Write(i + ". " + fieldType + "; ");
			}

			Trace.WriteLine(String.Empty);

			for (var i = 0; i < rowCount; i++)
			{
				if (_userDataReader.Read())
				{
					yield return BuildValueArray(fieldTypes);
				}
				else
				{
					_userDataReader.Close();
					break;
				}
			}
		}

		private object[] BuildValueArray(IList<string> fieldTypes)
		{
			var columnData = new object[fieldTypes.Count];

			for (var i = 0; i < fieldTypes.Count; i++)
			{
				var fieldType = fieldTypes[i];
				object value;
				switch (fieldType)
				{
					case "Blob":
						value = new OracleBlobValue(_userDataReader.GetOracleBlob(i));
						break;
					case "Clob":
					case "NClob":
						value = new OracleClobValue(fieldType.ToUpperInvariant(), _userDataReader.GetOracleClob(i));
						break;
					case "Long":
						var oracleString = _userDataReader.GetOracleString(i);
						value = oracleString.IsNull
							? String.Empty
							: String.Format("{0}{1}", oracleString.Value, oracleString.Value.Length == InitialLongFetchSize ? OracleClobValue.Ellipsis : null);
						break;
					case "LongRaw":
						value = new OracleLongRawValue(_userDataReader, i);
						break;
					default:
						value = _userDataReader.GetValue(i);
						break;
				}

				columnData[i] = value;
			}

			return columnData;
		}

		private void CheckCanFetch()
		{
			if (_userDataReader == null || _userDataReader.IsClosed)
				throw new InvalidOperationException("No data reader available. ");
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

					connection.ModuleName = "SQLPad database model";
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
			var lastRefresh = DateTime.Now;
			_isRefreshing = true;

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
						((OraclePackage)packageObject).Functions.Add(functionMetadata);
					}
				}
				else
				{
					OracleSchemaObject functionObject;
					if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Name), out functionObject))
					{
						((OracleFunction)functionObject).Metadata = functionMetadata;
					}
				}
			}

			var databaseLinks = _dataDictionaryMapper.GetDatabaseLinks();
			var characterSets = _dataDictionaryMapper.GetCharacterSets();
			var statisticsKeys = _dataDictionaryMapper.GetStatisticsKeys().ToDictionary(k => k.Key, k => k.Value);

			_dataDictionary = new OracleDataDictionary(allObjects, databaseLinks, nonSchemaBuiltInFunctionMetadata, characterSets, statisticsKeys, lastRefresh);
			CachedDataDictionaries[CachedConnectionStringName] = _dataDictionary;

			SetRefreshTaskResults();

			MetadataCache.StoreDatabaseModelCache(CachedConnectionStringName, stream => _dataDictionary.Serialize(stream));

			_isRefreshing = false;
			RaiseEvent(RefreshFinished);
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
