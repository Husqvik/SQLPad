using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SqlPad
{
	public class StatementCollection : ReadOnlyCollection<StatementBase>
	{
		public StatementCollection(IList<StatementBase> statements, IEnumerable<StatementCommentNode> comments)
			: base(statements)
		{
			Comments = comments.ToList().AsReadOnly();
		}

		public ICollection<StatementCommentNode> Comments { get; private set; }

		public StatementBase GetStatementAtPosition(int position)
		{
			return Items.LastOrDefault(s => s.SourcePosition.IndexStart <= position && s.SourcePosition.IndexEnd + 1 >= position);
		}

		public StatementGrammarNode GetNodeAtPosition(int position, Func<StatementGrammarNode, bool> filter = null)
		{
			var statement = GetStatementAtPosition(position);
			return statement == null ? null : statement.GetNodeAtPosition(position, filter);
		}

		public StatementGrammarNode GetTerminalAtPosition(int position, Func<StatementGrammarNode, bool> filter = null)
		{
			var node = GetNodeAtPosition(position, n => n.Type == NodeType.Terminal && (filter == null || filter(n)));
			return node == null || node.Type == NodeType.NonTerminal ? null : node;
		}
	}
}
