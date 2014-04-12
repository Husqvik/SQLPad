using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public abstract class StatementBase
	{
		public ProcessingStatus ProcessingStatus { get; set; }

		public ICollection<StatementDescriptionNode> NodeCollection { get; set; }
		
		public ICollection<string> TerminalCandidates { get; set; }

		public SourcePosition SourcePosition { get; set; }
		
		public StatementDescriptionNode GetNodeAtPosition(int offset)
		{
			return NodeCollection.Select(n => n.GetNodeAtPosition(offset)).FirstOrDefault(n => n != null);
		}

		public StatementDescriptionNode GetNearestTerminalToPosition(int offset)
		{
			return NodeCollection.Select(n => n.GetNearestTerminalToPosition(offset)).FirstOrDefault(n => n != null);
		}
	}
}
