using System.Linq;
using System.Text;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class AddMissingAliasesCommand : IAddMissingAliasesCommand
	{
		private readonly OracleSqlParser _sqlParser = new OracleSqlParser();

		public string Execute(string statementText, int offset)
		{
			var statements = _sqlParser.Parse(statementText);
			var statement = statements.FirstOrDefault(s => s.SourcePosition.IndexStart <= offset && s.SourcePosition.IndexEnd >= offset);

			if (statement == null)
				return statementText;

			var builder = new StringBuilder(statementText);

			var selectedToken = statement.GetNodeAtPosition(offset);

			var queryBlockRoot = selectedToken.GetAncestor(OracleGrammarDescription.NonTerminals.QueryBlock);
			if (queryBlockRoot == null)
				return statementText;

			var aliasedColumns = queryBlockRoot.GetDescendants(OracleGrammarDescription.NonTerminals.AliasedExpression);

			var currentColumn = 0;
			var addedOffset = 0;
			foreach (var aliasedColumn in aliasedColumns)
			{
				currentColumn++;
				if (aliasedColumn.ChildNodes.Count == 2 ||
					(aliasedColumn.Terminals.Count() == 1 && aliasedColumn.Terminals.Single().Id == OracleGrammarDescription.Terminals.Identifier))
					continue;

				var alias = " COLUMN" + currentColumn;
				builder.Insert(aliasedColumn.SourcePosition.IndexEnd + 1 + addedOffset, alias);
				addedOffset += alias.Length;
			}

			return builder.ToString();
		}
	}
}
