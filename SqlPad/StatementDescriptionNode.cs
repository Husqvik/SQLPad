using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("{ToString()}")]
	public class StatementDescriptionNode : StatementNode
	{
		private readonly List<StatementDescriptionNode> _childNodes = new List<StatementDescriptionNode>();

		public int TerminalCount { get; private set; }

		public StatementDescriptionNode(NodeType type, StatementBase statement, IToken token) : base(statement, token)
		{
			Type = type;

			if (Type != NodeType.Terminal)
				return;

			if (token == null)
				throw new ArgumentNullException("token");

			IsGrammarValid = true;
			FirstTerminalNode = this;
			LastTerminalNode = this;
			TerminalCount = 1;
		}

		public NodeType Type { get; private set; }

		public StatementDescriptionNode ParentNode { get; private set; }

		public StatementDescriptionNode RootNode
		{
			get { return GetRootNode(); }
		}

		private StatementDescriptionNode GetRootNode()
		{
			return ParentNode == null ? this : ParentNode.GetRootNode();
		}

		public StatementDescriptionNode PrecedingTerminal
		{
			get
			{
				var previousNode = GetPrecedingNode(this);
				return previousNode == null ? null : previousNode.LastTerminalNode;
			}
		}

		private StatementDescriptionNode GetPrecedingNode(StatementDescriptionNode node)
		{
			if (node.ParentNode == null)
				return null;

			var index = node.ParentNode._childNodes.IndexOf(node) - 1;
			return index >= 0
				? node.ParentNode._childNodes[index]
				: GetPrecedingNode(node.ParentNode);
		}
		
		//public StatementDescriptionNode NextTerminal { get; private set; }

		public StatementDescriptionNode FirstTerminalNode { get; private set; }

		public StatementDescriptionNode LastTerminalNode { get; private set; }

		public string Id { get; set; }
		
		public int Level { get; set; }

		public bool IsRequired { get; set; }

		public bool IsKeyword { get; set; }
		
		public bool IsGrammarValid { get; set; }

		public IList<StatementDescriptionNode> ChildNodes { get { return _childNodes.AsReadOnly(); } }

		protected override SourcePosition BuildSourcePosition()
		{
			var indexStart = -1;
			var indexEnd = -1;
			if (Type == NodeType.Terminal)
			{
				indexStart = Token.Index;
				indexEnd = Token.Index + Token.Value.Length - 1;
			}
			else if (LastTerminalNode != null)
			{
				indexStart = FirstTerminalNode.Token.Index;
				var lastTerminal = LastTerminalNode.Token;
				indexEnd = lastTerminal.Index + lastTerminal.Value.Length - 1;
			}

			return new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
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
			get { return GetChildNodes(); }
		}

		private IEnumerable<StatementDescriptionNode> GetChildNodes(Func<StatementDescriptionNode, bool> filter = null)
		{
			return Type == NodeType.Terminal
				? Enumerable.Empty<StatementDescriptionNode>()
				: ChildNodes.Where(n => filter == null || filter(n)).SelectMany(n => Enumerable.Repeat(n, 1).Concat(n.GetChildNodes(filter)));
		}

		#region Overrides of Object
		public override string ToString()
		{
			var terminalValue = Type == NodeType.NonTerminal || Token == null ? String.Empty : String.Format("; TokenValue={0}", Token.Value);
			return String.Format("StatementDescriptionNode (Id={0}; Type={1}; IsRequired={2}; IsGrammarValid={3}; Level={4}; SourcePosition=({5}-{6}){7})", Id, Type, IsRequired, IsGrammarValid, Level, SourcePosition.IndexStart, SourcePosition.IndexEnd, terminalValue);
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

			if (ParentNode != null)
				throw new InvalidOperationException("Child nodes cannot be added when the node is already associated with parent node. ");

			foreach (var node in nodes)
			{
				if (node.ParentNode != null)
					throw new InvalidOperationException(String.Format("Node '{0}' has been already associated with another parent. ", node.Id));

				if (node.Type == NodeType.Terminal)
				{
					FirstTerminalNode = FirstTerminalNode ?? node;
					LastTerminalNode = node;
					TerminalCount++;
				}
				else
				{
					FirstTerminalNode = FirstTerminalNode ?? node.FirstTerminalNode;
					LastTerminalNode = node.LastTerminalNode;
					TerminalCount += node.TerminalCount;
				}

				_childNodes.Add(node);
				node.ParentNode = this;
			}
		}

		public string GetStatementSubstring(string statementText)
		{
			return statementText.Substring(SourcePosition.IndexStart, SourcePosition.Length);
		}

		public IEnumerable<StatementDescriptionNode> GetPathFilterDescendants(Func<StatementDescriptionNode, bool> pathFilter, params string[] descendantNodeIds)
		{
			return GetChildNodes(pathFilter).Where(t => descendantNodeIds == null || descendantNodeIds.Length == 0 || descendantNodeIds.Contains(t.Id));
		}

		public StatementDescriptionNode GetSingleDescendant(params string[] descendantNodeIds)
		{
			return GetDescendants(descendantNodeIds).SingleOrDefault();
		}

		public IEnumerable<StatementDescriptionNode> GetDescendants(params string[] descendantNodeIds)
		{
			return GetPathFilterDescendants(null, descendantNodeIds);
		}

		public int? GetAncestorDistance(string ancestorNodeId)
		{
			return GetAncestorDistance(ancestorNodeId, 0);
		}

		private int? GetAncestorDistance(string ancestorNodeId, int level)
		{
			if (Id == ancestorNodeId)
				return level;
			
			return ParentNode != null ? ParentNode.GetAncestorDistance(ancestorNodeId, level + 1) : null;
		}

		public bool HasAncestor(StatementDescriptionNode node, bool includeSelf = false)
		{
			if (includeSelf && this == node)
				return true;

			var ancestorNode = this;
			do
			{
				ancestorNode = ancestorNode.ParentNode;
			}
			while (ancestorNode != null && ancestorNode != node);

			return ancestorNode != null;
		}

		public StatementDescriptionNode GetPathFilterAncestor(Func<StatementDescriptionNode, bool> pathFilter, string ancestorNodeId, bool includeSelf = false)
		{
			if (includeSelf && Id == ancestorNodeId)
				return this;

			if (ParentNode == null || (pathFilter != null && !pathFilter(ParentNode)))
				return null;

			return ParentNode.Id == ancestorNodeId
				? ParentNode
				: ParentNode.GetPathFilterAncestor(pathFilter, ancestorNodeId);
		}

		public StatementDescriptionNode GetAncestor(string ancestorNodeId, bool includeSelf = false)
		{
			return GetPathFilterAncestor(null, ancestorNodeId, includeSelf);
		}

		public StatementDescriptionNode GetTopAncestor(string ancestorNodeId, Func<StatementDescriptionNode, bool> pathFilter = null)
		{
			var node = this;
			StatementDescriptionNode ancestorNode;
			while ((ancestorNode = node.GetPathFilterAncestor(pathFilter, ancestorNodeId)) != null)
			{
				node = ancestorNode;
			}

			return node;
		}

		public StatementDescriptionNode GetNodeAtPosition(int position, Func<StatementDescriptionNode, bool> filter = null)
		{
			if (!SourcePosition.ContainsIndex(position))
				return null;

			var node = GetChildNodesAtPosition(this, position)
				.Where(n => filter == null || filter(n))
				.OrderBy(n => n.SourcePosition.IndexStart == position ? 0 : 1)
				.ThenByDescending(n => n.Level)
				.FirstOrDefault();

			return node ?? this;
		}

		private static IEnumerable<StatementDescriptionNode> GetChildNodesAtPosition(StatementDescriptionNode node, int position)
		{
			var returnedNodes = new List<StatementDescriptionNode>();
			if (node == null)
				return returnedNodes;

			var childNodesAtPosition = node.ChildNodes.Where(n => n.SourcePosition.ContainsIndex(position));
			foreach (var childNodeAtPosition in childNodesAtPosition)
			{
				if (childNodeAtPosition.Type == NodeType.Terminal)
				{
					returnedNodes.Add(childNodeAtPosition);
				}
				else
				{
					returnedNodes.AddRange(GetChildNodesAtPosition(childNodeAtPosition, position));
				}
			}

			return returnedNodes;
		}

		public StatementDescriptionNode GetNearestTerminalToPosition(int position)
		{
			return Terminals.TakeWhile(t => t.SourcePosition.IndexStart <= position).LastOrDefault();
		}

		/*private void ResolveLinks()
		{
			StatementDescriptionNode previousTerminal = null;
			foreach (var terminal in Terminals)
			{
				if (previousTerminal != null)
				{
					terminal.PrecedingTerminal = previousTerminal;
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

				if (_childNodes.Count == 1)
					throw new InvalidOperationException("Last terminal cannot be removed. ");
				
				_childNodes.RemoveAt(index);
				LastTerminalNode = _childNodes[index - 1].LastTerminalNode;
				TerminalCount--;
				return 1;
			}

			var removedTerminalCount = node.RemoveLastChildNodeIfOptional();
			if (removedTerminalCount == 0 && !node.IsRequired)
			{
				removedTerminalCount = node.TerminalCount;
				_childNodes.RemoveAt(index);
				LastTerminalNode = _childNodes[index - 1].LastTerminalNode;
			}

			TerminalCount -= removedTerminalCount;
			return removedTerminalCount;
		}

		public StatementDescriptionNode Clone()
		{
			var clonedNode = new StatementDescriptionNode(Type, Statement, Token)
			                 {
				                 Id = Id,
				                 IsRequired = IsRequired,
				                 Level = Level,
				                 IsGrammarValid = IsGrammarValid,
								 IsKeyword = IsKeyword
			                 };

			if (Type == NodeType.NonTerminal)
			{
				clonedNode.AddChildNodes(_childNodes.Select(n => n.Clone()));
			}

			return clonedNode;
		}
	}
}
