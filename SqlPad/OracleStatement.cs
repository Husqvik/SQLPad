using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("OracleStatement (Count={NodeCollection.Count})")]
	public class OracleStatement
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

		public SourcePosition SourcePosition { get; set; }
		
		public StatementDescriptionNode GetNodeAtPosition(int offset)
		{
			return NodeCollection.Select(n => n.GetNodeAtPosition(offset)).FirstOrDefault(n => n != null);
		}
	}

	[DebuggerDisplay("SourcePosition (IndexStart={IndexStart}, IndexEnd={IndexEnd})")]
	public struct SourcePosition
	{
		public int IndexStart { get; set; }
		public int IndexEnd { get; set; }
		public int Length { get { return IndexEnd - IndexStart + 1; } }
	}

	public enum NodeType
	{
		Terminal,
		NonTerminal
	}
}
