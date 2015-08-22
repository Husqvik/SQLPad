using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Data;
using SqlPad.Oracle.DataDictionary;
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

		private readonly OracleCommand _debuggedSessionCommand;
		private readonly OracleConnection _debuggerSession;
		private readonly OracleCommand _debuggerSessionCommand;
		private readonly Task _debuggedAction;

		private OracleRuntimeInfo _runtimeInfo;
		private string _debuggerSessionId;

		public event EventHandler Detached;

		public OracleObjectIdentifier ActiveObject => OracleObjectIdentifier.Create(_runtimeInfo.SourceLocation.Owner, _runtimeInfo.SourceLocation.Name);

		public int? ActiveLine => _runtimeInfo.SourceLocation.LineNumber;

		public OracleDebuggerSession(OracleConnection debuggedSession, Task debuggedAction)
		{
			_debuggedSessionCommand = debuggedSession.CreateCommand();
			_debuggedSessionCommand.BindByName = true;
			_debuggerSession = (OracleConnection)debuggedSession.Clone();
			_debuggerSessionCommand = _debuggerSession.CreateCommand();
			_debuggerSessionCommand.BindByName = true;
			_debuggedAction = debuggedAction;
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

		public async Task<string> GetStackTrace(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.GetDebuggerStackTrace;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("OUTPUT_CLOB", null, TerminalValues.Clob);
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			return ((OracleClob)_debuggerSessionCommand.Parameters[0].Value).Value;
		}

		public async Task Start(CancellationToken cancellationToken)
		{
			_debuggedSessionCommand.CommandText = OracleDatabaseCommands.StartDebuggee;
			_debuggedSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", null, TerminalValues.Varchar2, 12);
			var debuggedSessionIdParameter = _debuggedSessionCommand.Parameters[0];

			_debuggedSessionCommand.ExecuteNonQuery();
			_debuggerSessionId = ((OracleString)debuggedSessionIdParameter.Value).Value;

			Trace.WriteLine($"Target debug session initialized. Debug session ID = {_debuggerSessionId}");

			await Attach(cancellationToken);
			Trace.WriteLine("Debugger attached. ");

			using (var startDebuggerTask = Synchronize(cancellationToken))
			{
				_debuggedAction.Start();
				Trace.WriteLine("Debugged action started. ");

				startDebuggerTask.Wait(cancellationToken);
				Trace.WriteLine("Debugger synchronized. ");
				_runtimeInfo = startDebuggerTask.Result;
				_runtimeInfo.Trace();
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

		private object GetValue(string name)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.DebuggerGetValue;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);
			_debuggerSessionCommand.AddSimpleParameter("NAME", name, TerminalValues.Varchar2);
			_debuggerSessionCommand.AddSimpleParameter("VALUE", null, TerminalValues.Varchar2, 32767);

			_debuggerSessionCommand.ExecuteNonQuery();

			return GetValueFromOracleString(_debuggerSessionCommand.Parameters["VALUE"]);
		}

		private async Task ContinueAndDetachIfTerminated(OracleDebugBreakFlags breakFlags, CancellationToken cancellationToken)
		{
			await Continue(breakFlags, cancellationToken);

			_runtimeInfo.Trace();

			if (_runtimeInfo.IsTerminated == true)
			{
				await Detach(cancellationToken);
			}
		}

		private async Task Continue(OracleDebugBreakFlags breakFlags, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.ContinueDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("BREAK_FLAGS", (int)breakFlags, TerminalValues.Number);
			AddDebugParameters(_debuggerSessionCommand);

			_debuggerSession.ActionName = "Continue";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var status = (OracleDebugActionResult)GetValueFromOracleDecimal(_debuggerSessionCommand.Parameters[ParameterDebugActionStatus]);

			_runtimeInfo = GetRuntimeInfo(_debuggerSessionCommand);
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

			_debuggerSession.ActionName = "Get line map";
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

			_debuggerSession.Open();
			_debuggerSession.ModuleName = "SQLPad PL/SQL Debugger";
			_debuggerSession.ActionName = "Attach";

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
		}

		private async Task<OracleRuntimeInfo> Synchronize(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.SynchronizeDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			AddDebugParameters(_debuggerSessionCommand);

			_debuggerSession.ActionName = "Synchronize";

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			return GetRuntimeInfo(_debuggerSessionCommand);
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
			return value.IsNull ? null : value.Value;
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

		public async Task SetBreakpoint(OracleObjectIdentifier objectIdentifier, int line, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.SetDebuggerBreakpoint;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("OWNER", objectIdentifier.HasOwner ? objectIdentifier.NormalizedOwner.Trim('"') : null);
			_debuggerSessionCommand.AddSimpleParameter("NAME", String.IsNullOrEmpty(objectIdentifier.Name) ? null : objectIdentifier.NormalizedName.Trim('"'));
			_debuggerSessionCommand.AddSimpleParameter("LINE", line);
			var breakpointIdentifierParameter = _debuggerSessionCommand.AddSimpleParameter("BREAKPOINT_IDENTIFIER", null, TerminalValues.Number);
			var resultParameter = _debuggerSessionCommand.AddSimpleParameter("RESULT", null, TerminalValues.Number);

			_debuggerSession.ActionName = "Set breakpoint";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			var result = (OracleBreakpointFunctionResult)GetValueFromOracleDecimal(resultParameter);
			var breakpointIdentifier = GetNullableValueFromOracleDecimal(breakpointIdentifierParameter);

			Trace.WriteLine($"Breakpoint '{breakpointIdentifier}' set ({result}). ");
		}

		public Task Continue(CancellationToken cancellationToken)
		{
			_debuggerSession.ActionName = "Continue";
			return ContinueAndDetachIfTerminated(OracleDebugBreakFlags.Exception, cancellationToken);
		}

		public Task StepNextLine(CancellationToken cancellationToken)
		{
			_debuggerSession.ActionName = "Step next line";
			return ContinueAndDetachIfTerminated(OracleDebugBreakFlags.NextLine, cancellationToken);
		}

		public async Task StepInto(CancellationToken cancellationToken)
		{
			_debuggerSession.ActionName = "Step into";
			await ContinueAndDetachIfTerminated(OracleDebugBreakFlags.AnyCall, cancellationToken);

			// remove
			if (_runtimeInfo.IsTerminated != true)
			{
				var aValue = GetValue("A");
				Trace.WriteLine("A = " + aValue);
				//Trace.WriteLine("Stack trace: \n" + await GetStackTrace(cancellationToken));
				//Trace.WriteLine("Is running:" + await IsRunning(cancellationToken));
			}
		}

		public Task StepOut(CancellationToken cancellationToken)
		{
			_debuggerSession.ActionName = "Step out";
			return ContinueAndDetachIfTerminated(OracleDebugBreakFlags.AnyReturn, cancellationToken);
		}

		private void TerminateTargetSessionDebugMode()
		{
			_debuggedSessionCommand.CommandText = OracleDatabaseCommands.FinalizeDebuggee;
			_debuggedSessionCommand.Parameters.Clear();
			_debuggedSessionCommand.ExecuteNonQuery();

			Trace.WriteLine("Target session debug mode terminated. ");
		}

		public async Task Detach(CancellationToken cancellationToken)
		{
			var taskDebugOff = _debuggedAction.ContinueWith(t => TerminateTargetSessionDebugMode(), cancellationToken);

			await Synchronize(cancellationToken);
			await Continue(OracleDebugBreakFlags.None, cancellationToken);

			taskDebugOff.Wait(cancellationToken);

			_debuggerSessionCommand.CommandText = OracleDatabaseCommands.DetachDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSession.ActionName = "Detach";
			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			Trace.WriteLine("Debugger detached from target session. ");

			Detached?.Invoke(this, EventArgs.Empty);
		}

		public void Dispose()
		{
			_debuggedSessionCommand.Dispose();
			_debuggerSessionCommand.Dispose();
			_debuggerSession.Dispose();

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
		Procedure = 0,
		Function = 0,
		Package = 0,
		PackageBody = 0,
		Trigger = 0,
		Unknown = 0
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
		Handler = 2048,
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
}
