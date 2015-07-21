using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	[DebuggerDisplay("{ToString()}")]
	public class StatementGrammarNode : StatementNode
	{
		public static readonly IReadOnlyCollection<StatementGrammarNode> EmptyArray = new StatementGrammarNode[0];

		private readonly List<StatementGrammarNode> _childNodes = new List<StatementGrammarNode>();
		private IReadOnlyList<StatementGrammarNode> _terminals;
		private List<StatementCommentNode> _commentNodes;

		public int TerminalCount { get; private set; }

		public StatementGrammarNode this[params int[] descendantIndexes]
		{
			get
			{
				var node = this;
				foreach (var index in descendantIndexes)
				{
					if (node == null || node._childNodes.Count <= index)
					{
						return null;
					}

					node = node._childNodes[index];
				}

				return node;
			}
		}

		public StatementGrammarNode this[params string[] descendantNodeIds]
		{
			get
			{
				var node = this;
				foreach (var id in descendantNodeIds)
				{
					if (node == null || node._childNodes.Count == 0)
					{
						return null;
					}

					node = node._childNodes.SingleOrDefault(n => String.Equals(n.Id, id));
				}

				return node;
			}
		}

		public StatementGrammarNode(NodeType type, StatementBase statement, IToken token) : base(statement, token)
		{
			Type = type;

			if (Type != NodeType.Terminal)
				return;

			if (token == null)
				throw new ArgumentNullException(nameof(token));

			IsGrammarValid = true;
			FirstTerminalNode = this;
			LastTerminalNode = this;
			TerminalCount = 1;
		}

		public NodeType Type { get; }

		public StatementGrammarNode RootNode => GetRootNode();

	    private StatementGrammarNode GetRootNode()
		{
			return ParentNode == null ? this : ParentNode.GetRootNode();
		}

		public StatementGrammarNode PrecedingTerminal
		{
			get
			{
				var previousNode = GetPrecedingNode(this);
				return previousNode?.LastTerminalNode;
			}
		}

		private static StatementGrammarNode GetPrecedingNode(StatementGrammarNode node)
		{
			var parentNode = node.ParentNode;
			if (parentNode == null)
				return null;

			var index = parentNode._childNodes.IndexOf(node) - 1;
			return index >= 0
				? parentNode._childNodes[index]
				: GetPrecedingNode(parentNode);
		}

		public StatementGrammarNode FollowingTerminal
		{
			get
			{
				var followingNode = GetFollowingNode(this);
				return followingNode?.FirstTerminalNode;
			}
		}

		private static StatementGrammarNode GetFollowingNode(StatementGrammarNode node)
		{
			if (node.ParentNode == null)
				return null;

			var index = node.ParentNode._childNodes.IndexOf(node) + 1;
			return index < node.ParentNode._childNodes.Count
				? node.ParentNode._childNodes[index]
				: GetFollowingNode(node.ParentNode);
		}
		
		public StatementGrammarNode FirstTerminalNode { get; private set; }

		public StatementGrammarNode LastTerminalNode { get; private set; }

		public string Id { get; set; }
		
		public int Level { get; set; }

		public bool IsRequired { get; set; }

		public bool IsReservedWord { get; set; }
		
		public bool IsGrammarValid { get; set; }

		public IReadOnlyList<StatementGrammarNode> ChildNodes => _childNodes.AsReadOnly();

	    public ICollection<StatementCommentNode> Comments => _commentNodes ?? (_commentNodes = new List<StatementCommentNode>());

	    public bool IsRequiredIncludingParent
		{
			get
			{
				var node = this;
				while (node != null)
				{
					if (!node.IsRequired)
					{
						return false;
					}

					node = node.ParentNode;
				}

				return true;
			}
		}

		public int IndexOf(StatementGrammarNode childNode)
		{
			return _childNodes.IndexOf(childNode);
		}

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

		public IReadOnlyList<StatementGrammarNode> Terminals => _terminals ?? GatherTerminals();

	    private IReadOnlyList<StatementGrammarNode> GatherTerminals()
		{
			var statementGrammarNodes = new StatementGrammarNode[TerminalCount];
			var offset = 0;

			GatherNodeTerminals(this, statementGrammarNodes, ref offset);
			
			return _terminals = statementGrammarNodes;
		}

		private static void GatherChildNodeTerminals(StatementGrammarNode node, StatementGrammarNode[] targetArray, ref int offset)
		{
			foreach (var childNode in node.ChildNodes)
			{
				GatherNodeTerminals(childNode, targetArray, ref offset);
			}
		}

		private static void GatherNodeTerminals(StatementGrammarNode childNode, StatementGrammarNode[] targetArray, ref int offset)
		{
			if (childNode.Type == NodeType.Terminal)
			{
				targetArray[offset++] = childNode;
			}
			else
			{
				GatherChildNodeTerminals(childNode, targetArray, ref offset);
			}
		}

		public IEnumerable<StatementGrammarNode> AllChildNodes => GetChildNodes();

	    private IEnumerable<StatementGrammarNode> GetChildNodes(Func<StatementGrammarNode, bool> filter = null)
		{
			return Type == NodeType.Terminal
				? Enumerable.Empty<StatementGrammarNode>()
				: ChildNodes.Where(n => filter == null || filter(n)).SelectMany(n => Enumerable.Repeat(n, 1).Concat(n.GetChildNodes(filter)));
		}

		#region Overrides of Object
		public override string ToString()
		{
			var terminalValue = Type == NodeType.NonTerminal || Token == null ? String.Empty : $"; TokenValue={Token.Value}";
			return $"StatementGrammarNode (Id={Id}; Type={Type}; IsRequired={IsRequired}; IsGrammarValid={IsGrammarValid}; Level={Level}; TerminalCount={TerminalCount}; SourcePosition=({SourcePosition.IndexStart}-{SourcePosition.IndexEnd}){terminalValue})";
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
					throw new InvalidOperationException($"Node '{node.Id}' has been already associated with another parent. ");

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

		public string GetText(string sourceText)
		{
			return sourceText.Substring(SourcePosition.IndexStart, SourcePosition.Length);
		}

		public IEnumerable<StatementGrammarNode> GetPathFilterDescendants(Func<StatementGrammarNode, bool> pathFilter, params string[] descendantNodeIds)
		{
			return GetPathFilterDescendants(this, pathFilter, descendantNodeIds);
		}

		private static IEnumerable<StatementGrammarNode> GetPathFilterDescendants(StatementGrammarNode node, Func<StatementGrammarNode, bool> pathFilter, params string[] descendantNodeIds)
		{
			foreach (var childNode in node.ChildNodes)
			{
				if (pathFilter != null && !pathFilter(childNode))
				{
					continue;
				}

				if (descendantNodeIds == null || descendantNodeIds.Length == 0 || descendantNodeIds.Contains(childNode.Id))
				{
					yield return childNode;
				}

				if (childNode.Type == NodeType.Terminal)
				{
					continue;
				}
				
				foreach (var descendant in GetPathFilterDescendants(childNode, pathFilter, descendantNodeIds))
				{
					yield return descendant;
				}
			}
		}

		public StatementGrammarNode GetSingleDescendant(params string[] descendantNodeIds)
		{
			return GetDescendants(descendantNodeIds).SingleOrDefault();
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
			if (String.Equals(Id, ancestorNodeId))
				return level;
			
			return ParentNode?.GetAncestorDistance(ancestorNodeId, level + 1);
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
			if (includeSelf && String.Equals(Id, ancestorNodeId))
				return this;

			if (ParentNode == null || (pathFilter != null && !pathFilter(ParentNode)))
				return null;

			return String.Equals(ParentNode.Id, ancestorNodeId)
				? ParentNode
				: ParentNode.GetPathFilterAncestor(pathFilter, ancestorNodeId);
		}

		public StatementGrammarNode GetPathFilterAncestor(Func<StatementGrammarNode, bool> pathFilter, Func<StatementGrammarNode, bool> ancestorPredicate)
		{
			if (ParentNode == null || (pathFilter != null && !pathFilter(ParentNode)))
				return null;

			return ancestorPredicate(ParentNode)
				? ParentNode
				: ParentNode.GetPathFilterAncestor(pathFilter, ancestorPredicate);
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
