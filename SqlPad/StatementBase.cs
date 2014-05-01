using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public abstract class StatementBase
	{
		private ICollection<StatementDescriptionNode> _allTerminals;
		private ICollection<StatementDescriptionNode> _invalidGrammarNodes;

		public ProcessingStatus ProcessingStatus { get; set; }

		public StatementDescriptionNode RootNode { get; set; }
		
		public ICollection<string> TerminalCandidates { get; set; }

		public SourcePosition SourcePosition { get; set; }

		public ICollection<StatementDescriptionNode> InvalidGrammarNodes
		{
			get
			{
				return _invalidGrammarNodes ?? (_invalidGrammarNodes = RootNode.AllChildNodes.Where(n => !n.IsGrammarValid).ToArray());
			}
		}

		public ICollection<StatementDescriptionNode> AllTerminals
		{
			get
			{
				return _allTerminals ?? (_allTerminals = BuildTerminalCollection());
			}
		}

		private ICollection<StatementDescriptionNode> BuildTerminalCollection()
		{
			return new HashSet<StatementDescriptionNode>(RootNode == null ? Enumerable.Empty<StatementDescriptionNode>() : RootNode.Terminals);
		}

		public StatementDescriptionNode GetNodeAtPosition(int position, Func<StatementDescriptionNode, bool> filter = null)
		{
			return RootNode == null ? null : RootNode.GetNodeAtPosition(position, filter);
		}

		public StatementDescriptionNode GetNearestTerminalToPosition(int position)
		{
			return RootNode == null ? null : RootNode.GetNearestTerminalToPosition(position);
		}
	}
}
