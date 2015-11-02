using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IDebuggerSession
	{
		int? ActiveLine { get; }

		bool BreakOnExceptions { get; set; }

		event EventHandler Attached;

		event EventHandler Detached;

		IReadOnlyList<DebugProgramItem> StackTrace { get; }

		DatabaseExceptionInformation CurrentException { get; }

		Task Start(CancellationToken cancellationToken);

		Task Continue(CancellationToken cancellationToken);

		Task StepOver(CancellationToken cancellationToken);

		Task StepInto(CancellationToken cancellationToken);

		Task StepOut(CancellationToken cancellationToken);

		Task Abort(CancellationToken cancellationToken);

		Task GetValue(WatchItem watchItem, CancellationToken cancellationToken);

		Task SetValue(WatchItem watchItem, CancellationToken cancellationToken);

		Task<BreakpointActionResult> SetBreakpoint(object programInfo, int line, CancellationToken cancellationToken);

		Task<BreakpointActionResult> DeleteBreakpoint(object breakpointIdentifier, CancellationToken cancellationToken);

		Task<BreakpointActionResult> EnableBreakpoint(object breakpointIdentifier, CancellationToken cancellationToken);

		Task<BreakpointActionResult> DisableBreakpoint(object breakpointIdentifier, CancellationToken cancellationToken);
	}

	public class DebugProgramItem
	{
		public string Header { get; set; }

		public string ProgramText { get; set; }

		public int Line { get; set; }

		public object ProgramIdentifier { get; set; }
	}

	public struct BreakpointActionResult
	{
		public object BreakpointIdentifier;

		public bool IsSuccessful;
	}

	public class DatabaseExceptionInformation
	{
		public int ErrorCode { get; set; }

		public string ErrorMessage { get; set; }
	}
}
