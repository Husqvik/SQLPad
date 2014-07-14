using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SqlPad
{
	public class StatementCollection : ReadOnlyCollection<StatementBase>
	{
		public StatementCollection(IList<StatementBase> statements)
			: base(statements)
		{
		}

		public StatementBase GetStatementAtPosition(int position)
		{
			return Items.LastOrDefault(s => s.SourcePosition.IndexStart <= position && s.SourcePosition.IndexEnd + 1 >= position);
		}

		public StatementDescriptionNode GetNodeAtPosition(int position, Func<StatementDescriptionNode, bool> filter = null)
		{
			var statement = GetStatementAtPosition(position);
			return statement == null ? null : statement.GetNodeAtPosition(position, filter);
		}

		public StatementDescriptionNode GetTerminalAtPosition(int position, Func<StatementDescriptionNode, bool> filter = null)
		{
			var node = GetNodeAtPosition(position, filter);
			return node == null || node.Type == NodeType.NonTerminal ? null : node;
		}
	}
}