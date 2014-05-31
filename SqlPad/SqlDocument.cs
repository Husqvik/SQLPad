using System;

namespace SqlPad
{
	public class SqlDocument
	{
		private readonly object _lockObject = new object();

		public static SqlDocument FromStatementCollection(StatementCollection statements, string statementText)
		{
			return new SqlDocument { StatementCollection = statements, StatementText = statementText };
		}

		public StatementCollection StatementCollection { get; private set; }
		public string StatementText { get; private set; }

		public void UpdateStatements(StatementCollection statements, string statementText)
		{
			lock (_lockObject)
			{
				StatementCollection = statements;
				StatementText = statementText;
			}
		}

		public void ExecuteStatementAction(Action<StatementCollection> action)
		{
			lock (_lockObject)
			{
				action(StatementCollection);
			}
		}

		public T ExecuteStatementAction<T>(Func<StatementCollection, T> function)
		{
			lock (_lockObject)
			{
				return function(StatementCollection);
			}
		}
	}
}