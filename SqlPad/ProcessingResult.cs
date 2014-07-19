using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("ProcessingResult (Status={Status}, TerminalCount={System.Linq.Enumerable.Count(Terminals)})")]
	public struct ProcessingResult
	{
		public ProcessingStatus Status { get; set; }

		public string NodeId { get; set; }
		
		public IList<StatementDescriptionNode> Nodes { get; set; }

		public IList<StatementDescriptionNode> BestCandidates { get; set; }
	}
}