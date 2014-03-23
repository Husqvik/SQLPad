using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("ProcessingResult (Status={Status}, TerminalCount={System.Linq.Enumerable.Count(Terminals)})")]
	public struct ProcessingResult
	{
		public ProcessingStatus Status { get; set; }
		
		public IList<StatementDescriptionNode> Nodes { get; set; }
		
		public IList<StatementDescriptionNode> BestCandidates { get; set; }

		public IEnumerable<StatementDescriptionNode> Terminals
		{
			get
			{
				return Nodes == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: Nodes.SelectMany(t => t.Terminals);
			}
		} 
	}
}