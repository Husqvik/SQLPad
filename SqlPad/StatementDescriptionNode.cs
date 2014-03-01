using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("{ToString()}")]
	public class StatementDescriptionNode
	{
		private readonly List<StatementDescriptionNode> _childNodes = new List<StatementDescriptionNode>();

		private StatementDescriptionNode _firstTerminal;
		private StatementDescriptionNode _lastTerminal;

		public StatementDescriptionNode(NodeType type)
		{
			Type = type;

			if (Type != NodeType.Terminal)
				return;
			
			_firstTerminal = this;
			_lastTerminal = this;
		}

		public NodeType Type { get; private set; }

		public StatementDescriptionNode ParentNode { get; private set; }

		//public StatementDescriptionNode PreviousTerminal { get; private set; }
		
		//public StatementDescriptionNode NextTerminal { get; private set; }

		public IToken Token { get; set; }
		
		public string Id { get; set; }
		
		public int Level { get; set; }

		public bool IsRequired { get; set; }

		public ICollection<StatementDescriptionNode> ChildNodes { get { return _childNodes.AsReadOnly(); } }

		public SourcePosition SourcePosition
		{
			get
			{
				var indexStart = -1;
				var indexEnd = -1;
				if (Type == NodeType.Terminal)
				{
					indexStart = Token.Index;
					indexEnd = Token.Index + Token.Value.Length - 1;
				}
				else if (Terminals.Any())
				{
					indexStart = Terminals.First().Token.Index;
					var lastTerminal = Terminals.Last().Token;
					indexEnd = lastTerminal.Index + lastTerminal.Value.Length - 1;
				}

				return new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
			}
		}

		public IEnumerable<StatementDescriptionNode> Terminals 
		{
			get
			{
				return Type == NodeType.Terminal
					? Enumerable.Repeat(this, 1)
					: ChildNodes.SelectMany(t => t.Terminals);
			}
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

		#region Overrides of Object
		public override string ToString()
		{
			return string.Format("StatementDescriptionNode (Id={0}; Type={1}; IsRequired={2}; SourcePosition=({3}-{4}))", Id, Type, IsRequired, SourcePosition.IndexStart, SourcePosition.IndexEnd);
		}
		#endregion

		public void AddChildNodes(params StatementDescriptionNode[] nodes)
		{
			AddChildNodes((IEnumerable<StatementDescriptionNode>)nodes);
		}

		public void AddChildNodes(IEnumerable<StatementDescriptionNode> nodes)
		{
			if (Type == NodeType.Terminal)
				throw new InvalidOperationException("Terminal nodes cannot have child nodes. ");

			foreach (var node in nodes)
			{
				if (node.Type == NodeType.Terminal)
				{
					_firstTerminal = _firstTerminal ?? node;
					_lastTerminal = node;
				}
				else
				{
					_firstTerminal = _firstTerminal ?? node._firstTerminal;
					_lastTerminal = node._lastTerminal;
				}

				_childNodes.Add(node);
				node.ParentNode = this;
			}
		}

		public IEnumerable<StatementDescriptionNode> GetDescendants(params string[] descendantNodeIds)
		{
			return AllChildNodes.Where(t => descendantNodeIds == null || descendantNodeIds.Length == 0 || descendantNodeIds.Contains(t.Id));
		}

		/*public int? GetAncestorDistance(string ancestorNodeId)
		{
			return GetAncestorDistance(ancestorNodeId, 0);
		}

		private int? GetAncestorDistance(string ancestorNodeId, int level)
		{
			if (Id == ancestorNodeId)
				return level;
			
			return ParentNode != null ? ParentNode.GetAncestorDistance(ancestorNodeId, level + 1) : null;
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

		/*private void ResolveLinks()
		{
			StatementDescriptionNode previousTerminal = null;
			foreach (var terminal in Terminals)
			{
				if (previousTerminal != null)
				{
					terminal.PreviousTerminal = previousTerminal;
					previousTerminal.NextTerminal = terminal;
				}

				previousTerminal = terminal;
			}
		}*/

		public int RemoveLastChildNodeIfOptional()
		{
			if (Type == NodeType.Terminal)
				throw new InvalidOperationException("Terminal node has no child nodes. ");

			var index = _childNodes.Count - 1;
			var node = _childNodes[index];
			if (node.Type == NodeType.Terminal)
			{
				if (node.IsRequired)
					return 0;
				
				_childNodes.RemoveAt(index);
				return 1;
			}

			var removedTerminalCount = node.RemoveLastChildNodeIfOptional();
			if (removedTerminalCount != 0 || node.IsRequired)
				return removedTerminalCount;
			
			removedTerminalCount = node.Terminals.Count();
			_childNodes.RemoveAt(index);

			return removedTerminalCount;
		}

		internal static StatementDescriptionNode FromChildNodes(IEnumerable<StatementDescriptionNode> childNodes)
		{
			var node = new StatementDescriptionNode(NodeType.NonTerminal);
			node.AddChildNodes(childNodes);

			return node;
		}
	}
}
