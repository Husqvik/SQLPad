using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
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
		
		public int Level { get; set; }

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
					indexEnd = Value.Index + Value.Value.Length - 1;
				}
				else if (Terminals.Any())
				{
					indexStart = Terminals.First().Value.Index;
					var lastTerminal = Terminals.Last().Value;
					indexEnd = lastTerminal.Index + lastTerminal.Value.Length - 1;
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

		public IEnumerable<StatementDescriptionNode> GetDescendants(params string[] descendantNodeIds)
		{
			return AllChildNodes.Where(t => descendantNodeIds == null || descendantNodeIds.Length == 0 || descendantNodeIds.Contains(t.Id));
		}

		/*public IEnumerable<StatementDescriptionNode> GetDescendants(string startingNodeId, params string[] descendantNodeIds)
		{
			var startingNode = AllChildNodes.FirstOrDefault(t => t.Id == startingNodeId);
			return startingNode == null
				? Enumerable.Empty<StatementDescriptionNode>()
				: startingNode.GetDescendants(descendantNodeIds);
		}*/

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

		public StatementDescriptionNode GetNodeAtPosition(int offset)
		{
			if (SourcePosition.IndexEnd < offset || SourcePosition.IndexStart > offset)
				return null;

			return AllChildNodes.Where(t => t.SourcePosition.IndexStart <= offset && t.SourcePosition.IndexEnd >= offset)
				.OrderBy(n => n.SourcePosition.IndexEnd - n.SourcePosition.IndexStart).ThenByDescending(n => n.Level)
				.FirstOrDefault();
		}
	}
}