using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SqlPad
{
	public class StatementCollection : ReadOnlyCollection<StatementBase>
	{
		internal StatementCollection(IEnumerable<StatementBase> statements)
			: base(new List<StatementBase>(statements))
		{
		}

		public StatementBase GetStatementAtPosition(int position)
		{
			return Items.SingleOrDefault(s => s.SourcePosition.IndexStart <= position - 1 && s.SourcePosition.IndexEnd >= position - 1);
		}

		public StatementDescriptionNode GetNodeAtPosition(int position)
		{
			var statement = GetStatementAtPosition(position);
			return statement == null ? null : statement.GetNodeAtPosition(position);
		}

		public StatementDescriptionNode GetTerminalAtPosition(int position)
		{
			var node = GetNodeAtPosition(position);
			return node.Type == NodeType.Terminal ? node : null;
		}
	}
}