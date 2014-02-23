using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("OracleStatement (Count={TokenCollection.Count})")]
	public class OracleStatement
	{
		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingResult = NonTerminalProcessingResult.Success,
				TokenCollection = new StatementDescriptionNode[0],
				SourcePosition = new SourcePosition { IndexStart = 0, IndexEnd = 0 }
			};

		public NonTerminalProcessingResult ProcessingResult { get; set; }

		public ICollection<StatementDescriptionNode> TokenCollection { get; set; }

		public SourcePosition SourcePosition { get; set; }

		public StatementDescriptionNode GetNodeAtPosition(int offset)
		{
			return TokenCollection.Select(n => n.GetNodeAtPosition(offset)).FirstOrDefault(n => n != null);
		}
	}

	[DebuggerDisplay("SourcePosition (IndexStart={IndexStart}, IndexEnd={IndexEnd})")]
	public struct SourcePosition
	{
		public int IndexStart { get; set; }
		public int IndexEnd { get; set; }
	}

	public enum NodeType
	{
		Terminal,
		NonTerminal
	}
}
