using System;
using System.Collections.Generic;
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

namespace SqlPad.Oracle
{
	public class OracleConnectionAdapter : OracleConnectionAdapterBase
	{
		private const string ModuleNameSqlPadDatabaseModelBase = "Database model";

		private static int _counter;

		private readonly ConnectionStringSettings _connectionString;
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly List<OracleTraceEvent> _activeTraceEvents = new List<OracleTraceEvent>();

		private bool _isExecuting;
		private bool _databaseOutputEnabled;
		private string _moduleName;
		private string _currentSchema;
		private string _identifier;
		private OracleConnection _userConnection;
		private OracleDataReader _userDataReader;
		private OracleCommand _userCommand;
		private string _userCommandSqlId;
		private int _userCommandChildNumber;
		private OracleTransaction _userTransaction;
		private int _userSessionId;
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

		public override string TraceFileName { get { return _userTraceFileName; } }

		public OracleConnectionAdapter(OracleDatabaseModel databaseModel)
		{
			_databaseModel = databaseModel;
			_connectionString = _databaseModel.ConnectionString;

			_oracleConnectionString = new OracleConnectionStringBuilder(_databaseModel.ConnectionString.ConnectionString);

			var identifier = Convert.ToString(Interlocked.Increment(ref _counter));
			UpdateModuleName(identifier);

			InitializeUserConnection();

			SwitchCurrentSchema();
		}

		public override async Task ActivateTraceEvents(IEnumerable<OracleTraceEvent> traceEvents, CancellationToken cancellationToken)
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

			var executionModel = BuildTraceEventActionStatement(_activeTraceEvents, e => e.CommandTextEnable);

			try
			{
				await ExecuteUserStatement(executionModel, cancellationToken);
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

		private static StatementExecutionModel BuildTraceEventActionStatement(IEnumerable<OracleTraceEvent> traceEvents, Func<OracleTraceEvent, string> getCommandTextFunc)
		{
			var builder = new StringBuilder("BEGIN\n");

			foreach (var traceEvent in traceEvents)
			{
				builder.AppendLine(String.Format("\tEXECUTE IMMEDIATE '{0}';", getCommandTextFunc(traceEvent).Replace("'", "''")));
			}

			builder.Append("END;");

			return new StatementExecutionModel {StatementText = builder.ToString(), BindVariables = new BindVariableModel[0]};
		}

		public override async Task StopTraceEvents(CancellationToken cancellationToken)
		{
			StatementExecutionModel executionModel;
			lock (_activeTraceEvents)
			{
				if (_activeTraceEvents.Count == 0)
				{
					return;
				}

				executionModel = BuildTraceEventActionStatement(_activeTraceEvents, e => e.CommandTextDisable);
				_activeTraceEvents.Clear();
			}

			await ExecuteUserStatement(executionModel, cancellationToken);
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
			_moduleName = String.Format("{0}/{1}/{2}", ModuleNameSqlPadDatabaseModelBase, _databaseModel.ConnectionIdentifier, _identifier);
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
			RollbackTransaction().Wait();

			DisposeCommandAndReader();

			DisposeUserConnection();

			_databaseModel.RemoveConnectionAdapter(this);
		}

		public override bool CanFetch
		{
			get { return CanFetchFromReader(_userDataReader) && !_isExecuting; }
		}

		public override bool IsExecuting { get { return _isExecuting; } }

		public override bool EnableDatabaseOutput { get; set; }

		public override async Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			await _databaseModel.UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionEndExecutionStatisticsDataProvider);
			return _executionStatisticsDataProvider.ExecutionStatistics;
		}

		public override Task<IReadOnlyList<object[]>> FetchRecordsAsync(int rowCount, CancellationToken cancellationToken)
		{
			return FetchRecordsFromReader(_userDataReader, rowCount, false).EnumerateAsync(cancellationToken);
		}

		public override bool HasActiveTransaction { get { return !String.IsNullOrEmpty(_userTransactionId); } }

		public override void CommitTransaction()
		{
			ExecuteUserTransactionAction(t => t.Commit());
		}

		public override Task RollbackTransaction()
		{
			return Task.Factory.StartNew(() => ExecuteUserTransactionAction(t => t.Rollback()));
		}

		public override void CloseActiveReader()
		{
			DisposeCommandAndReader();
		}

		public async override Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			var cursorExecutionStatisticsDataProvider = new CursorExecutionStatisticsDataProvider(_userCommandSqlId, _userCommandChildNumber);
			var displayCursorDataProvider = new DisplayCursorDataProvider(_userCommandSqlId, _userCommandChildNumber);
			await _databaseModel.UpdateModelAsync(cancellationToken, true, cursorExecutionStatisticsDataProvider, displayCursorDataProvider);
			cursorExecutionStatisticsDataProvider.ItemCollection.PlanText = displayCursorDataProvider.PlanText;
			return cursorExecutionStatisticsDataProvider.ItemCollection;
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

		private void PreInitialize()
		{
			if (_isExecuting)
				throw new InvalidOperationException("Another statement is executing right now. ");

			DisposeCommandAndReader();
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

		private void DisposeUserConnection()
		{
			if (_userConnection == null)
			{
				return;
			}

			_userConnection.InfoMessage -= UserConnectionInfoMessageHandler;
			_userConnection.Dispose();
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
							: String.Format("{0}{1}", oracleString.Value, oracleString.Value.Length == OracleDatabaseModel.InitialLongFetchSize ? OracleLargeTextValue.Ellipsis : null);

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

		private async Task InitializeSession(CancellationToken cancellationToken)
		{
			using (var command = _userConnection.CreateCommand())
			{
				await command.SetSchema(_currentSchema, cancellationToken);

				command.CommandText = DatabaseCommands.SelectCurrentSessionId;
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

				command.CommandText = DatabaseCommands.SelectTraceFileFullName;

				try
				{
					_userTraceFileName = (string)await command.ExecuteScalarAsynchronous(cancellationToken);
				}
				catch (Exception e)
				{
					Trace.WriteLine(String.Format("Trace file name retrieval failed: {0}", e));
				}
			}
		}

		private async Task<bool> EnsureUserConnectionOpen(CancellationToken cancellationToken)
		{
			var isConnectionStateChanged = await _userConnection.EnsureConnectionOpen(cancellationToken);
			if (isConnectionStateChanged)
			{
				_userConnection.ModuleName = _moduleName;
				_userConnection.ActionName = "User query";
			}

			return isConnectionStateChanged;
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
			_userCommand.InitialLONGFetchSize = OracleDatabaseModel.InitialLongFetchSize;

			foreach (var variable in executionModel.BindVariables)
			{
				_userCommand.AddSimpleParameter(variable.Name, variable.Value, variable.DataType);
			}

			if (executionModel.GatherExecutionStatistics)
			{
				_executionStatisticsDataProvider = new SessionExecutionStatisticsDataProvider(_databaseModel.StatisticsKeys, _userSessionId);
				await _databaseModel.UpdateModelAsync(cancellationToken, true, _executionStatisticsDataProvider.SessionBeginExecutionStatisticsDataProvider);
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
					Statement = executionModel,
					AffectedRowCount = _userDataReader.RecordsAffected,
					DatabaseOutput = await RetrieveDatabaseOutput(cancellationToken),
					ExecutedSuccessfully = true,
					ColumnHeaders = GetColumnHeadersFromReader(_userDataReader),
					InitialResultSet = await FetchRecordsAsync(executionModel.InitialFetchRowCount, cancellationToken),
					CompilationErrors = _userCommandHasCompilationErrors ? await RetrieveCompilationErrors(executionModel.Statement, cancellationToken) : new CompilationError[0]
				};
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
				command.CommandText = DatabaseCommands.FetchDatabaseOutputCommandText;

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
						Trace.WriteLine(String.Format("Execution plan identifers and transaction status could not been fetched: {0}", e));
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
				command.CommandText = DatabaseCommands.SelectLocalTransactionIdCommandText;
				_userTransactionId = OracleReaderValueConvert.ToString(await command.ExecuteScalarAsynchronous(cancellationToken));
				_userTransactionIsolationLevel = String.IsNullOrEmpty(_userTransactionId) ? IsolationLevel.Unspecified : IsolationLevel.ReadCommitted;
			}
		}
	}
}
