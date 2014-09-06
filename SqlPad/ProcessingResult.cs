using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("ProcessingResult (Status={Status}{Nodes == null ? \"\" : \", Nodes=\" + System.Linq.Enumerable.Count(Nodes), nq})")]
	public struct ProcessingResult
	{
		public ProcessingStatus Status { get; set; }

		public string NodeId { get; set; }
		
		public IList<StatementGrammarNode> Nodes { get; set; }

		public IList<StatementGrammarNode> BestCandidates { get; set; }
	}
}