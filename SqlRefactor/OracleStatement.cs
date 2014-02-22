using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlRefactor
{
	[DebuggerDisplay("OracleStatement (Count={TokenCollection.Count})")]
	public class OracleStatement
	{
		public NonTerminalProcessingResult ProcessingResult { get; set; }

		public ICollection<StatementDescriptionNode> TokenCollection { get; set; }
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

	public enum NodeType
	{
		Terminal,
		NonTerminal
	}
}
