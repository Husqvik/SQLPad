using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using SqlPad.Oracle.DataDictionary;
using OracleCollectionType = Oracle.DataAccess.Client.OracleCollectionType;
using ParameterDirection = System.Data.ParameterDirection;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif

using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleDebuggerSession : IDebuggerSession, IDisposable
	{
		private const string ParameterDebugActionStatus = "DEBUG_ACTION_STATUS";
		private const string PlSqlBlockTitle = "Anonymous PL/SQL block";

		private static readonly XmlSerializer StackTraceSerializer = new XmlSerializer(typeof(OracleStackTrace));
		private static readonly Regex RegexCursorStatus = new Regex(@"^flags:(?<Flags>\d+),\srowcount:(?<RowCount>\d+)(,\sknlflags:(?<KnlFlags>\d+))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		private readonly OracleConnectionAdapter _connectionAdapter;
		private readonly OracleCommand _debuggedSessionCommand;
		private readonly OracleConnection _debuggerConnection;
		private readonly OracleCommand _debuggerSessionCommand;
		private readonly List<DebugProgramItem> _stackTrace = new List<DebugProgramItem>();
		private readonly Dictionary<string, string> _sources = new Dictionary<string, string>();

		private OracleRuntimeInfo _runtimeInfo;
		private string _debuggerSessionId;
		private Task<int> _debuggedAction;

		public event EventHandler Attached;

		public event EventHandler Detached;

		public OracleObjectIdentifier ActiveObject => OracleObjectIdentifier.Create(_runtimeInfo.SourceLocation.Owner, _runtimeInfo.SourceLocation.Name);

		public int? ActiveLine => _runtimeInfo.SourceLocation.LineNumber;

		internal StatementExecutionBatchResult ExecutionResult { get; }

		internal OracleCommand DebuggedCommand { get; }

		public IReadOnlyList<DebugProgramItem> StackTrace => _stackTrace;

		public DatabaseExceptionInformation CurrentException { get; private set; }

		public bool BreakOnExceptions { get; set; }

		public OracleDebuggerSession(OracleConnectionAdapter connectionAdapter, OracleCommand debuggedCommand, StatementExecutionBatchResult executionResult)
		{
			_connectionAdapter = connectionAdapter;
			ExecutionResult = executionResult;
			DebuggedCommand = debuggedCommand;
			var debuggedConnection = DebuggedCommand.Connection;
			_debuggedSessionCommand = debuggedConnection.CreateCommand();
			_debuggedSessionCommand.BindByName = true;
			_debuggerConnection = (OracleConnection)debuggedConnection.Clone();
			_debuggerSessionCommand = _debuggerConnection.CreateCommand();
			_debuggerSessionCommand.BindByName = true;

			_sources.Add(PlSqlBlockTitle, debuggedCommand.CommandText.Trim());
		}

		public async Task<bool> IsRunning(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = "BEGIN IF dbms_debug.target_program_running THEN :isRunning := 1; ELSE :isRunning := 0; END IF; END;";
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("ISRUNNING", null, TerminalValues.Number);
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			var isRunning = ((OracleDecimal)_debuggerSessionCommand.Parameters[0].Value).Value;
			return Convert.ToBoolean(isRunning);
		}

		public async Task<OracleStackTrace> GetStackTrace(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.GetDebuggerStackTrace;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("OUTPUT_CLOB", null, TerminalValues.Clob);
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			var xmlString = ((OracleClob)_debuggerSessionCommand.Parameters[0].Value).Value;

			using (var reader = new StringReader(xmlString))
			{
				return (OracleStackTrace)StackTraceSerializer.Deserialize(reader);
			}
		}

		public async Task Start(CancellationToken cancellationToken)
		{
			_debuggedSessionCommand.CommandText = OracleDatabaseCommands.StartDebuggee;
			_debuggedSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", null, TerminalValues.Varchar2, 12);
			var debuggedSessionIdParameter = _debuggedSessionCommand.Parameters[0];

			await _debuggedSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			_debuggerSessionId = ((OracleString)debuggedSessionIdParameter.Value).Value;

			Trace.WriteLine($"Target debug session initialized. Debug session ID = {_debuggerSessionId}");

			await Attach(cancellationToken);
			Trace.WriteLine("Debugger attached. ");

			var attachTask = Synchronize(cancellationToken).ContinueWith(AfterSynchronized, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion);

			_debuggedAction = DebuggedCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			Trace.WriteLine("Debugged action started. ");

			await attachTask;
		}

		public async Task GetValue(WatchItem watchItem, CancellationToken cancellationToken)
		{
			SetupGetValueCommandParameters(watchItem.Name);
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var result = (ValueInfoStatus)GetValueFromOracleDecimal(_debuggerSessionCommand.Parameters["RESULT"]);
			var value = GetValueFromOracleString(_debuggerSessionCommand.Parameters["VALUE"]);
			Trace.WriteLine($"Get value '{watchItem.Name}' result: {result}; value={value}");

			if (result != ValueInfoStatus.ErrorIndexedTable)
			{
				watchItem.Value = result == ValueInfoStatus.ErrorNullValue ? "NULL" : FormatIfCursorValue(value);
				watchItem.ChildItems = null;
				return;
			}

			var indexes = await GetCollectionIndexes(OracleObjectIdentifier.Create(_runtimeInfo.SourceLocation.Owner, _runtimeInfo.SourceLocation.Name), watchItem.Name, cancellationToken);
			watchItem.Value = $"collection ({indexes.Count} items)";

			var watchItems = new ObservableCollection<WatchItem>();
			foreach (var index in indexes)
			{
				var collectionItemExpression = $"{watchItem.Name}({index.Value})";
				SetupGetValueCommandParameters(collectionItemExpression);
				await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

				var childItem =
					new WatchItem
					{
						Name = collectionItemExpression,
						Value = GetValueFromOracleString(_debuggerSessionCommand.Parameters["VALUE"])
					};

				watchItems.Add(childItem);
			}

			watchItem.ChildItems = watchItems;
		}

		private static string FormatIfCursorValue(string value)
		{
			var match = RegexCursorStatus.Match(value);
			return match.Success
				? $"Cursor (status: {((OracleCursorFlags)Convert.ToInt32(match.Groups["Flags"].Value)).ToPrettyString(formatFunction: e => e.ToString().SplitCamelCase().ToLowerInvariant())}; fetched rows: {Convert.ToInt64(match.Groups["RowCount"].Value)})"
				: value;
		}

		private void SetupGetValueCommandParameters(string expression)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.DebuggerGetValue;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);
			_debuggerSessionCommand.AddSimpleParameter("VARIABLE_NAME", expression, TerminalValues.Varchar2);
			_debuggerSessionCommand.AddSimpleParameter("VALUE", null, TerminalValues.Varchar2, 32767);
		}

		private async Task<IReadOnlyList<OracleDecimal>> GetCollectionIndexes(OracleObjectIdentifier objectIdentifier, string variable, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.GetDebuggerCollectionIndexes;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);
			_debuggerSessionCommand.AddSimpleParameter("OWNER", objectIdentifier.HasOwner ? objectIdentifier.NormalizedOwner.Trim('"') : null);
			_debuggerSessionCommand.AddSimpleParameter("NAME", String.IsNullOrEmpty(objectIdentifier.Name) ? null : objectIdentifier.NormalizedName.Trim('"'));
			_debuggerSessionCommand.AddSimpleParameter("VARIABLE_NAME", variable, TerminalValues.Varchar2);
			var entriesParameter = _debuggerSessionCommand.CreateParameter();
			entriesParameter.Direction = ParameterDirection.Output;
			entriesParameter.OracleDbType = OracleDbType.Decimal;
			entriesParameter.CollectionType = OracleCollectionType.PLSQLAssociativeArray;
			entriesParameter.ParameterName = "ENTRIES";
			entriesParameter.Size = 32767;
			entriesParameter.Value = new int[0];

			_debuggerSessionCommand.Parameters.Add(entriesParameter);

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			var result = (ValueInfoStatus)GetValueFromOracleDecimal(_debuggerSessionCommand.Parameters["RESULT"]);

			var entries = (OracleDecimal[])entriesParameter.Value;

			Trace.WriteLine($"Get indexes '{variable}' result: {result}; indexes retrieved: {entries.Length}");
			return entries;
		}

		public async Task SetValue(WatchItem watchItem, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.DebuggerSetValue;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);
			var statement = $"{watchItem.Name} := {watchItem.Value};";
			_debuggerSessionCommand.AddSimpleParameter("ASSIGNMENT_STATEMENT", statement, TerminalValues.Varchar2);

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var result = (ValueInfoStatus)GetValueFromOracleDecimal(_debuggerSessionCommand.Parameters["RESULT"]);
			Trace.WriteLine($"Set value '{statement}' result: {result}");
		}

		private async void AfterSynchronized(Task synchronzationTask, object cancellationToken)
		{
			Trace.WriteLine("Debugger synchronized. ");

			await StepInto((CancellationToken)cancellationToken);

			if (_runtimeInfo.IsTerminated == true)
			{
				RaiseDetached();
			}
			else
			{
				Attached?.Invoke(this, EventArgs.Empty);
			}
		}

		private static void AddDebugParameters(OracleCommand command)
		{
			command.AddSimpleParameter(ParameterDebugActionStatus, null, TerminalValues.Number);
			command.AddSimpleParameter("BREAKPOINT", null, TerminalValues.Number);
			command.AddSimpleParameter("INTERPRETERDEPTH", null, TerminalValues.Number);
			command.AddSimpleParameter("LINE", null, TerminalValues.Number);
			command.AddSimpleParameter("OER", null, TerminalValues.Number);
			command.AddSimpleParameter("REASON", null, TerminalValues.Number);
			command.AddSimpleParameter("STACKDEPTH", null, TerminalValues.Number);
			command.AddSimpleParameter("TERMINATED", null, TerminalValues.Number);
			command.AddSimpleParameter("DBLINK", null, TerminalValues.Varchar2, 30);
			command.AddSimpleParameter("ENTRYPOINTNAME", null, TerminalValues.Varchar2, 512);
			command.AddSimpleParameter("OWNER", null, TerminalValues.Varchar2, 30);
			command.AddSimpleParameter("NAME", null, TerminalValues.Varchar2, 30);
			command.AddSimpleParameter("NAMESPACE", null, TerminalValues.Number);
			command.AddSimpleParameter("LIBUNITTYPE", null, TerminalValues.Number);
		}

		private async Task ContinueAndDetachIfTerminated(OracleDebugBreakFlags breakFlags, CancellationToken cancellationToken)
		{
			CurrentException = null;

			await Continue(breakFlags, cancellationToken);

			if (_runtimeInfo.Reason == OracleDebugReason.Exception)
			{
				CurrentException =
					new DatabaseExceptionInformation
					{
						ErrorCode = _runtimeInfo.OerException.Value,
						ErrorMessage = await GetOracleExceptionMessage(_runtimeInfo.OerException.Value, cancellationToken)
					};
			}

			if (_runtimeInfo.IsTerminated == true)
			{
				await Detach(cancellationToken);
			}
		}

		private async Task<string> GetOracleExceptionMessage(int errorCode, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = "BEGIN :message := SQLERRM(:errorCode); END;";
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("ERRORCODE", -errorCode);
			var messageParameter = _debuggerSessionCommand.AddSimpleParameter("MESSAGE", null, TerminalValues.Varchar2, 32767);

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			return OracleReaderValueConvert.ToString(((OracleString)messageParameter.Value).Value);
		}

		private async Task Continue(OracleDebugBreakFlags breakFlags, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.ContinueDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("BREAK_FLAGS", (int)breakFlags, TerminalValues.Number);
			AddDebugParameters(_debuggerSessionCommand);

			_debuggerConnection.ActionName = "Continue";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var status = (OracleDebugActionResult)GetValueFromOracleDecimal(_debuggerSessionCommand.Parameters[ParameterDebugActionStatus]);

			await UpdateRuntimeInfo(GetRuntimeInfo(_debuggerSessionCommand), cancellationToken);
		}

		public async Task GetLineMap(OracleObjectIdentifier objectIdentifier, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.GetDebuggerLineMap;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("OWNER", objectIdentifier.HasOwner ? objectIdentifier.NormalizedOwner.Trim('"') : null);
			_debuggerSessionCommand.AddSimpleParameter("NAME", String.IsNullOrEmpty(objectIdentifier.Name) ? null : objectIdentifier.NormalizedName.Trim('"'));
			var maxLineParameter = _debuggerSessionCommand.AddSimpleParameter("MAXLINE", null, TerminalValues.Number);
			var numberOfEntryPointsParameter = _debuggerSessionCommand.AddSimpleParameter("NUMBER_OF_ENTRY_POINTS", null, TerminalValues.Number);
			var lineMapParameter = _debuggerSessionCommand.AddSimpleParameter("LINEMAP", null, TerminalValues.Raw, 32767);
			var resultParameter = _debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);

			_debuggerConnection.ActionName = "Get line map";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var result = GetValueFromOracleDecimal(resultParameter);
			if (result != 0)
			{
				return;
			}

			var maxLine = GetValueFromOracleDecimal(maxLineParameter);
			var numberOfEntryPoints = GetValueFromOracleDecimal(numberOfEntryPointsParameter);
			var lineMap = ((OracleBinary)lineMapParameter.Value).Value;
		}

		private async Task Attach(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.AttachDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", _debuggerSessionId);

			await _debuggerConnection.OpenAsynchronous(cancellationToken);
			_debuggerConnection.ModuleName = "SQLPad PL/SQL Debugger";
			_debuggerConnection.ActionName = "Attach";

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
		}

		private async Task Synchronize(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.SynchronizeDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			AddDebugParameters(_debuggerSessionCommand);

			_debuggerConnection.ActionName = "Synchronize";

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
		}

		private async Task UpdateRuntimeInfo(OracleRuntimeInfo runtimeInfo, CancellationToken cancellationToken)
		{
			_runtimeInfo = runtimeInfo;
			_runtimeInfo.Trace();

			if (_runtimeInfo.IsTerminated == true)
			{
				return;
			}

			var stackTrace = await GetStackTrace(cancellationToken);
			_stackTrace.Clear();

			foreach (var item in stackTrace.Items)
			{
				var isAnonymousBlock = String.IsNullOrEmpty(item.Name);
				var objectIdentifier = OracleObjectIdentifier.Create(item.Owner, item.Name);
				var objectName = isAnonymousBlock
					? PlSqlBlockTitle
					: $"{item.Owner}.{item.Name}";

				string programText;
				if (!_sources.TryGetValue(objectName, out programText))
				//{
				//	if (isAnonymousBlock && stackTrace.Items[0] != item)
				//	{
				//		var anonymousPlSqlSources = await GetAnonymousPlSqlSources(cancellationToken);
				//		anonymousPlSqlSources = anonymousPlSqlSources.Trim();
				//		foreach (var kvp in _sources)
				//		{
				//			if (String.Equals(kvp.Value, anonymousPlSqlSources))
				//			{
				//				objectName = kvp.Key;
				//				programText = kvp.Value;
				//				break;
				//			}
				//		}

				//		if (String.Equals(objectName, PlSqlBlockTitle))
				//		{
				//			objectName = $"dynamic PL/SQL block #{dynamicSqlBlockCounter++}";
				//			_sources.Add(objectName, anonymousPlSqlSources);
				//		}
				//	}
				//}
				//else
				{
					programText = await GetSources(item, cancellationToken);
					_sources.Add(objectName, programText);
				}

				var debugProgramItem =
					new DebugProgramItem
					{
						Header = objectName,
						ProgramText = programText,
						Line = item.Line,
						ProgramIdentifier = objectIdentifier
					};

				_stackTrace.Add(debugProgramItem);
			}
		}

		private async Task<string> GetAnonymousPlSqlSources(CancellationToken cancellationToken)
		{
			var lineCount = _sources[PlSqlBlockTitle].Count(c => c == '\n') + 1;
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.GetDebuggerSourceCode;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("LAST_LINE", lineCount, TerminalValues.Number);
			_debuggerSessionCommand.AddSimpleParameter("OUTPUT_CLOB", null, TerminalValues.Clob);
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			return ((OracleClob)_debuggerSessionCommand.Parameters["OUTPUT_CLOB"].Value).Value;
		}

		private async Task<string> GetSources(OracleStackTraceItem activeItem, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = "SELECT TYPE, LINE, TEXT FROM DBA_SOURCE WHERE OWNER = :OWNER AND NAME = :NAME AND TYPE IN ('FUNCTION', 'JAVA SOURCE', 'LIBRARY', 'PACKAGE BODY', 'PROCEDURE', 'TRIGGER', 'TYPE BODY', 'TYPE') ORDER BY TYPE, LINE";
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("OWNER", activeItem.Owner);
			_debuggerSessionCommand.AddSimpleParameter("NAME", activeItem.Name);

			using (var reader = await _debuggerSessionCommand.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
			{
				var sourceBuilder = new StringBuilder("CREATE ");
				while (await reader.ReadAsynchronous(cancellationToken))
				{
					sourceBuilder.Append((string)reader["TEXT"]);
				}

				return sourceBuilder.ToString();
			}
		}

		private static OracleRuntimeInfo GetRuntimeInfo(OracleCommand command)
		{
			var namespaceRaw = GetNullableValueFromOracleDecimal(command.Parameters["NAMESPACE"]);
			var libraryUnitType = GetNullableValueFromOracleDecimal(command.Parameters["LIBUNITTYPE"]);
			var isTerminatedRaw = GetNullableValueFromOracleDecimal(command.Parameters["TERMINATED"]);

			return
				new OracleRuntimeInfo
				{
					Reason = (OracleDebugReason)GetValueFromOracleDecimal(command.Parameters["REASON"]),
					BreakpointNumber = GetNullableValueFromOracleDecimal(command.Parameters["BREAKPOINT"]),
					InterpreterDepth = GetNullableValueFromOracleDecimal(command.Parameters["INTERPRETERDEPTH"]),
					StackDepth = GetNullableValueFromOracleDecimal(command.Parameters["STACKDEPTH"]),
					IsTerminated = isTerminatedRaw == null ? (bool?)null : Convert.ToBoolean(isTerminatedRaw.Value),
					OerException = GetNullableValueFromOracleDecimal(command.Parameters["OER"]),
					SourceLocation =
						new OracleProgramInfo
						{
							LineNumber = GetNullableValueFromOracleDecimal(command.Parameters["LINE"]),
							DatabaseLink = GetValueFromOracleString(command.Parameters["DBLINK"]),
							EntryPointName = GetValueFromOracleString(command.Parameters["ENTRYPOINTNAME"]),
							Owner = GetValueFromOracleString(command.Parameters["OWNER"]),
							Name = GetValueFromOracleString(command.Parameters["NAME"]),
							Namespace = namespaceRaw == null ? null : (OracleDebugProgramNamespace?)namespaceRaw.Value,
							LibraryUnitType = libraryUnitType == null ? null : (OracleLibraryUnitType?)libraryUnitType
						}
				};
		}

		private static string GetValueFromOracleString(IDataParameter parameter)
		{
			var value = (OracleString)parameter.Value;
			return value.IsNull ? String.Empty : value.Value;
		}

		private static int GetValueFromOracleDecimal(IDataParameter parameter)
		{
			var nullableValue = GetNullableValueFromOracleDecimal(parameter);
			if (nullableValue == null)
			{
				throw new InvalidOperationException($"Parameter '{parameter.ParameterName}' must be not null. ");
			}

			return nullableValue.Value;
		}

		private static int? GetNullableValueFromOracleDecimal(IDataParameter parameter)
		{
			var value = (OracleDecimal)parameter.Value;
			return value.IsNull ? (int?)null : Convert.ToInt32(value.Value);
		}

		public Task<BreakpointActionResult> SetBreakpoint(object programInfo, int line, CancellationToken cancellationToken)
		{
			return SetBreakpoint((OracleObjectIdentifier)programInfo, line, cancellationToken);
		}

		public async Task<BreakpointActionResult> SetBreakpoint(OracleObjectIdentifier objectIdentifier, int line, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.SetDebuggerBreakpoint;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("OWNER", objectIdentifier.HasOwner ? objectIdentifier.NormalizedOwner.Trim('"') : null);
			_debuggerSessionCommand.AddSimpleParameter("NAME", String.IsNullOrEmpty(objectIdentifier.Name) ? null : objectIdentifier.NormalizedName.Trim('"'));
			_debuggerSessionCommand.AddSimpleParameter("LINE", line);
			var breakpointIdentifierParameter = _debuggerSessionCommand.AddSimpleParameter("BREAKPOINT_IDENTIFIER", null, TerminalValues.Number);
			var resultParameter = _debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);

			_debuggerConnection.ActionName = "Set breakpoint";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var result = (OracleBreakpointFunctionResult)GetValueFromOracleDecimal(resultParameter);
			var breakpointIdentifier = GetNullableValueFromOracleDecimal(breakpointIdentifierParameter);

			Trace.WriteLine($"Breakpoint '{breakpointIdentifier}' set ({result}). ");
			return
				new BreakpointActionResult
				{
					BreakpointIdentifier = breakpointIdentifier,
					IsSuccessful = result == OracleBreakpointFunctionResult.Success
				};
		}

		public async Task<BreakpointActionResult> EnableBreakpoint(object breakpointIdentifier, CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Enable breakpoint";
			return await ConfigureBreakpoint(breakpointIdentifier, "enable", cancellationToken);
		}

		public async Task<BreakpointActionResult> DisableBreakpoint(object breakpointIdentifier, CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Disable breakpoint";
			return await ConfigureBreakpoint(breakpointIdentifier, "disable", cancellationToken);
		}

		public async Task<BreakpointActionResult> DeleteBreakpoint(object breakpointIdentifier, CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Delete breakpoint";
			return await ConfigureBreakpoint(breakpointIdentifier, "delete", cancellationToken);
		}

		private async Task<BreakpointActionResult> ConfigureBreakpoint(object breakpointIdentifier, string option, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = $"BEGIN :result := dbms_debug.{option}_breakpoint(breakpoint => :breakpoint_identifier); END;";
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("BREAKPOINT_IDENTIFIER", breakpointIdentifier);
			var resultParameter = _debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var result = (OracleBreakpointFunctionResult)GetValueFromOracleDecimal(resultParameter);

			Trace.WriteLine($"Breakpoint '{breakpointIdentifier}' {option}d ({result}). ");

			return
				new BreakpointActionResult
				{
					BreakpointIdentifier = breakpointIdentifier,
					IsSuccessful = result == OracleBreakpointFunctionResult.Success
				};
		}

		private OracleDebugBreakFlags GetEffectiveOracleDebugBreakFlags(OracleDebugBreakFlags baseFlags)
		{
			return BreakOnExceptions ? baseFlags | OracleDebugBreakFlags.Exception : baseFlags;
		}

		public Task Continue(CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Continue";
			return ContinueAndDetachIfTerminated(GetEffectiveOracleDebugBreakFlags(OracleDebugBreakFlags.None), cancellationToken);
		}

		public Task StepOver(CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Step next line";
			return ContinueAndDetachIfTerminated(GetEffectiveOracleDebugBreakFlags(OracleDebugBreakFlags.NextLine), cancellationToken);
		}

		public async Task StepInto(CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Step into";
			await ContinueAndDetachIfTerminated(GetEffectiveOracleDebugBreakFlags(OracleDebugBreakFlags.AnyCall), cancellationToken);
		}

		public Task StepOut(CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Step out";
			return ContinueAndDetachIfTerminated(GetEffectiveOracleDebugBreakFlags(OracleDebugBreakFlags.AnyReturn), cancellationToken);
		}

		public Task Abort(CancellationToken cancellationToken)
		{
			_debuggerConnection.ActionName = "Abort";
			return ContinueAndDetachIfTerminated(OracleDebugBreakFlags.AbortExecution, cancellationToken);
		}

		private void TerminateTargetSessionDebugMode()
		{
			_debuggedSessionCommand.CommandText = OracleDatabaseCommands.FinalizeDebuggee;
			_debuggedSessionCommand.Parameters.Clear();
			_debuggedSessionCommand.ExecuteNonQuery();

			Trace.WriteLine("Target session debug mode terminated. ");
		}

		private async Task Detach(CancellationToken cancellationToken)
		{
			var taskDebugOff = _debuggedAction.ContinueWith(t => TerminateTargetSessionDebugMode(), cancellationToken);

			await Synchronize(cancellationToken);
			await Continue(OracleDebugBreakFlags.None, cancellationToken);
			await taskDebugOff;

			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.DetachDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerConnection.ActionName = "Detach";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			Trace.WriteLine("Debugger detached from target session. ");

			var statementResult = ExecutionResult.StatementResults[0];
			if (_debuggedAction.Status == TaskStatus.RanToCompletion)
			{
				statementResult.AffectedRowCount = _debuggedAction.Result;
			}

			statementResult.Duration = DateTime.Now - statementResult.ExecutedAt;
			await _connectionAdapter.FinalizeBatchExecution(ExecutionResult, cancellationToken);

			RaiseDetached();
		}

		private void RaiseDetached()
		{
			Detached?.Invoke(this, EventArgs.Empty);
		}

		public void Dispose()
		{
			Attached = null;
			Detached = null;

			DebuggedCommand.Dispose();
			_debuggedSessionCommand.Dispose();
			_debuggerSessionCommand.Dispose();
			_debuggerConnection.Dispose();

			Trace.WriteLine("Debugger disposed. ");
		}
	}

	[DebuggerDisplay("OracleRuntimeInfo (Reason={Reason}; IsTerminated={IsTerminated})")]
	internal struct OracleRuntimeInfo
	{
		public int? OerException { get; set; }

		public int? BreakpointNumber { get; set; }

		public int? StackDepth { get; set; }

		public int? InterpreterDepth { get; set; }

		public OracleDebugReason Reason { get; set; }

		public OracleProgramInfo SourceLocation { get; set; }

		public bool? IsTerminated { get; set; }

		public void Trace()
		{
			System.Diagnostics.Trace.WriteLine($"OerException = {OerException}; BreakpointNumber = {BreakpointNumber}; StackDepth = {StackDepth}; InterpreterDepth = {InterpreterDepth}; Reason = {Reason}; IsTerminated = {IsTerminated}");
			SourceLocation.Trace();
		}
	}

	public struct OracleProgramInfo
	{
		public int? LineNumber { get; set; }

		public string DatabaseLink { get; set; }

		public string EntryPointName { get; set; }

		public string Owner { get; set; }

		public string Name { get; set; }

		public OracleDebugProgramNamespace? Namespace { get; set; }

		public OracleLibraryUnitType? LibraryUnitType { get; set; }

		public void Trace()
		{
			System.Diagnostics.Trace.WriteLine($"LineNumber = {LineNumber}; DatabaseLink = {DatabaseLink}; EntryPointName = {EntryPointName}; Owner = {Owner}; Name = {Name}; Namespace = {Namespace}; LibraryUnitType = {LibraryUnitType}");
		}
	}

	[XmlRoot("Items")]
	public class OracleStackTrace
	{
		[XmlElement("Item")]
		public OracleStackTraceItem[] Items { get; set; }
	}

	public class OracleStackTraceItem
	{
		[XmlElement("Owner")]
		public string Owner { get; set; }

		[XmlElement("Name")]
		public string Name { get; set; }

		[XmlElement("DatabaseLink")]
		public string DatabaseLink { get; set; }

		[XmlElement("Line")]
		public int Line { get; set; }
		
		[XmlElement("Namespace")]
		public int Namespace { get; set; }

		[XmlElement("LibraryUnitType")]
		public int LibraryUnitType { get; set; }
	}

	public enum OracleDebugReason
	{
		None = 0,
		InterpreterStarting = 2,
		Breakpoint = 3,

		/// <summary>
		/// procedure entered
		/// </summary>
		Enter = 6,
		Return = 7,
		Finish = 8,

		/// <summary>
		/// reached a new line
		/// </summary>
		Line = 9,
		Interrupt = 10,
		Exception = 11,

		/// <summary>
		/// interpreter is exiting
		/// </summary>
		Exit = 15,

		/// <summary>
		/// start exception-handler
		/// </summary>
		Handler = 16,
		Timeout = 17,

		/// <summary>
		/// instantiation block
		/// </summary>
		Instantiate = 20,

		/// <summary>
		/// interpeter is abortings
		/// </summary>
		Abort = 21,

		/// <summary>
		/// interpreter is exiting
		/// </summary>
		KnlExit = 25,

		// Not yet supported:
		/// <summary>
		/// executing SQL
		/// </summary>
		Sql = 4,

		/// <summary>
		/// watched value changed
		/// </summary>
		Watch = 14,

		/// <summary>
		/// an RPC started
		/// </summary>
		Rpc = 18,

		/// <summary>
		/// unhandled exception
		/// </summary>
		Unhandled = 19,

		// Reserved internal values:
		InternalBootstrappingInit = 1,
		Unused = 5,
		IcdCall = 12,
		IcdReturn = 13,

		// Added in Probe v2.4
		OerBreakpoint = 26
	}

	public enum OracleDebugActionResult
	{
		Success = 0,
		ErrorCommunication = 29,
		ErrorTimeout = 31
	}

	public enum OracleBreakpointStatus
	{
		Unused,
		Active,
		Disable,
		Remote
	}

	public enum OracleLibraryUnitType
	{
		Cursor = 0,
		Procedure = 7,
		Function = 8,
		Package = 9,
		PackageBody = 11,
		Trigger = 12,
		Unknown = -1
	}

	[Flags]
	public enum OracleCursorFlags
	{
		None = 0,
		Open = 1,
		Found = 2,
		NotFound = 4,
		Uninitialized = 32,
		Recursive = 128,
		RefCursorBindVariable = 1024,
		DynamicSql = 2048,
		FirstIteration = 4096,
		DynamicOpen = 16384
	}

	[Flags]
	public enum OracleDebugBreakFlags
	{
		None = 0,
		Exception = 2,
		Call = 4, // reserved
		XCall = 8, // reserved
		AnyCall = 12,
		Return = 16,
		NextLine = 32,
		IcdEnter = 64, // reserved
		IcdExit = 128, // reserved
		ControlC = 256, // not supported
		AnyReturn = 512,
		ExceptionHandler = 2048,
		Rpc = 4096, // not supported
		AbortExecution = 8192
	}

	public enum OracleDebugProgramNamespace
	{
		Cursor = 0,
		PackageSpecificationOrTopLevel = 1,
		PackageBody = 2,
		Trigger = 3,
		None = 255
	}

	public enum OracleBreakpointFunctionResult
	{
		Success = 0,
		ErrorIllegalLine = 12,
		ErrorBadHandle = 16 // can't set breakpoint there
	}

	public enum ValueInfoStatus
	{
		Success = 0,
		ErrorBogusFrame = 1, // No such frame
		ErrorNoDebugInfo = 2, // Debug info missing
		ErrorNoSuchObject = 3, // No such variable/parameter
		ErrorUnknownType = 4, // Debug info garbled
		ErrorIllegalValue = 5, // Constraint violation
		ErrorIllegalNull = 6, // Constraint violation
		ErrorValueMalformed = 7, // Bad value
		ErrorOther = 8, // Unknown error
		ErrorNameIncomplete = 11, // not a scalar value
		ErrorIndexedTable = 18, // Cannot get/set an entire collection at once
		ErrorIllegalIndex = 19, // Illegal collection index
		ErrorNullValue = 32, // Value is null
		ErrorNullCollection = 40 // Collection is atomically null
	}
}
