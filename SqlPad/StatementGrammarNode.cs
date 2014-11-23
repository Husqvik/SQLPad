using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("{ToString()}")]
	public class StatementGrammarNode : StatementNode
	{
		private readonly List<StatementGrammarNode> _childNodes = new List<StatementGrammarNode>();
		private List<StatementCommentNode> _commentNodes;

		public int TerminalCount { get; private set; }

		public StatementGrammarNode this[int index] 
		{
			get { return _childNodes.Count > index ? _childNodes[index] : null; }
		}

		public StatementGrammarNode(NodeType type, StatementBase statement, IToken token) : base(statement, token)
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

		public StatementGrammarNode RootNode
		{
			get { return GetRootNode(); }
		}

		private StatementGrammarNode GetRootNode()
		{
			return ParentNode == null ? this : ParentNode.GetRootNode();
		}

		public StatementGrammarNode PrecedingTerminal
		{
			get
			{
				var previousNode = GetPrecedingNode(this);
				return previousNode == null ? null : previousNode.LastTerminalNode;
			}
		}

		private StatementGrammarNode GetPrecedingNode(StatementGrammarNode node)
		{
			if (node.ParentNode == null)
				return null;

			var index = node.ParentNode._childNodes.IndexOf(node) - 1;
			return index >= 0
				? node.ParentNode._childNodes[index]
				: GetPrecedingNode(node.ParentNode);
		}

		public StatementGrammarNode FollowingTerminal
		{
			get
			{
				var followingNode = GetFollowingNode(this);
				return followingNode == null ? null : followingNode.FirstTerminalNode;
			}
		}

		private StatementGrammarNode GetFollowingNode(StatementGrammarNode node)
		{
			if (node.ParentNode == null)
				return null;

			var index = node.ParentNode._childNodes.IndexOf(node) + 1;
			return index < node.ParentNode._childNodes.Count
				? node.ParentNode._childNodes[index]
				: GetFollowingNode(node.ParentNode);
		}
		
		//public StatementGrammarNode NextTerminal { get; private set; }

		public StatementGrammarNode FirstTerminalNode { get; private set; }

		public StatementGrammarNode LastTerminalNode { get; private set; }

		public string Id { get; set; }
		
		public int Level { get; set; }

		public bool IsRequired { get; set; }

		public bool IsReservedWord { get; set; }
		
		public bool IsGrammarValid { get; set; }

		public IList<StatementGrammarNode> ChildNodes { get { return _childNodes.AsReadOnly(); } }

		public ICollection<StatementCommentNode> Comments { get { return _commentNodes ?? (_commentNodes = new List<StatementCommentNode>()); } }

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

		public IEnumerable<StatementGrammarNode> Terminals 
		{
			get
			{
				return Type == NodeType.Terminal
					? Enumerable.Repeat(this, 1)
					: ChildNodes.SelectMany(t => t.Terminals);
			}
		}

		public IEnumerable<StatementGrammarNode> AllChildNodes
		{
			get { return GetChildNodes(); }
		}

		private IEnumerable<StatementGrammarNode> GetChildNodes(Func<StatementGrammarNode, bool> filter = null)
		{
			return Type == NodeType.Terminal
				? Enumerable.Empty<StatementGrammarNode>()
				: ChildNodes.Where(n => filter == null || filter(n)).SelectMany(n => Enumerable.Repeat(n, 1).Concat(n.GetChildNodes(filter)));
		}

		#region Overrides of Object
		public override string ToString()
		{
			var terminalValue = Type == NodeType.NonTerminal || Token == null ? String.Empty : String.Format("; TokenValue={0}", Token.Value);
			return String.Format("StatementGrammarNode (Id={0}; Type={1}; IsRequired={2}; IsGrammarValid={3}; Level={4}; SourcePosition=({5}-{6}){7})", Id, Type, IsRequired, IsGrammarValid, Level, SourcePosition.IndexStart, SourcePosition.IndexEnd, terminalValue);
		}
		#endregion

		public void AddChildNodes(params StatementGrammarNode[] nodes)
		{
			AddChildNodes((IEnumerable<StatementGrammarNode>)nodes);
		}

		public void AddChildNodes(IEnumerable<StatementGrammarNode> nodes)
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

		public IEnumerable<StatementGrammarNode> GetPathFilterDescendants(Func<StatementGrammarNode, bool> pathFilter, params string[] descendantNodeIds)
		{
			return GetChildNodes(pathFilter).Where(t => descendantNodeIds == null || descendantNodeIds.Length == 0 || descendantNodeIds.Contains(t.Id));
		}

		public StatementGrammarNode GetSingleDescendant(params string[] descendantNodeIds)
		{
			return GetDescendants(descendantNodeIds).SingleOrDefault();
		}

		public StatementGrammarNode GetDescendantByPath(params string[] descendantNodeIds)
		{
			var node = this;
			foreach (var id in descendantNodeIds)
			{
				if (node == null || node._childNodes.Count == 0)
				{
					return null;
				}

				node = node._childNodes.SingleOrDefault(n => n.Id == id);
			}

			return node;
		}

		public IEnumerable<StatementGrammarNode> GetDescendants(params string[] descendantNodeIds)
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

		public bool HasAncestor(StatementGrammarNode node, bool includeSelf = false)
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

		public StatementGrammarNode GetPathFilterAncestor(Func<StatementGrammarNode, bool> pathFilter, string ancestorNodeId, bool includeSelf = false)
		{
			if (includeSelf && Id == ancestorNodeId)
				return this;

			if (ParentNode == null || (pathFilter != null && !pathFilter(ParentNode)))
				return null;

			return ParentNode.Id == ancestorNodeId
				? ParentNode
				: ParentNode.GetPathFilterAncestor(pathFilter, ancestorNodeId);
		}

		public StatementGrammarNode GetAncestor(string ancestorNodeId, bool includeSelf = false)
		{
			return GetPathFilterAncestor(null, ancestorNodeId, includeSelf);
		}

		public StatementGrammarNode GetTopAncestor(string ancestorNodeId, Func<StatementGrammarNode, bool> pathFilter = null)
		{
			var node = this;
			StatementGrammarNode ancestorNode;
			while ((ancestorNode = node.GetPathFilterAncestor(pathFilter, ancestorNodeId)) != null)
			{
				node = ancestorNode;
			}

			return node;
		}

		public StatementGrammarNode GetNodeAtPosition(int position, Func<StatementGrammarNode, bool> filter = null)
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

		private static IEnumerable<StatementGrammarNode> GetChildNodesAtPosition(StatementGrammarNode node, int position)
		{
			var returnedNodes = new List<StatementGrammarNode>();
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

			if (returnedNodes.Count == 0)
			{
				returnedNodes.Add(node);
			}

			return returnedNodes;
		}

		public StatementGrammarNode GetNearestTerminalToPosition(int position, Func<StatementGrammarNode, bool> filter = null)
		{
			return GetNearestTerminalToPosition(this, position, filter);
		}

		private static StatementGrammarNode GetNearestTerminalToPosition(StatementGrammarNode node, int position, Func<StatementGrammarNode, bool> filter = null)
		{
			if (node.Type == NodeType.Terminal)
			{
				throw new InvalidOperationException("This operation is available only at non-terminal nodes. ");
			}

			for (var index = node._childNodes.Count - 1; index >= 0; index--)
			{
				var childNode = node._childNodes[index];
				if (childNode.SourcePosition.IndexStart == SourcePosition.Empty.IndexStart || childNode.SourcePosition.IndexStart > position)
					continue;

				if (filter == null || filter(childNode))
				{
					return childNode.Type == NodeType.Terminal
						? childNode
						: GetNearestTerminalToPosition(childNode, position, filter);
				}

				var precedingTerminal = childNode;
				do
				{
					precedingTerminal = precedingTerminal.PrecedingTerminal;
				}
				while (precedingTerminal != null && !filter(precedingTerminal));
				
				return precedingTerminal;
			}

			return null;
		}
		
		/*private void ResolveLinks()
		{
			StatementGrammarNode previousTerminal = null;
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

		public StatementGrammarNode Clone()
		{
			var clonedNode = new StatementGrammarNode(Type, Statement, Token)
			                 {
				                 Id = Id,
				                 IsRequired = IsRequired,
				                 Level = Level,
				                 IsGrammarValid = IsGrammarValid,
								 IsReservedWord = IsReservedWord
			                 };

			if (Type == NodeType.NonTerminal)
			{
				clonedNode.AddChildNodes(_childNodes.Select(n => n.Clone()));
			}

			return clonedNode;
		}
	}
}
