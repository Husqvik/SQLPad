using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (Count={NodeCollection.Count})")]
	public class OracleStatement : IStatement
	{
		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingStatus = ProcessingStatus.Success,
				NodeCollection = new StatementDescriptionNode[0],
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};

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
