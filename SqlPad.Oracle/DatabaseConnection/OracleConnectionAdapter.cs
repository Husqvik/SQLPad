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

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleConnectionAdapter : OracleConnectionAdapterBase
	{
		private const string ModuleNameSqlPadDatabaseModelBase = "Database model";

		private static int _counter;

		private readonly string _connectionString;
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
		private int? _userSessionId;
		private string _userTransactionId;
		private string _userTraceFileName = String.Empty;
		private IsolationLevel _userTransactionIsolationLevel;
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

		public override int? SessionId => _userSessionId;

		public OracleConnectionAdapter(OracleDatabaseModel databaseModel)
		{
			_databaseModel = databaseModel;
			var connectionStringBuilder = new OracleConnectionStringBuilder(databaseModel.ConnectionString.ConnectionString) { Pooling = false, SelfTuning = false };
			_connectionString = connectionStringBuilder.ConnectionString;

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

			_userConnection = new OracleConnection(_connectionString);
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

		public override Task<StatementExecutionBatchResult> ExecuteStatementAsync(StatementBatchExecutionModel executionModel, CancellationToken cancellationToken)
		{
			PreInitialize();

			return ExecuteUserStatementAsync(executionModel, false, cancellationToken);
		}

		public async override Task<IReadOnlyList<ColumnHeader>> RefreshResult(ResultInfo resultInfo, CancellationToken cancellationToken)
		{
			_isExecuting = true;

			try
			{
				var commandReader = ReinitializeResultInfo(resultInfo);
				OracleDataReader dataReader;
				if (commandReader.RefCursorInfo.Parameter != null || commandReader.RefCursorInfo.ImplicitCursorIndex.HasValue)
				{
					await commandReader.Command.ExecuteNonQueryAsynchronous(cancellationToken);
					dataReader = commandReader.RefCursorInfo.Parameter != null
						? ((OracleRefCursor)commandReader.RefCursorInfo.Parameter.Value).GetDataReader()
						: commandReader.Command.ImplicitRefCursors[commandReader.RefCursorInfo.ImplicitCursorIndex.Value].GetDataReader();
				}
				else
				{
					dataReader = await commandReader.Command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken);
				}

				_commandReaders[resultInfo] = new CommandReader { Command = commandReader.Command, Reader = dataReader, RefCursorInfo = commandReader.RefCursorInfo };
				var columnHeaders = GetColumnHeadersFromReader(dataReader);
				_resultInfoColumnHeaders[resultInfo] = columnHeaders;

				return columnHeaders;
			}
			catch (OracleException exception)
			{
				TryHandleNetworkError(exception);
				throw;
			}
			finally
			{
				_isExecuting = false;
			}
		}

		private CommandReader ReinitializeResultInfo(ResultInfo resultInfo)
		{
			var commandReader = _commandReaders[resultInfo];
			commandReader.Reader.Dispose();

			var command = commandReader.Command;
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
			using (var command = _userConnection.CreateCommand())
			{
				await command.SetSchema(_currentSchema, cancellationToken);

				command.CommandText = OracleDatabaseCommands.SelectCurrentSessionId;
				_userSessionId = Convert.ToInt32(await command.ExecuteScalarAsynchronous(cancellationToken));

				await RetrieveTraceFileName(cancellationToken);

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
				command.CommandText = $"CALL DBMS_OUTPUT.{(EnableDatabaseOutput ? "ENABLE(NULL)" : "DISABLE()")}";
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

		private async Task<StatementExecutionBatchResult> ExecuteUserStatementAsync(StatementBatchExecutionModel executionModel, bool isReferenceConstraintNavigation, CancellationToken cancellationToken)
		{
			if (executionModel.Statements == null || executionModel.Statements.Count == 0)
			{
				throw new ArgumentException("An execution batch must contain at least one statement. ", nameof(executionModel));
			}

			_isExecuting = true;
			_userCommandHasCompilationErrors = false;

			var batchResult = new StatementExecutionBatchResult { ExecutionModel = executionModel };
			var statementResults = new List<StatementExecutionResult>();
			StatementExecutionResult currentStatementResult = null;

			try
			{
				SetOracleGlobalization();

				await EnsureUserConnectionOpen(cancellationToken);

				await EnsureDatabaseOutput(cancellationToken);

				var userCommand = _userConnection.CreateCommand();
				userCommand.BindByName = true;
				userCommand.AddToStatementCache = false;

				if (_userTransaction == null)
				{
					_userTransaction = _userConnection.BeginTransaction();
				}

				userCommand.InitialLONGFetchSize = OracleDatabaseModel.InitialLongFetchSize;

				if (executionModel.GatherExecutionStatistics)
				{
					_executionStatisticsDataProvider = new SessionExecutionStatisticsDataProvider(_databaseModel.StatisticsKeys, _userSessionId.Value);
					await _databaseModel.UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionBeginExecutionStatisticsDataProvider);
				}

				foreach (var statement in executionModel.Statements)
				{
					currentStatementResult = new StatementExecutionResult { StatementModel = statement };
					statementResults.Add(currentStatementResult);

					userCommand.Parameters.Clear();
					userCommand.CommandText = statement.StatementText.Replace("\r\n", "\n");

					foreach (var variable in statement.BindVariables)
					{
						var value = await GetBindVariableValue(variable, cancellationToken);
						userCommand.AddSimpleParameter(variable.Name, value, variable.DataType.Name);
					}

					var resultInfoColumnHeaders = new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>();
					var isPlSql = ((OracleStatement)statement.Statement)?.IsPlSql ?? false;
					if (isPlSql && executionModel.EnableDebug && statement.IsPartialStatement)
					{
						throw new InvalidOperationException("Debugging is not supported for PL/SQL fragment. ");
					}

					if (isPlSql)
					{
						currentStatementResult.ExecutedAt = DateTime.Now;

						if (executionModel.EnableDebug)
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
						? await RetrieveCompilationErrors(statement.ValidationModel.Statement, cancellationToken)
						: CompilationError.EmptyArray;
				}
			}
			catch (OracleException exception)
			{
				if (currentStatementResult != null)
				{
					currentStatementResult.Exception = exception;

					if (currentStatementResult.ExecutedAt != null && currentStatementResult.Duration == null)
					{
						currentStatementResult.Duration = DateTime.Now - currentStatementResult.ExecutedAt;
					}
				}

				var executionException = new StatementExecutionException(batchResult, exception);

				if (!TryHandleNetworkError(exception) && exception.Number == (int)OracleErrorCode.UserInvokedCancellation)
				{
					return batchResult;
				}

				currentStatementResult.ErrorPosition = await GetSyntaxErrorIndex(currentStatementResult.StatementModel.StatementText, cancellationToken);

				throw executionException;
			}
			finally
			{
				batchResult.StatementResults = statementResults.AsReadOnly();

				try
				{
					if (_userConnection.State == ConnectionState.Open && !executionModel.EnableDebug && !cancellationToken.IsCancellationRequested)
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
			var exception = await ResolveExecutionPlanIdentifiersAndTransactionStatus(cancellationToken);
			if (exception != null)
			{
				await SafeResolveTransactionStatus(cancellationToken);
			}

			batchResult.DatabaseOutput = await RetrieveDatabaseOutput(cancellationToken);
		}

		private async Task<int?> GetSyntaxErrorIndex(string sqlText, CancellationToken cancellationToken)
		{
			using (var connection = new OracleConnection(_connectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = OracleDatabaseCommands.GetSyntaxErrorIndex;
					command.AddSimpleParameter("SQL_TEXT", sqlText);
					var errorPositionParameter = command.AddSimpleParameter("ERROR_POSITION", null, TerminalValues.Number);

					await connection.OpenAsynchronous(cancellationToken);
					await command.ExecuteNonQueryAsynchronous(cancellationToken);
					var result = (OracleDecimal)errorPositionParameter.Value;
					return result.IsNull ? null : (int?)result.Value;
				}
			}
		}

		private bool TryHandleNetworkError(OracleException exception)
		{
			var errorCode = (OracleErrorCode)exception.Number;
			if (!errorCode.In(OracleErrorCode.EndOfFileOnCommunicationChannel, OracleErrorCode.NotConnectedToOracle))
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
			_userSessionId = null;
			return true;
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

				yield return AcquireRefCursor(command, command.ImplicitRefCursors[i], cursorInfo);
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
			using (var connection = new OracleConnection(_connectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;
					command.CommandText = OracleDatabaseCommands.SelectExecutionPlanIdentifiersCommandText;
					command.AddSimpleParameter("SID", _userSessionId.Value);

					try
					{
						await connection.OpenAsync(cancellationToken);
						connection.ModuleName = $"{_moduleName}/BackgroundConnection";
						connection.ActionName = "Fetch execution info";

						using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
						{
							if (reader.Read())
							{
								_userCommandSqlId = OracleReaderValueConvert.ToString(reader["SQL_ID"]);
								_userCommandChildNumber = OracleReaderValueConvert.ToInt32(reader["SQL_CHILD_NUMBER"]) ?? 0;
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
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = OracleDatabaseCommands.SelectLocalTransactionIdCommandText;
				_userTransactionId = OracleReaderValueConvert.ToString(await command.ExecuteScalarAsynchronous(cancellationToken));
				_userTransactionIsolationLevel = String.IsNullOrEmpty(_userTransactionId) ? IsolationLevel.Unspecified : IsolationLevel.ReadCommitted;
			}
		}

		private struct CommandReader
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
	}
}
