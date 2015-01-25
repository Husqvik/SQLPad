using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif

namespace SqlPad.Oracle
{
	public class OracleDebuggerSession : IDebuggerSession, IDisposable
	{
		private readonly OracleCommand _debuggedSessionCommand;
		private readonly OracleConnection _debuggerSession;
		private readonly OracleCommand _debuggerSessionCommand;

		private string _debuggerSessionId;

		public OracleDebuggerSession(OracleConnection debuggedSession)
		{
			_debuggedSessionCommand = debuggedSession.CreateCommand();
			_debuggedSessionCommand.BindByName = true;
			_debuggerSession = (OracleConnection)debuggedSession.Clone();
			_debuggerSessionCommand = _debuggerSession.CreateCommand();
			_debuggerSessionCommand.BindByName = true;
		}

		public void Start()
		{
			_debuggedSessionCommand.CommandText = DatabaseCommands.StartDebuggee;
			_debuggedSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", null, OracleBindVariable.DataTypeVarchar2, 12);
			var debuggerSessionIdParameter = _debuggedSessionCommand.Parameters[0];

			_debuggedSessionCommand.ExecuteNonQuery();
			_debuggerSessionId = ((OracleString)debuggerSessionIdParameter.Value).Value;

			var startDebuggerTask = StartDebugger(CancellationToken.None);
			
			var continueDebuggeeTask = ContinueDebuggee(CancellationToken.None);

			startDebuggerTask.Wait();
			var runtimeInfo = startDebuggerTask.Result;

			do
			{
				var task = ContinueDebugger(OracleDebugBreakFlags.AnyCall, CancellationToken.None);
				task.Wait();

				runtimeInfo = task.Result;

				var aValue = GetValue("A");
			} while (runtimeInfo.Reason != OracleDebugReason.KnlExit);

			Detach();
		}

		private void AddDebugParameters(OracleCommand command)
		{
			command.AddSimpleParameter("DEBUG_ACTION_STATUS", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("BREAKPOINT", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("INTERPRETERDEPTH", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("LINE", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("OER", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("REASON", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("STACKDEPTH", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("TERMINATED", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("DBLINK", null, OracleBindVariable.DataTypeVarchar2, 30);
			command.AddSimpleParameter("ENTRYPOINTNAME", null, OracleBindVariable.DataTypeVarchar2, 512);
			command.AddSimpleParameter("OWNER", null, OracleBindVariable.DataTypeVarchar2, 30);
			command.AddSimpleParameter("NAME", null, OracleBindVariable.DataTypeVarchar2, 30);
			command.AddSimpleParameter("NAMESPACE", null, OracleBindVariable.DataTypeNumber);
			command.AddSimpleParameter("LIBUNITTYPE", null, OracleBindVariable.DataTypeNumber);
		}

		private object GetValue(string name)
		{
			_debuggerSessionCommand.CommandText = DatabaseCommands.DebuggerGetValue;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("RESULT", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("NAME", name, OracleBindVariable.DataTypeVarchar2);
			_debuggerSessionCommand.AddSimpleParameter("VALUE", null, OracleBindVariable.DataTypeVarchar2, 32767);

			_debuggerSessionCommand.ExecuteNonQuery();

			return GetValueFromOracleString(_debuggerSessionCommand.Parameters["VALUE"]);
		}

		private async Task ContinueDebuggee(CancellationToken cancellationToken)
		{
			_debuggedSessionCommand.CommandText =
@"BEGIN
	TESTPROC;
END;";

			_debuggedSessionCommand.Parameters.Clear();

			await _debuggedSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
		}

		private async Task<OracleRuntimeInfo> ContinueDebugger(OracleDebugBreakFlags breakFlags, CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = DatabaseCommands.ContinueDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.AddSimpleParameter("BREAK_FLAGS", (int)breakFlags, OracleBindVariable.DataTypeNumber);
			AddDebugParameters(_debuggerSessionCommand);

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);
			return GetRuntimeInfo(_debuggerSessionCommand);
		}

		private async Task<OracleRuntimeInfo> StartDebugger(CancellationToken cancellationToken)
		{
			_debuggerSessionCommand.CommandText = DatabaseCommands.StartDebugger;
			_debuggerSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", _debuggerSessionId);
			AddDebugParameters(_debuggerSessionCommand);

			_debuggerSession.Open();
			_debuggerSession.ModuleName = "SQLPad PL/SQL Debugger";
			_debuggerSession.ActionName = "Attach";

			await _debuggerSessionCommand.ExecuteNonQueryAsynchronous(cancellationToken);

			return GetRuntimeInfo(_debuggerSessionCommand);
		}

		private OracleRuntimeInfo GetRuntimeInfo(OracleCommand command)
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

		private string GetValueFromOracleString(OracleParameter parameter)
		{
			var value = (OracleString)parameter.Value;
			return value.IsNull ? null : value.Value;
		}

		private int GetValueFromOracleDecimal(OracleParameter parameter)
		{
			var nullableValue = GetNullableValueFromOracleDecimal(parameter);
			if (nullableValue == null)
			{
				throw new InvalidOperationException(String.Format("Parameter '{0}' must be not null. ", parameter.ParameterName));
			}

			return nullableValue.Value;
		}

		private int? GetNullableValueFromOracleDecimal(OracleParameter parameter)
		{
			var value = (OracleDecimal)parameter.Value;
			return value.IsNull ? (int?)null : Convert.ToInt32(value.Value);
		}

		public void Continue()
		{
			
		}

		public void StepNextLine()
		{

		}

		public void StepInto()
		{

		}

		public void StepOut()
		{

		}

		public void Detach()
		{
			_debuggerSessionCommand.CommandText = DatabaseCommands.DetachDebugger;
			_debuggerSessionCommand.Parameters.Clear();
			_debuggerSessionCommand.ExecuteNonQuery();
		}

		public void Dispose()
		{
			_debuggedSessionCommand.Dispose();
			_debuggerSessionCommand.Dispose();
			_debuggerSession.Dispose();
		}
	}

	[DebuggerDisplay("OracleRuntimeInfo (Reason={Reason}; IsTerminated={IsTerminated})")]
	public struct OracleRuntimeInfo
	{
		public int? OerException { get; set; }
		
		public int? BreakpointNumber { get; set; }
		
		public int? StackDepth { get; set; }
		
		public int? InterpreterDepth { get; set; }

		public OracleDebugReason Reason { get; set; }

		public OracleProgramInfo SourceLocation { get; set; }
		
		public bool? IsTerminated { get; set; }
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
}
