using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("ProcessingResult (Status={Status}, TerminalCount={TerminalCount})")]
	public struct ProcessingResult
	{
		public ProcessingStatus Status { get; set; }
		
		public IList<StatementDescriptionNode> Nodes { get; set; }

		public IEnumerable<StatementDescriptionNode> Terminals
		{
			get
			{
				return Nodes == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: Nodes.SelectMany(t => t.Terminals);
			}
		} 

		public int TerminalCount
		{
			get { return Nodes == null ? 0 : Terminals.Count(); }
		}
	}
}