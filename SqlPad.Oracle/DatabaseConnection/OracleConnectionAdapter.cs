using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DebugTrace;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif

using ParameterDirection = System.Data.ParameterDirection;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleConnectionAdapter : OracleConnectionAdapterBase
	{
		private const string ModuleNameSqlPadDatabaseModelBase = "Database model";
		private const string UserCommand = "User command";

		private static int _counter;

		private readonly OracleDatabaseModel _databaseModel;
		private readonly List<OracleTraceEvent> _activeTraceEvents = new List<OracleTraceEvent>();
		private readonly Dictionary<ResultInfo, CommandReader> _commandReaders = new Dictionary<ResultInfo, CommandReader>();
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
		private SessionIdentifier? _userSessionIdentifier;
		private TransactionInfo _userTransactionInfo;
		private string _userTraceFileName = String.Empty;
		private bool _userCommandHasCompilationErrors;
		private SessionExecutionStatisticsDataProvider _executionStatisticsDataProvider;
		private OracleDebuggerSession _debuggerSession;

		public override string Identifier
		{
			get { return _identifier; }
			set { UpdateModuleName(value); }
		}

		public override IDatabaseModel DatabaseModel => _databaseModel;

		public override IDebuggerSession DebuggerSession => _debuggerSession;

		public override string TraceFileName => _userTraceFileName;

		public override SessionIdentifier? SessionIdentifier => _userSessionIdentifier;

		public OracleConnectionAdapter(OracleDatabaseModel databaseModel)
		{
			_databaseModel = databaseModel;

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

			var preface = $"EXECUTE IMMEDIATE 'ALTER SESSION SET TRACEFILE_IDENTIFIER = {OracleTraceIdentifier.Normalize(traceIdentifier)}';";
			var commandText = BuildTraceEventActionStatement(_activeTraceEvents, () => preface, e => e.CommandTextEnable);

			try
			{
				await ExecuteSimpleCommandUsingUserConnection(commandText, "Activate trace events", cancellationToken);
				Trace.WriteLine($"Enable trace event command executed successfully: \n{commandText}");

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

		private async Task ExecuteSimpleCommandUsingUserConnection(string commandText, string actionName, CancellationToken cancellationToken)
		{
			await EnsureUserConnectionOpen(cancellationToken);

			using (var command = _userConnection.CreateCommand())
			{
				command.Connection.ActionName = actionName;
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

			await ExecuteSimpleCommandUsingUserConnection(commandText, "Stop trace events", cancellationToken);
			Trace.WriteLine($"Disable trace event command executed successfully: \n{commandText}");
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

			_userConnection = new OracleConnection();
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

			DisposeDebuggerSession();

			DisposeDataReaders();

			DisposeUserConnection();

			_databaseModel.RemoveConnectionAdapter(this);
		}

		private void DisposeDebuggerSession()
		{
			_debuggerSession?.Dispose();
			_debuggerSession = null;
		}

		public override bool CanFetch(ResultInfo resultInfo)
		{
			CommandReader commandReader;
			return !_isExecuting && _commandReaders.TryGetValue(resultInfo, out commandReader) && CanFetchFromReader(commandReader.Reader);
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
			CommandReader commandReader;
			if (_commandReaders.TryGetValue(resultInfo, out commandReader))
			{
				return FetchRecordsFromReader(commandReader.Reader, rowCount, false).EnumerateAsync(cancellationToken);
			}

			throw new ArgumentException($"Result '{resultInfo.ResultIdentifier}' ({resultInfo.Type}) does not exist. ");
		}

		public override bool HasActiveTransaction => _userTransactionInfo != null;

		public override string TransanctionIdentifier => _userTransactionInfo?.ToString();

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
			var displayCursorDataProvider = new DisplayCursorDataProvider(_userCommandSqlId, _userCommandChildNumber, _databaseModel.Version);
			await _databaseModel.UpdateModelAsync(cancellationToken, false, cursorExecutionStatisticsDataProvider, displayCursorDataProvider);
			cursorExecutionStatisticsDataProvider.ItemCollection.PlanText = displayCursorDataProvider.PlanText;
			return cursorExecutionStatisticsDataProvider.ItemCollection;
		}

		public override Task<StatementExecutionBatchResult> ExecuteStatementAsync(StatementBatchExecutionModel executionModel, CancellationToken cancellationToken)
		{
			PreInitialize();

			return ExecuteUserStatementAsync(executionModel, false, cancellationToken);
		}

		public async override Task RefreshResult(StatementExecutionResult result, CancellationToken cancellationToken)
		{
			_isExecuting = true;
			DateTime? executedAt = null;

			try
			{
				await EnsureUserConnectionOpen(cancellationToken);

				var resultInfo = result.ResultInfoColumnHeaders.Keys.Last();
				var commandReader = ReinitializeResultInfo(resultInfo);
				OracleDataReader dataReader;

				executedAt = DateTime.Now;
				var stopWatch = Stopwatch.StartNew();

				if (commandReader.RefCursorInfo.Parameter != null || commandReader.RefCursorInfo.ImplicitCursorIndex.HasValue)
				{
					await commandReader.Command.ExecuteNonQueryAsynchronous(cancellationToken);
					stopWatch.Stop();
					dataReader = commandReader.RefCursorInfo.Parameter != null
						? ((OracleRefCursor)commandReader.RefCursorInfo.Parameter.Value).GetDataReader()
						: commandReader.Command.ImplicitRefCursors[commandReader.RefCursorInfo.ImplicitCursorIndex.Value].GetDataReader();
				}
				else
				{
					dataReader = await commandReader.Command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);
					stopWatch.Stop();
				}

				_commandReaders[resultInfo] = new CommandReader { Command = commandReader.Command, Reader = dataReader, RefCursorInfo = commandReader.RefCursorInfo };
				var columnHeaders = GetColumnHeadersFromReader(dataReader);
				_resultInfoColumnHeaders[resultInfo] = columnHeaders;

				result.Exception = null;
				result.Duration = stopWatch.Elapsed;
				result.ResultInfoColumnHeaders =
					new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>
					{
						{ resultInfo, columnHeaders }
					}.AsReadOnly();
			}
			catch (OracleException exception)
			{
				TryHandleConnectionTerminatedError(exception);
				result.Exception = exception;
				throw;
			}
			finally
			{
				result.ExecutedAt = executedAt;
				_isExecuting = false;
			}
		}

		private CommandReader ReinitializeResultInfo(ResultInfo resultInfo)
		{
			var commandReader = _commandReaders[resultInfo];
			commandReader.Reader.Dispose();

			var command = commandReader.Command;
			command.Connection = _userConnection;

			var commandText = command.CommandText;
			command.CommandText = null;
			command.CommandText = commandText;
			return commandReader;
		}

		public override async Task<StatementExecutionResult> ExecuteChildStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var result = await ExecuteUserStatementAsync(new StatementBatchExecutionModel { Statements = new [] { executionModel } }, true, cancellationToken);
			return result.StatementResults[0];
		}

		private void PreInitialize()
		{
			if (_isExecuting)
			{
				throw new InvalidOperationException("Another statement is executing right now. ");
			}

			DisposeDebuggerSession();
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
			_userTransactionInfo = null;

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
			_commandReaders.Values.ForEach(r => r.Dispose());
			_commandReaders.Clear();
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
			var internalTypes = reader.GetInternalDataTypes();
			for (var i = 0; i < reader.FieldCount; i++)
			{
				var fieldType = reader.GetDataTypeName(i);
				fieldTypes[i] = internalTypes[i] == OracleRowId.InternalCode ? OracleRowId.TypeName : fieldType;
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
							? (object)DBNull.Value
							: $"{oracleString.Value}{(oracleString.Value.Length == OracleDatabaseModel.InitialLongFetchSize ? CellValueConverter.Ellipsis : null)}";

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
					case "IntervalDS":
						value = new OracleIntervalDayToSecond(reader.GetOracleIntervalDS(i));
						break;
					case "IntervalYM":
						value = new OracleIntervalYearToMonth(reader.GetOracleIntervalYM(i));
						break;
					case "Char":
					case "NChar":
					case "Varchar":
					case "Varchar2":
					case "NVarchar":
					case "NVarchar2":
						value = new OracleSimpleValue(reader.GetValue(i));
						break;
					case OracleRowId.TypeName:
						value = new OracleRowId(reader.GetOracleString(i));
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
			_userConnection.ModuleName = _moduleName;
			_userConnection.ActionName = "Set default schema";

			using (var command = _userConnection.CreateCommand())
			{
				await command.SetSchema(_currentSchema, cancellationToken);

				command.CommandText = OracleDatabaseCommands.SelectCurrentSessionIdentifierCommandText;
				_userConnection.ActionName = "Get session identifier";

				using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
				{
					if (reader.Read())
					{
						_userSessionIdentifier = new SessionIdentifier(Convert.ToInt32(reader["INSTANCE"]), Convert.ToInt32(reader["SID"]));
					}
				}

				await RetrieveTraceFileName(cancellationToken);

				var startupScript = OracleConfiguration.Configuration.StartupScript;
				await ExecuteStartupScript(command, startupScript, cancellationToken);
				await ExecuteStartupScript(command, OracleConfiguration.Configuration.GetConnectionStartupScript(_databaseModel.ConnectionString.Name), cancellationToken);
			}
		}

		private async Task ExecuteStartupScript(OracleCommand command, string startupScript, CancellationToken cancellationToken)
		{
			if (String.IsNullOrWhiteSpace(startupScript))
			{
				return;
			}

			_userConnection.ActionName = "Execute startup script";

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
		}

		private async Task RetrieveTraceFileName(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = OracleDatabaseCommands.SelectTraceFileFullName;

				try
				{
					_userTraceFileName = (string)await command.ExecuteScalarAsynchronous(cancellationToken);
					Trace.WriteLine($"Instance {_userSessionIdentifier.Value.Instance} Session ID {_userSessionIdentifier.Value.SessionId} trace file name is {_userTraceFileName}. ");
				}
				catch (Exception e)
				{
					Trace.WriteLine($"Trace file name retrieval failed: {e}");
				}
			}
		}

		private async Task EnsureUserConnectionOpen(CancellationToken cancellationToken)
		{
			var connectionString = OracleConnectionStringRepository.GetUserConnectionString(_databaseModel.ConnectionString.ConnectionString);
			var isConnectionStateChanged = await _userConnection.EnsureConnectionOpen(connectionString, cancellationToken);
			if (isConnectionStateChanged)
			{
				Trace.WriteLine("User connection has been open. ");
			}

			if (isConnectionStateChanged || _userSessionIdentifier == null)
			{
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
				command.Connection.ActionName = "Set database output";
				command.CommandText = $"CALL DBMS_OUTPUT.{(EnableDatabaseOutput ? "ENABLE(NULL)" : "DISABLE()")}";
				await command.ExecuteNonQueryAsynchronous(cancellationToken);
			}
			
			_databaseOutputEnabled = EnableDatabaseOutput;
		}

		private static void SetOracleGlobalization()
		{
#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
			var info = OracleGlobalization.GetClientInfo();
			var numberFormat = CultureInfo.CurrentCulture.NumberFormat;
			var decimalSeparator = numberFormat.NumberDecimalSeparator;
			var groupSeparator = numberFormat.NumberGroupSeparator;
			info.NumericCharacters = $"{(decimalSeparator.Length == 1 ? decimalSeparator : ".")}{(groupSeparator.Length == 1 ? groupSeparator : " ")}";
			OracleGlobalization.SetThreadInfo(info);
#endif
		}

		private async Task<StatementExecutionBatchResult> ExecuteUserStatementAsync(StatementBatchExecutionModel batchExecutionModel, bool isReferenceConstraintNavigation, CancellationToken cancellationToken)
		{
			if (batchExecutionModel.Statements == null || batchExecutionModel.Statements.Count == 0)
			{
				throw new ArgumentException("An execution batch must contain at least one statement. ", nameof(batchExecutionModel));
			}

			_isExecuting = true;
			_userCommandHasCompilationErrors = false;

			var batchResult = new StatementExecutionBatchResult { ExecutionModel = batchExecutionModel };
			var statementResults = new List<StatementExecutionResult>();
			StatementExecutionResult currentStatementResult = null;

			try
			{
				SetOracleGlobalization();

				await EnsureUserConnectionOpen(cancellationToken);

				await EnsureDatabaseOutput(cancellationToken);

				if (batchExecutionModel.GatherExecutionStatistics)
				{
					_executionStatisticsDataProvider = new SessionExecutionStatisticsDataProvider(_databaseModel.StatisticsKeys, _userSessionIdentifier.Value.SessionId);
					await _databaseModel.UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionBeginExecutionStatisticsDataProvider);
				}

				_userConnection.ActionName = UserCommand;

				foreach (var executionModel in batchExecutionModel.Statements)
				{
					currentStatementResult = new StatementExecutionResult { StatementModel = executionModel };
					statementResults.Add(currentStatementResult);

					if (_userTransaction == null)
					{
						var isolationLevel = executionModel.Statement?.RootNode[NonTerminals.Statement, NonTerminals.SetTransactionStatement, NonTerminals.TransactionModeOrIsolationLevelOrRollbackSegment, NonTerminals.SerializableOrReadCommitted, Terminals.Serializable] != null
							? IsolationLevel.Serializable
							: IsolationLevel.ReadCommitted;

						_userTransaction = _userConnection.BeginTransaction(isolationLevel);
					}

					var userCommand = InitializeUserCommand();
					userCommand.CommandText = executionModel.StatementText.Replace("\r\n", "\n");

					foreach (var variable in executionModel.BindVariables)
					{
						var value = await GetBindVariableValue(variable, cancellationToken);
						userCommand.AddSimpleParameter(variable.Name, value, variable.DataType.Name);
					}

					var resultInfoColumnHeaders = new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>();
					var statement = (OracleStatement)executionModel.Statement;
					var isPlSql = statement?.IsPlSql ?? false;
					if (isPlSql && batchExecutionModel.EnableDebug && executionModel.IsPartialStatement)
					{
						throw new InvalidOperationException("Debugging is not supported for PL/SQL fragment. ");
					}

					if (isPlSql)
					{
						currentStatementResult.ExecutedAt = DateTime.Now;

						if (batchExecutionModel.EnableDebug)
						{
							// TODO: Add COMPILE DEBUG
							_debuggerSession = new OracleDebuggerSession(this, (OracleCommand)userCommand.Clone(), batchResult);
							_debuggerSession.Detached += DebuggerSessionDetachedHandler;
						}
						else
						{
							currentStatementResult.AffectedRowCount = await userCommand.ExecuteNonQueryAsynchronous(cancellationToken);
							currentStatementResult.Duration = DateTime.Now - currentStatementResult.ExecutedAt;
							resultInfoColumnHeaders.AddRange(AcquireImplicitRefCursors(userCommand));
						}
					}
					else
					{
						currentStatementResult.ExecutedAt = DateTime.Now;
						var dataReader = await userCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);
						currentStatementResult.Duration = DateTime.Now - currentStatementResult.ExecutedAt;
						currentStatementResult.AffectedRowCount = dataReader.RecordsAffected;
						var resultInfo = isReferenceConstraintNavigation
							? new ResultInfo($"ReferenceConstrantResult{dataReader.GetHashCode()}", null, ResultIdentifierType.SystemGenerated)
							: new ResultInfo($"MainResult{dataReader.GetHashCode()}", $"Result set {_resultInfoColumnHeaders.Count + 1}", ResultIdentifierType.UserDefined);

						var columnHeaders = GetColumnHeadersFromReader(dataReader);
						if (columnHeaders.Count > 0)
						{
							_commandReaders.Add(resultInfo, new CommandReader { Reader = dataReader, Command = userCommand } );
							resultInfoColumnHeaders.Add(resultInfo, columnHeaders);
						}
					}

					resultInfoColumnHeaders.AddRange(UpdateBindVariables(currentStatementResult.StatementModel, userCommand));
					currentStatementResult.ResultInfoColumnHeaders = resultInfoColumnHeaders.AsReadOnly();
					_resultInfoColumnHeaders.AddRange(resultInfoColumnHeaders);

					currentStatementResult.CompilationErrors = _userCommandHasCompilationErrors
						? await RetrieveCompilationErrors(executionModel.ValidationModel.Statement, cancellationToken)
						: CompilationError.EmptyArray;

					currentStatementResult.SuccessfulExecutionMessage = statement == null
						? OracleStatement.DefaultMessageCommandExecutedSuccessfully
						: statement.BuildSuccessfulExecutionMessage(currentStatementResult.AffectedRowCount);
				}
			}
			catch (OracleException exception)
			{
				if (currentStatementResult == null)
				{
					statementResults.Add(
						new StatementExecutionResult
						{
							StatementModel = batchExecutionModel.Statements[0],
							Exception = exception
						});
				}
				else
				{
					currentStatementResult.Exception = exception;

					if (currentStatementResult.ExecutedAt != null && currentStatementResult.Duration == null)
					{
						currentStatementResult.Duration = DateTime.Now - currentStatementResult.ExecutedAt;
					}
				}

				var executionException = new StatementExecutionException(batchResult, exception);

				var isConnectionTerminated = TryHandleConnectionTerminatedError(exception);
				if (isConnectionTerminated)
				{
					throw executionException;
				}

				if (exception.Number == (int)OracleErrorCode.UserInvokedCancellation)
				{
					return batchResult;
				}

				if (currentStatementResult != null)
				{
					currentStatementResult.ErrorPosition = await GetSyntaxErrorIndex(currentStatementResult.StatementModel.StatementText, cancellationToken);
				}

				throw executionException;
			}
			finally
			{
				batchResult.StatementResults = statementResults.AsReadOnly();

				try
				{
					if (_userConnection.State == ConnectionState.Open && !batchExecutionModel.EnableDebug && !cancellationToken.IsCancellationRequested)
					{
						await FinalizeBatchExecution(batchResult, cancellationToken);
					}
				}
				finally
				{
					_isExecuting = false;
				}
			}

			return batchResult;
		}

		internal async Task FinalizeBatchExecution(StatementExecutionBatchResult batchResult, CancellationToken cancellationToken)
		{
			_userTransactionInfo = null;

			var exception = await ResolveExecutionPlanIdentifiersAndTransactionStatus(cancellationToken);
			if (exception != null)
			{
				await SafeResolveTransactionStatus(cancellationToken);
			}

			batchResult.DatabaseOutput = await RetrieveDatabaseOutput(cancellationToken);
		}

		private async Task<int?> GetSyntaxErrorIndex(string sqlText, CancellationToken cancellationToken)
		{
			using (var connection = new OracleConnection(_databaseModel.BackgroundConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = OracleDatabaseCommands.GetSyntaxErrorIndex;
					command.AddSimpleParameter("SQL_TEXT", sqlText);
					var errorPositionParameter = command.AddSimpleParameter("ERROR_POSITION", null, TerminalValues.Number);

					await connection.OpenAsynchronous(cancellationToken);
					await command.ExecuteNonQueryAsynchronous(cancellationToken);
					await connection.CloseAsynchronous(cancellationToken);
					var result = (OracleDecimal)errorPositionParameter.Value;
					return result.IsNull ? null : (int?)result.Value;
				}
			}
		}

		private bool TryHandleConnectionTerminatedError(OracleException exception)
		{
			var errorCode = (OracleErrorCode)exception.Number;
			if (!errorCode.In(OracleErrorCode.EndOfFileOnCommunicationChannel, OracleErrorCode.NotConnectedToOracle, OracleErrorCode.TnsPacketWriterFailure, OracleErrorCode.SessionTerminatedByDebugger, OracleErrorCode.UnableToSendBreak))
			{
				return false;
			}

			InitializeUserConnection();

			_userTraceFileName = String.Empty;

			lock (_activeTraceEvents)
			{
				_activeTraceEvents.Clear();
			}

			_databaseModel.Disconnect(exception);
			_userTransaction = null;
			_userSessionIdentifier = null;
			return true;
		}

		private OracleCommand InitializeUserCommand()
		{
			var userCommand = _userConnection.CreateCommand();
			userCommand.BindByName = true;
			userCommand.AddToStatementCache = false;
			userCommand.InitialLONGFetchSize = OracleDatabaseModel.InitialLongFetchSize;
			return userCommand;
		}

		private static async Task<object> GetBindVariableValue(BindVariableModel variable, CancellationToken cancellationToken)
		{
			var value = variable.Value;
			if (!variable.IsFilePath)
			{
				return value;
			}

			using (var file = File.OpenRead(value.ToString()))
			{
				switch (variable.DataType.Name)
				{
					case TerminalValues.Raw:
					case TerminalValues.Blob:
						value = await file.ReadAllBytesAsync(cancellationToken);
						break;

					default:
						value = await file.ReadAllTextAsync(cancellationToken);
						break;
				}
			}

			return value;
		}

		private void DebuggerSessionDetachedHandler(object sender, EventArgs args)
		{
			var statementResult = _debuggerSession.ExecutionResult.StatementResults[0];
			var resultInfoColumnHeaders = new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>();
			resultInfoColumnHeaders.AddRange(AcquireImplicitRefCursors(_debuggerSession.DebuggedCommand));
			resultInfoColumnHeaders.AddRange(UpdateBindVariables(statementResult.StatementModel, _debuggerSession.DebuggedCommand));
			_resultInfoColumnHeaders.AddRange(resultInfoColumnHeaders);
			statementResult.ResultInfoColumnHeaders = resultInfoColumnHeaders.AsReadOnly();

			_debuggerSession.Detached -= DebuggerSessionDetachedHandler;
			_debuggerSession.Dispose();
			_debuggerSession = null;
		}

		private IEnumerable<KeyValuePair<ResultInfo, IReadOnlyList<ColumnHeader>>> UpdateBindVariables(StatementExecutionModel executionModel, OracleCommand userCommand)
		{
			var bindVariableModels = executionModel.BindVariables.ToDictionary(v => v.Name, v => v);
			foreach (OracleParameter parameter in userCommand.Parameters)
			{
				var bindVariableModel = bindVariableModels[parameter.ParameterName];

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

				if (!bindVariableModel.IsFilePath)
				{
					if (parameter.Value is OracleBinary)
					{
						var oracleBinary = (OracleBinary)parameter.Value;
						value = oracleBinary.IsNull ? null : oracleBinary.Value.ToHexString();
					}

					var clob = parameter.Value as OracleClob;
					if (clob != null)
					{
						value = clob.IsNull ? String.Empty : clob.Value;
					}

					var blob = parameter.Value as OracleBlob;
					if (blob != null)
					{
						value = blob.IsNull ? null : blob.Value.ToHexString();
					}
				}

				var refCursor = parameter.Value as OracleRefCursor;
				if (refCursor != null)
				{
					if (refCursor.IsNull)
					{
						continue;
					}

					var refCursorInfo =
						new RefCursorInfo
						{
							CursorName = parameter.ParameterName,
							Parameter = parameter
						};

					yield return AcquireRefCursor(userCommand, refCursor, refCursorInfo);
				}
				else
				{
					if (!bindVariableModel.IsFilePath)
					{
						bindVariableModel.Value = value;
					}
				}
			}
		}

		private IEnumerable<KeyValuePair<ResultInfo, IReadOnlyList<ColumnHeader>>> AcquireImplicitRefCursors(OracleCommand command)
		{
			if (command.ImplicitRefCursors == null)
			{
				yield break;
			}

			for (var i = 0; i < command.ImplicitRefCursors.Length; i++)
			{
				var cursorInfo =
					new RefCursorInfo
					{
						CursorName = $"Implicit cursor {i + 1}",
						ImplicitCursorIndex = i
					};

				var refCursor = command.ImplicitRefCursors[i];
				if (!refCursor.IsNull)
				{
					yield return AcquireRefCursor(command, refCursor, cursorInfo);
				}
			}
		}

		private KeyValuePair<ResultInfo, IReadOnlyList<ColumnHeader>> AcquireRefCursor(OracleCommand command, OracleRefCursor refCursor, RefCursorInfo refCursorInfo)
		{
			var reader = refCursor.GetDataReader();
			var resultInfo = new ResultInfo($"RefCursor{reader.GetHashCode()}", refCursorInfo.CursorName, ResultIdentifierType.UserDefined);

			_commandReaders.Add(resultInfo, new CommandReader { Reader = reader, Command = command, RefCursorInfo = refCursorInfo } );
			return new KeyValuePair<ResultInfo, IReadOnlyList<ColumnHeader>>(resultInfo, GetColumnHeadersFromReader(reader));
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
			if (!_databaseOutputEnabled || cancellationToken.IsCancellationRequested)
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
			using (var connection = new OracleConnection(_databaseModel.BackgroundConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;
					command.CommandText = OracleDatabaseCommands.SelectExecutionPlanIdentifiersCommandText;
					command.AddSimpleParameter("SID", _userSessionIdentifier.Value.SessionId);

					try
					{
						await connection.OpenAsynchronous(cancellationToken);
						connection.ModuleName = $"{_moduleName}/BackgroundConnection".EnsureMaximumLength(64);
						connection.ActionName = "Fetch execution info";

						_userCommandSqlId = null;

						using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
						{
							if (!await reader.ReadAsynchronous(cancellationToken))
							{
								return null;
							}

							_userCommandSqlId = OracleReaderValueConvert.ToString(reader["SQL_ID"]);
							_userCommandChildNumber = OracleReaderValueConvert.ToInt32(reader["SQL_CHILD_NUMBER"]) ?? 0;
							var undoSegmentNumber = OracleReaderValueConvert.ToInt32(reader["XIDUSN"]);
							if (undoSegmentNumber.HasValue)
							{
								_userTransactionInfo =
									new TransactionInfo
									{
										UndoSegmentNumber = undoSegmentNumber.Value,
										SlotNumber = Convert.ToInt32(reader["XIDSLOT"]),
										SequnceNumber = Convert.ToInt32(reader["XIDSQN"]),
										IsolationLevel = (IsolationLevel)Convert.ToInt32(reader["TRANSACTION_ISOLATION_LEVEL"])
									};
							}
						}

						return null;
					}
					catch (OracleException e)
					{
						Trace.WriteLine($"Execution plan identifers and transaction status could not been fetched: {e}");
						return e;
					}
					finally
					{
						await connection.CloseAsynchronous(cancellationToken);
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
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = OracleDatabaseCommands.SelectLocalTransactionIdCommandText;
				var transactionId  = OracleReaderValueConvert.ToString(await command.ExecuteScalarAsynchronous(cancellationToken));

				if (!String.IsNullOrEmpty(transactionId))
				{
					var transactionElements = transactionId.Split('.');
					_userTransactionInfo =
						new TransactionInfo
						{
							UndoSegmentNumber = Convert.ToInt32(transactionElements[0]),
							SlotNumber = Convert.ToInt32(transactionElements[1]),
							SequnceNumber = Convert.ToInt32(transactionElements[2]),
							IsolationLevel = IsolationLevel.Unspecified
						};
				}
			}
		}

		private struct CommandReader : IDisposable
		{
			public OracleDataReader Reader;
			public OracleCommand Command;
			public RefCursorInfo RefCursorInfo;

			public void Dispose()
			{
				Reader.Dispose();
				Command.Dispose();
			}
		}

		private struct RefCursorInfo
		{
			public string CursorName;
			public OracleParameter Parameter;
			public int? ImplicitCursorIndex;
		}

		private class TransactionInfo
		{
			public int UndoSegmentNumber;
			public int SlotNumber;
			public int SequnceNumber;
			public IsolationLevel IsolationLevel;

			public override string ToString()
			{
				return $"{UndoSegmentNumber}.{SlotNumber}.{SequnceNumber}{IsolationLevelPostfix}";
			}

			private string IsolationLevelPostfix
			{
				get
				{
					switch (IsolationLevel)
					{
						case IsolationLevel.ReadCommitted:
							return " (read committed)";
						case IsolationLevel.Serializable:
							return " (serializable)";
						default:
							return String.Empty;
					}
				}
			}
		}
	}
}
