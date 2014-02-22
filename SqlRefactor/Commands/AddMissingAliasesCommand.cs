using System.Linq;
using System.Text;

namespace SqlRefactor.Commands
{
	public class AddMissingAliasesCommand
	{
		private readonly OracleSqlParser _sqlParser = new OracleSqlParser();

		public string Execute(int offset, string statementText)
		{
			var statements = _sqlParser.Parse(statementText);
			var statement = statements.FirstOrDefault(s => s.SourcePosition.IndexStart <= offset && s.SourcePosition.IndexEnd >= offset);

			if (statement == null)
				return statementText;

			var builder = new StringBuilder(statementText);

			var selectedToken = statement.GetNodeAtPosition(offset);

			var nestedQueryRoot = selectedToken.GetAncestor(OracleGrammarDescription.NonTerminals.NestedQuery);

			var aliasedExpressions = nestedQueryRoot.GetDescendants(OracleGrammarDescription.NonTerminals.SelectList, OracleGrammarDescription.NonTerminals.AliasedExpression).OrderBy(e => e.SourcePosition.IndexStart);

			var currentColumn = 0;
			var addedOffset = 0;
			foreach (var aliasedExpression in aliasedExpressions)
			{
				currentColumn++;
				if (aliasedExpression.ChildNodes.Count == 2)
					continue;

				var alias = " COLUMN" + currentColumn;
				builder.Insert(aliasedExpression.SourcePosition.IndexEnd + addedOffset, alias);
				addedOffset += alias.Length;
			}

			return builder.ToString();
		}
	}
}
