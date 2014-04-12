using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public abstract class StatementBase
	{
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
			get { return AllNodes.Where(n => !n.IsGrammarValid); }
		}

		public IEnumerable<StatementDescriptionNode> AllTerminals
		{
			get
			{
				return NodeCollection == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: NodeCollection.SelectMany(n => n.Terminals);
			}
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
