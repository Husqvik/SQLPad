using System.Linq;
using System.Text;

namespace SqlPad.Commands
{
	public class AddMissingAliasesCommand
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
			var aliasedColumns = queryBlockRoot.GetDescendants(OracleGrammarDescription.NonTerminals.AliasedExpression);

			var currentColumn = 0;
			var addedOffset = 0;
			foreach (var aliasedExpression in aliasedColumns)
			{
				currentColumn++;
				if (aliasedExpression.ChildNodes.Count == 2 ||
					(aliasedExpression.TerminalCount == 1 && aliasedExpression.Terminals.Single().Id == OracleGrammarDescription.Terminals.Identifier))
					continue;

				var alias = " COLUMN" + currentColumn;
				builder.Insert(aliasedExpression.SourcePosition.IndexEnd + 1 + addedOffset, alias);
				addedOffset += alias.Length;
			}

			return builder.ToString();
		}
	}
}
