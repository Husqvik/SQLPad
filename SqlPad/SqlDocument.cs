using System;

namespace SqlPad
{
	public class SqlDocument
	{
		private readonly object _lockObject = new object();

		public static SqlDocument FromStatementCollection(StatementCollection statements)
		{
			return new SqlDocument { StatementCollection = statements };
		}

		public StatementCollection StatementCollection { get; private set; }

		public void UpdateStatements(StatementCollection statements)
		{
			lock (_lockObject)
			{
				StatementCollection = statements;
			}
		}

		public void ExecuteStatementAction(Action<StatementCollection> action)
		{
			lock (_lockObject)
			{
				action(StatementCollection);
			}
		}
	}
}