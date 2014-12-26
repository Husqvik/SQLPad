using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("ParseResult (Status={Status}{Nodes == null ? \"\" : \", Nodes=\" + System.Linq.Enumerable.Count(Nodes), nq})")]
	public struct ParseResult
	{
		public ParseStatus Status;

		public string NodeId;

		public IList<StatementGrammarNode> Nodes;

		public IList<StatementGrammarNode> BestCandidates;

		public int TerminalCount;

		public int BestCandidateTerminalCount;
	}
}