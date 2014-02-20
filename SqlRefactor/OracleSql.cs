using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlRefactor
{
	[DebuggerDisplay("OracleSql (Count={TokenCollection.Count})")]
	public class OracleSql
	{
		public NonTerminalProcessingResult ProcessingResult { get; set; }

		public ICollection<SqlDescriptionNode> TokenCollection { get; set; }
	}

	[DebuggerDisplay("{ToString()}")]
	public class SqlDescriptionNode
	{
		public SqlDescriptionNode()
		{
			ChildTokens = new List<SqlDescriptionNode>();
		}

		public SqlNodeType Type { get; set; }

		public SqlDescriptionNode ParentDescriptionNode { get; set; }
		
		public OracleToken Value { get; set; }
		
		public string Id { get; set; }

		public ICollection<SqlDescriptionNode> ChildTokens { get; set; }

		#region Overrides of Object
		public override string ToString()
		{
			return string.Format("SqlDescriptionNode (Id={0}; Type={1})", Id, Type);
		}
		#endregion

		public int TerminalCount
		{
			get { return Terminals.Count(); }
		}
		
		public IEnumerable<SqlDescriptionNode> Terminals 
		{
			get { return Type == SqlNodeType.Terminal ? Enumerable.Repeat(this, 1) : ChildTokens.SelectMany(t => t.Terminals); }
		}
	}

	public enum SqlNodeType
	{
		Terminal,
		NonTerminal
	}
}