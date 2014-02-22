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

		public StatementDescriptionNode GetNodeAtPosition(int offset)
		{
			return TokenCollection.FirstOrDefault(t => t.SourcePosition.IndexStart <= offset && t.SourcePosition.IndexEnd >= offset);
		}
	}

	[DebuggerDisplay("{ToString()}")]
	public class StatementDescriptionNode
	{
		public StatementDescriptionNode()
		{
			ChildNodes = new List<StatementDescriptionNode>();
		}

		public NodeType Type { get; set; }

		public StatementDescriptionNode ParentNode { get; set; }
		
		public OracleToken Value { get; set; }
		
		public string Id { get; set; }

		public ICollection<StatementDescriptionNode> ChildNodes { get; set; }

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
			return string.Format("StatementDescriptionNode (Id={0}; Type={1}; SourcePosition=({2}-{3}))", Id, Type, SourcePosition.IndexStart, SourcePosition.IndexEnd);
		}
		#endregion

		public int TerminalCount
		{
			get { return Terminals.Count(); }
		}
		
		public IEnumerable<StatementDescriptionNode> Terminals 
		{
			get { return Type == NodeType.Terminal ? Enumerable.Repeat(this, 1) : ChildNodes.SelectMany(t => t.Terminals); }
		}

		public IEnumerable<StatementDescriptionNode> AllChildNodes
		{
			get
			{
				return Type == NodeType.Terminal
					? Enumerable.Empty<StatementDescriptionNode>()
					: ChildNodes.Concat(ChildNodes.SelectMany(n => n.AllChildNodes));
			}
		}

		public IEnumerable<StatementDescriptionNode> GetDescendants(string startingNodeId, params string[] descendantNodeIds)
		{
			var startingNode = AllChildNodes.FirstOrDefault(t => t.Id == startingNodeId);
			return startingNode == null
				? Enumerable.Empty<StatementDescriptionNode>()
				: startingNode.AllChildNodes.Where(t => descendantNodeIds.Contains(t.Id));
		}

		public StatementDescriptionNode GetAncestor(string ancestorNodeId, bool includeSelf = true)
		{
			if (includeSelf && Id == ancestorNodeId)
				return this;

			if (ParentNode == null)
				return null;

			return ParentNode.Id == ancestorNodeId
				? ParentNode
				: ParentNode.GetAncestor(ancestorNodeId);
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
