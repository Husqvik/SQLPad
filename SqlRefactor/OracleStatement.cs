using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlRefactor
{
	[DebuggerDisplay("OracleStatement (Count={TokenCollection.Count})")]
	public class OracleStatement
	{
		public static readonly OracleStatement EmptyStatement = new OracleStatement { ProcessingResult = NonTerminalProcessingResult.Success, TokenCollection = new StatementDescriptionNode[0], SourcePosition = new SourcePosition { IndexStart = 0, IndexEnd = 0 } };

		public NonTerminalProcessingResult ProcessingResult { get; set; }

		public ICollection<StatementDescriptionNode> TokenCollection { get; set; }

		public SourcePosition SourcePosition { get; set; }
	}

	[DebuggerDisplay("{ToString()}")]
	public class StatementDescriptionNode
	{
		public StatementDescriptionNode()
		{
			ChildTokens = new List<StatementDescriptionNode>();
		}

		public NodeType Type { get; set; }

		public StatementDescriptionNode ParentDescriptionNode { get; set; }
		
		public OracleToken Value { get; set; }
		
		public string Id { get; set; }

		public ICollection<StatementDescriptionNode> ChildTokens { get; set; }

		public SourcePosition SourcePosition
		{
			get
			{
				var indexStart = -1;
				var indexEnd = -1;
				if (Type == NodeType.Terminal)
				{
					indexStart = Value.Index;
					indexEnd = Value.Index + Value.Value.Length;
				}
				else if (Terminals.Any())
				{
					indexStart = Terminals.First().Value.Index;
					var lastTerminal = Terminals.Last().Value;
					indexEnd = lastTerminal.Index + lastTerminal.Value.Length;
				}

				return new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
			}
		}

		#region Overrides of Object
		public override string ToString()
		{
			return string.Format("StatementDescriptionNode (Id={0}; Type={1})", Id, Type);
		}
		#endregion

		public int TerminalCount
		{
			get { return Terminals.Count(); }
		}
		
		public IEnumerable<StatementDescriptionNode> Terminals 
		{
			get { return Type == NodeType.Terminal ? Enumerable.Repeat(this, 1) : ChildTokens.SelectMany(t => t.Terminals); }
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
