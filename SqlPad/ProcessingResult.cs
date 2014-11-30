using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("ProcessingResult (Status={Status}{Nodes == null ? \"\" : \", Nodes=\" + System.Linq.Enumerable.Count(Nodes), nq})")]
	public struct ProcessingResult
	{
		public ProcessingStatus Status;

		public string NodeId;

		public IList<StatementGrammarNode> Nodes;

		public IList<StatementGrammarNode> BestCandidates;

		public int TerminalCount;

		public int BestCandidateTerminalCount;
	}
}