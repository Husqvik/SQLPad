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

		public ICollection<StatementDescriptionNode> NodeCollection { get; set; }
		
		public ICollection<string> TerminalCandidates { get; set; }

		public SourcePosition SourcePosition { get; set; }

		public IEnumerable<StatementDescriptionNode> AllNodes
		{
			get
			{
				return NodeCollection == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: NodeCollection.SelectMany(n => n.AllChildNodes);
			}
		}

		public IEnumerable<StatementDescriptionNode> InvalidGrammarNodes
		{
			get
			{
				return _invalidGrammarNodes ?? (_invalidGrammarNodes = AllNodes.Where(n => !n.IsGrammarValid).ToArray());
			}
		}

		public IEnumerable<StatementDescriptionNode> AllTerminals
		{
			get
			{
				return _allTerminals ?? (_allTerminals = BuildTerminalCollection());
			}
		}

		private ICollection<StatementDescriptionNode> BuildTerminalCollection()
		{
			return NodeCollection == null
				? new StatementDescriptionNode[0]
				: NodeCollection.SelectMany(n => n.Terminals).ToArray();
		}

		public StatementDescriptionNode GetNodeAtPosition(int position, Func<StatementDescriptionNode, bool> filter = null)
		{
			return NodeCollection.Select(n => n.GetNodeAtPosition(position, filter)).FirstOrDefault(n => n != null);
		}

		public StatementDescriptionNode GetNearestTerminalToPosition(int position)
		{
			return NodeCollection.Select(n => n.GetNearestTerminalToPosition(position)).FirstOrDefault(n => n != null);
		}
	}
}
