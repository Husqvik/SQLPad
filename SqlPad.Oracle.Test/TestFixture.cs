using System;
using System.Linq;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.Test
{
	public class TestFixture
	{
		public static readonly OracleDatabaseModelBase DatabaseModel = OracleTestDatabaseModel.Instance;

		public static SqlDocumentRepository CreateDocumentRepository()
		{
			return new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), DatabaseModel);
		}
	}

	public static class TestExtensions
	{
		public static StatementBase Validate(this StatementBase statement)
		{
			if (statement.RootNode == null || statement.RootNode.TerminalCount <= 1)
				return statement;

			var sortedTerminals = statement.AllTerminals.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			var terminal = sortedTerminals[0];
			var allTerminals = statement.AllTerminals.ToArray();

			for (var i = 1; i < sortedTerminals.Length; i++)
			{
				var followingTerminal = sortedTerminals[i];
				if (terminal.SourcePosition.IndexEnd >= followingTerminal.SourcePosition.IndexStart)
					throw new InvalidOperationException($"Terminals '{terminal.Id}' and '{followingTerminal.Id}' within the statement are overlapping. ");

				if (followingTerminal != allTerminals[i])
					throw new InvalidOperationException($"Terminals within the statement are in invalid order (index {i}). ");

				terminal = followingTerminal;
			}

			return statement;
		}
	}
}