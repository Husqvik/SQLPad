using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleConnectionAdapter : OracleConnectionAdapterBase
	{
		private const string ModuleNameSqlPadDatabaseModelBase = "Database model";
		internal static readonly ResultInfo MainResultInfo = new ResultInfo("MainResultIdentifier", ResultIdentifierType.SystemGenerated);

		private static int _counter;

		private readonly ConnectionStringSettings _connectionString;
		private readonly List<OracleTraceEvent> _activeTraceEvents = new List<OracleTraceEvent>();
		private readonly Dictionary<ResultInfo, OracleDataReader> _dataReaders = new Dictionary<ResultInfo, OracleDataReader>();
		private readonly Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>> _resultInfoColumnHeaders = new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>();

		private bool _isExecuting;
		private bool _databaseOutputEnabled;
		private string _moduleName;
		private string _currentSchema;
		private string _identifier;
		private OracleConnection _userConnection;
		private string _userCommandSqlId;
		private int _userCommandChildNumber;
		private OracleTransaction _userTransaction;
		private int? _userSessionId;
		private string _userTransactionId;
		private string _userTraceFileName = String.Empty;
		private IsolationLevel _userTransactionIsolationLevel;
		private bool _userCommandHasCompilationErrors;
		private SessionExecutionStatisticsDataProvider _executionStatisticsDataProvider;
		private readonly OracleDatabaseModel _databaseModel;

		public override string Identifier
		{
			get { return _identifier; }
			set { UpdateModuleName(value); }
		}

		public override IDatabaseModel DatabaseModel => _databaseModel;

		public override string TraceFileName => _userTraceFileName;

		public override int? SessionId => _userSessionId;

		public OracleConnectionAdapter(OracleDatabaseModel databaseModel)
		{
			_databaseModel = databaseModel;
			_connectionString = _databaseModel.ConnectionString;

			var identifier = Convert.ToString(Interlocked.Increment(ref _counter));
			UpdateModuleName(identifier);

			InitializeUserConnection();

			SwitchCurrentSchema();
		}

		public override async Task ActivateTraceEvents(IEnumerable<OracleTraceEvent> traceEvents, string traceIdentifier, CancellationToken cancellationToken)
		{
			lock (_activeTraceEvents)
			{
				if (_activeTraceEvents.Count > 0)
				{
					throw new InvalidOperationException("Connection has active trace events already. ");
				}

				_activeTraceEvents.AddRange(traceEvents);

				if (_activeTraceEvents.Count == 0)
				{
					return;
				}
			}

			var preface =  $"EXECUTE IMMEDIATE 'ALTER SESSION SET TRACEFILE_IDENTIFIER = \"{traceIdentifier}\"';";
			var commandText = BuildTraceEventActionStatement(_activeTraceEvents, () => preface, e => e.CommandTextEnable);

			try
			{
				await ExecuteSimpleCommandUsingUserConnection(commandText, cancellationToken);
				Trace.WriteLine($"Enable trace event statement executed successfully: \n{commandText}");

				await RetrieveTraceFileName(cancellationToken);
			}
			catch
			{
				lock (_activeTraceEvents)
				{
					_activeTraceEvents.Clear();
				}
				
				throw;
			}
		}

		private async Task ExecuteSimpleCommandUsingUserConnection(string commandText, CancellationToken cancellationToken)
		{
			await EnsureUserConnectionOpen(cancellationToken);

			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = commandText;
				await command.ExecuteNonQueryAsynchronous(cancellationToken);
			}
		}

		private static string BuildTraceEventActionStatement(IEnumerable<OracleTraceEvent> traceEvents, Func<string> prefaceFunction, Func<OracleTraceEvent, string> getCommandTextFunction)
		{
			var builder = new StringBuilder("BEGIN\n");

			var preface = prefaceFunction?.Invoke();
			if (!String.IsNullOrEmpty(preface))
			{
				builder.Append("\t");
				builder.AppendLine(preface);
			}

			foreach (var traceEvent in traceEvents)
			{
				builder.AppendLine($"\tEXECUTE IMMEDIATE '{getCommandTextFunction(traceEvent).Replace("'", "''")}';");
			}

			builder.Append("END;");

			return builder.ToString();
		}

		public override async Task StopTraceEvents(CancellationToken cancellationToken)
		{
			string commandText;
			lock (_activeTraceEvents)
			{
				if (_activeTraceEvents.Count == 0)
				{
					return;
				}

				commandText = BuildTraceEventActionStatement(_activeTraceEvents, null, e => e.CommandTextDisable);
				_activeTraceEvents.Clear();
			}

			await ExecuteSimpleCommandUsingUserConnection(commandText, cancellationToken);
			Trace.WriteLine($"Disable trace event statement executed successfully: \n{commandText}");
		}

		internal void SwitchCurrentSchema()
		{
			if (_currentSchema == _databaseModel.CurrentSchema)
			{
				return;
			}

			if (_userConnection.State != ConnectionState.Closed)
			{
				InitializeUserConnection();
			}

			_currentSchema = _databaseModel.CurrentSchema;
		}

		private void UpdateModuleName(string identifier)
		{
			_identifier = identifier;
			_moduleName = $"{ModuleNameSqlPadDatabaseModelBase}/{_databaseModel.ConnectionIdentifier}/{_identifier}";
		}

		private void InitializeUserConnection()
		{
			DisposeUserConnection();

			_databaseOutputEnabled = false;

			_userConnection = new OracleConnection(_connectionString.ConnectionString);
			_userConnection.InfoMessage += UserConnectionInfoMessageHandler;
		}

		private void UserConnectionInfoMessageHandler(object sender, OracleInfoMessageEventArgs args)
		{
			var containsCompilationError = args.Errors.Cast<OracleError>().Any(e => e.Number == (int)OracleErrorCode.SuccessWithCompilationError);
			if (containsCompilationError)
			{
				_userCommandHasCompilationErrors = true;
			}
			else
			{
				Trace.WriteLine(args.ToString());
			}
		}

		public override void Dispose()
		{
			DisposeUserTransaction();

			DisposeDataReaders();

			DisposeUserConnection();

			_databaseModel.RemoveConnectionAdapter(this);
		}

		public override bool CanFetch(ResultInfo resultInfo)
		{
			OracleDataReader reader;
			return !_isExecuting && _dataReaders.TryGetValue(resultInfo, out reader) && CanFetchFromReader(reader);
		}

		public override bool IsExecuting => _isExecuting;

		public override bool EnableDatabaseOutput { get; set; }

		public override async Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			await _databaseModel.UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionEndExecutionStatisticsDataProvider);
			return _executionStatisticsDataProvider.ExecutionStatistics;
		}

		public override Task<IReadOnlyList<object[]>> FetchRecordsAsync(ResultInfo resultInfo, int rowCount, CancellationToken cancellationToken)
		{
			OracleDataReader reader;
			if (_dataReaders.TryGetValue(resultInfo, out reader))
			{
				return FetchRecordsFromReader(reader, rowCount, false).EnumerateAsync(cancellationToken);
			}

			throw new ArgumentException($"Result '{resultInfo.ResultIdentifier}' ({resultInfo.Type}) does not exist. ");
		}

		public override bool HasActiveTransaction => !String.IsNullOrEmpty(_userTransactionId);

	    public override Task CommitTransaction()
		{
			return ExecuteUserTransactionAction(async t => await t.CommitAsynchronous());
		}

		public override Task RollbackTransaction()
		{
			return ExecuteUserTransactionAction(async t => await t.RollbackAsynchronous());
		}

		public async override Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			var cursorExecutionStatisticsDataProvider = new CursorExecutionStatisticsDataProvider(_userCommandSqlId, _userCommandChildNumber);
			var displayCursorDataProvider = new DisplayCursorDataProvider(_userCommandSqlId, _userCommandChildNumber);
			await _databaseModel.UpdateModelAsync(cancellationToken, false, cursorExecutionStatisticsDataProvider, displayCursorDataProvider);
			cursorExecutionStatisticsDataProvider.ItemCollection.PlanText = displayCursorDataProvider.PlanText;
			return cursorExecutionStatisticsDataProvider.ItemCollection;
		}

		public override Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			PreInitialize();

			return ExecuteUserStatementAsync(executionModel, true, cancellationToken);
		}

		public override Task<StatementExecutionResult> ExecuteChildStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			return ExecuteUserStatementAsync(executionModel, false, cancellationToken);
		}

		private void PreInitialize()
		{
			if (_isExecuting)
			{
				throw new InvalidOperationException("Another statement is executing right now. ");
			}

			DisposeDataReaders();
		}

		private async Task ExecuteUserTransactionAction(Func<OracleTransaction, Task> action)
		{
			if (!HasActiveTransaction)
			{
				return;
			}

			try
			{
				_isExecuting = true;
				await action(_userTransaction);
			}
			finally
			{
				_isExecuting = false;
			}

			DisposeUserTransaction();
		}

		private void DisposeUserTransaction()
		{
			_userTransactionId = null;
			_userTransactionIsolationLevel = IsolationLevel.Unspecified;

			if (_userTransaction != null)
			{
				_userTransaction.Dispose();
				_userTransaction = null;
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

		private void DisposeDataReaders()
		{
			_resultInfoColumnHeaders.Clear();
			_dataReaders.Values.ForEach(r => r.Dispose());
			_dataReaders.Clear();
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
			}

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
						var stringValue = oracleString.IsNull
							? String.Empty
							: $"{oracleString.Value}{(oracleString.Value.Length == OracleDatabaseModel.InitialLongFetchSize ? OracleLargeTextValue.Ellipsis : null)}";

						value = new OracleSimpleValue(stringValue);
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
							: new OracleDateTime(oracleDate);
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
				    largeValue?.Prefetch();
				}

				columnData[i] = value;
			}

			return columnData;
		}

		private async Task InitializeSession(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				await command.SetSchema(_currentSchema, cancellationToken);

				command.CommandText = OracleDatabaseCommands.SelectCurrentSessionId;
				_userSessionId = Convert.ToInt32(await command.ExecuteScalarAsynchronous(cancellationToken));

				var startupScript = OracleConfiguration.Configuration.StartupScript;
				if (String.IsNullOrWhiteSpace(startupScript))
				{
					return;
				}

				var statements = await OracleSqlParser.Instance.ParseAsync(startupScript, cancellationToken);
				foreach (var statement in statements)
				{
					command.CommandText = statement.RootNode.GetText(startupScript);

					try
					{
						await command.ExecuteNonQueryAsynchronous(cancellationToken);
						Trace.WriteLine($"Startup script command '{command.CommandText}' executed successfully. ");
					}
					catch (Exception e)
					{
						Trace.WriteLine($"Startup script command '{command.CommandText}' failed: {e}");
					}
				}

				await RetrieveTraceFileName(cancellationToken);
			}
		}

		private async Task RetrieveTraceFileName(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = OracleDatabaseCommands.SelectTraceFileFullName;

				try
				{
					_userTraceFileName = (string)await command.ExecuteScalarAsynchronous(cancellationToken);
					Trace.WriteLine($"Session ID {_userSessionId.Value} trace file name is {_userTraceFileName}. ");
				}
				catch (Exception e)
				{
					Trace.WriteLine($"Trace file name retrieval failed: {e}");
				}
			}
		}

		private async Task EnsureUserConnectionOpen(CancellationToken cancellationToken)
		{
			var isConnectionStateChanged = await _userConnection.EnsureConnectionOpen(cancellationToken);
			if (isConnectionStateChanged)
			{
				_userConnection.ModuleName = _moduleName;
				_userConnection.ActionName = "User query";

				await InitializeSession(cancellationToken);
			}
		}

		private async Task EnsureDatabaseOutput(CancellationToken cancellationToken)
		{
			if ((EnableDatabaseOutput && _databaseOutputEnabled) ||
				!EnableDatabaseOutput && !_databaseOutputEnabled)
			{
				return;
			}

			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = $"CALL DBMS_OUTPUT.{(EnableDatabaseOutput ? "ENABLE(1)" : "DISABLE()")}";
				await command.ExecuteNonQueryAsynchronous(cancellationToken);
			}
			
			_databaseOutputEnabled = EnableDatabaseOutput;
		}

		private static void SetOracleGlobalization()
		{
			var info = OracleGlobalization.GetClientInfo();
			var numberFormat = CultureInfo.CurrentCulture.NumberFormat;
			var decimalSeparator = numberFormat.NumberDecimalSeparator;
			var groupSeparator = numberFormat.NumberGroupSeparator;
			info.NumericCharacters = $"{(decimalSeparator.Length == 1 ? decimalSeparator : ".")}{(groupSeparator.Length == 1 ? groupSeparator : " ")}";
			OracleGlobalization.SetThreadInfo(info);
		}

		private async Task<StatementExecutionResult> ExecuteUserStatementAsync(StatementExecutionModel executionModel, bool isUserStatement, CancellationToken cancellationToken)
		{
			_isExecuting = true;
			
			_userCommandHasCompilationErrors = false;

			try
			{
				SetOracleGlobalization();

				await EnsureUserConnectionOpen(cancellationToken);

				await EnsureDatabaseOutput(cancellationToken);

				using (var userCommand = _userConnection.CreateCommand())
				{
					userCommand.BindByName = true;
					userCommand.AddToStatementCache = false;

					if (_userTransaction == null)
					{
						_userTransaction = _userConnection.BeginTransaction();
					}

					userCommand.CommandText = executionModel.StatementText.Replace("\r\n", "\n");
					userCommand.InitialLONGFetchSize = OracleDatabaseModel.InitialLongFetchSize;

					foreach (var variable in executionModel.BindVariables)
					{
						userCommand.AddSimpleParameter(variable.Name, variable.Value, variable.DataType);
					}

					if (executionModel.GatherExecutionStatistics)
					{
						_executionStatisticsDataProvider = new SessionExecutionStatisticsDataProvider(_databaseModel.StatisticsKeys, _userSessionId.Value);
						await _databaseModel.UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionBeginExecutionStatisticsDataProvider);
					}

					int recordsAffected;
					var isPlSql = ((OracleStatement)executionModel.Statement)?.IsPlSql ?? false;
					if (!executionModel.IsPartialStatement && isPlSql)
					{
						//Task.Factory.StartNew(() => TestDebug(userCommand, cancellationToken), cancellationToken).Wait(cancellationToken);
						recordsAffected = await userCommand.ExecuteNonQueryAsynchronous(cancellationToken);
						AcquireImplicitRefCursors(userCommand);
					}
					else
					{
						var dataReader = await userCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);
						recordsAffected = dataReader.RecordsAffected;
						var resultInfo = new ResultInfo(isUserStatement ? MainResultInfo.ResultIdentifier : $"ReferenceConstrantResult{dataReader.GetHashCode()}", ResultIdentifierType.SystemGenerated);
						var columnHeaders = GetColumnHeadersFromReader(dataReader);
						if (columnHeaders.Count > 0)
						{
							_dataReaders.Add(resultInfo, dataReader);
							_resultInfoColumnHeaders.Add(resultInfo, columnHeaders);
						}
					}

					var exception = await ResolveExecutionPlanIdentifiersAndTransactionStatus(cancellationToken);
					if (exception != null)
					{
						await SafeResolveTransactionStatus(cancellationToken);
					}

					UpdateBindVariables(executionModel, userCommand);

					return
						new StatementExecutionResult
						{
							Statement = executionModel,
							AffectedRowCount = recordsAffected,
							DatabaseOutput = await RetrieveDatabaseOutput(cancellationToken),
							ExecutedSuccessfully = true,
							CompilationErrors = _userCommandHasCompilationErrors ? await RetrieveCompilationErrors(executionModel.ValidationModel.Statement, cancellationToken) : new CompilationError[0],
							ResultInfoColumnHeaders = new ReadOnlyDictionary<ResultInfo, IReadOnlyList<ColumnHeader>>(_resultInfoColumnHeaders)
						};
				}
			}
			catch (OracleException exception)
			{
				var errorCode = (OracleErrorCode)exception.Number;
				if (errorCode.In(OracleErrorCode.EndOfFileOnCommunicationChannel, OracleErrorCode.NotConnectedToOracle))
				{
					InitializeUserConnection();

					_userTraceFileName = String.Empty;

					lock (_activeTraceEvents)
					{
						_activeTraceEvents.Clear();
					}

					_databaseModel.Disconnect(exception);

					throw;
				}
				else if (errorCode != OracleErrorCode.UserInvokedCancellation)
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

		private void TestDebug(OracleCommand userCommand, CancellationToken cancellationToken)
		{
			var debuggedAction = new Task(async () =>
			{
				var dataReader = await userCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);
			});

			using (var debuggerSession = new OracleDebuggerSession(_userConnection, debuggedAction))
			{
				debuggerSession.Start(cancellationToken).Wait(cancellationToken);

				do
				{
					debuggerSession.StepInto(cancellationToken).Wait(cancellationToken);
				} while (debuggerSession.RuntimeInfo.IsTerminated != true);

				/*var isRunning = debuggerSession.IsRunning(cancellationToken);
				isRunning.Wait(cancellationToken);
				Trace.WriteLine("Is running:" + isRunning.Result);*/

				debuggerSession.Detach(cancellationToken).Wait(cancellationToken);
			}
		}

		private void UpdateBindVariables(StatementExecutionModel executionModel, OracleCommand userCommand)
		{
			var bindVariableModels = executionModel.BindVariables.ToDictionary(v => v.Name, v => v);
			foreach (OracleParameter parameter in userCommand.Parameters)
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

				var refCursor = parameter.Value as OracleRefCursor;
				if (refCursor != null)
				{
					AcquireRefCursor(refCursor, parameter.ParameterName);
				}
				else
				{
					bindVariableModels[parameter.ParameterName].Value = value;
				}
			}
		}

		private void AcquireImplicitRefCursors(OracleCommand userCommand)
		{
			if (userCommand.ImplicitRefCursors == null)
			{
				return;
			}

			for (var i = 0; i < userCommand.ImplicitRefCursors.Length; i++)
			{
				AcquireRefCursor(userCommand.ImplicitRefCursors[i], $"Implicit cursor {i + 1}");
			}
		}

		private void AcquireRefCursor(OracleRefCursor refCursor, string cursorName)
		{
			var reader = refCursor.GetDataReader();
			var resultInfo = new ResultInfo(cursorName, ResultIdentifierType.UserDefined);
			
			// TODO: Remove when multiple results fully supported
			if (_dataReaders.Count == 0)
			{
				resultInfo = MainResultInfo;
			}

			_dataReaders.Add(resultInfo, reader);
			_resultInfoColumnHeaders.Add(resultInfo, GetColumnHeadersFromReader(reader));
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

		private async Task<string> RetrieveDatabaseOutput(CancellationToken cancellationToken)
		{
			if (!_databaseOutputEnabled)
			{
				return null;
			}

			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = OracleDatabaseCommands.FetchDatabaseOutputCommandText;

				using (var parameter = command.CreateParameter())
				{
					parameter.OracleDbType = OracleDbType.Clob;
					parameter.Direction = ParameterDirection.Output;
					command.Parameters.Add(parameter);

					await command.ExecuteNonQueryAsynchronous(cancellationToken);

					var oracleClob = (OracleClob)parameter.Value;
					return oracleClob.IsNull ? String.Empty : oracleClob.Value;
				}
			}
		}

		private async Task<Exception> ResolveExecutionPlanIdentifiersAndTransactionStatus(CancellationToken cancellationToken)
		{
			using (var connection = new OracleConnection(_connectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;
					command.CommandText = OracleDatabaseCommands.SelectExecutionPlanIdentifiersCommandText;
					command.AddSimpleParameter("SID", _userSessionId.Value);

					try
					{
						connection.Open();

						using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
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

						return null;
					}
					catch (OracleException e)
					{
						Trace.WriteLine($"Execution plan identifers and transaction status could not been fetched: {e}");
						return e;
					}
				}
			}
		}

		private async Task<IReadOnlyList<CompilationError>> RetrieveCompilationErrors(StatementBase statement, CancellationToken cancellationToken)
		{
			var compilationErrorUpdater = new CompilationErrorDataProvider(statement, _currentSchema);
			await _databaseModel.UpdateModelAsync(cancellationToken, true, compilationErrorUpdater);
			return compilationErrorUpdater.Errors;
		}

		private async Task SafeResolveTransactionStatus(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = OracleDatabaseCommands.SelectLocalTransactionIdCommandText;
				_userTransactionId = OracleReaderValueConvert.ToString(await command.ExecuteScalarAsynchronous(cancellationToken));
				_userTransactionIsolationLevel = String.IsNullOrEmpty(_userTransactionId) ? IsolationLevel.Unspecified : IsolationLevel.ReadCommitted;
			}
		}
	}
}
