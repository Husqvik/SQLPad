using System.Linq;
using System.Text;

namespace SqlPad.Commands
{
	public class WrapAsCommonTableExpressionCommand
	{
		private readonly OracleSqlParser _sqlParser = new OracleSqlParser();
		private readonly AddMissingAliasesCommand _addMissingAliasesCommand = new AddMissingAliasesCommand();

		public string Execute(string statementText, int offset, string queryName)
		{
			statementText = _addMissingAliasesCommand.Execute(statementText, offset);

			var statements = _sqlParser.Parse(statementText);
			var statement = statements.FirstOrDefault(s => s.SourcePosition.IndexStart <= offset && s.SourcePosition.IndexEnd >= offset);

			if (statement == null)
				return statementText;

			var selectedToken = statement.GetNodeAtPosition(offset);

			var queryBlockRook = selectedToken.GetAncestor(OracleGrammarDescription.NonTerminals.QueryBlock);

			var columnAliases = queryBlockRook
				.GetDescendants(OracleGrammarDescription.NonTerminals.AliasedExpression)
				.Select(e => e.Terminals.Last());
			
			var newStatementBuilder = new StringBuilder("SELECT ");
			var isFirst = true;
			foreach (var column in columnAliases)
			{
				if (!isFirst)
					newStatementBuilder.Append(", ");

				isFirst = false;

				newStatementBuilder.Append(column.Value.Value);
			}

			newStatementBuilder.Append(" FROM ");
			newStatementBuilder.Append(queryName);

			var lastEffectiveNode = queryBlockRook.Terminals.LastOrDefault(t => t.Id != OracleGrammarDescription.Terminals.Semicolon);
			var indexEnd = lastEffectiveNode == null
				? queryBlockRook.SourcePosition.IndexEnd
				: lastEffectiveNode.SourcePosition.IndexEnd;

			var movedStatementLength = indexEnd - queryBlockRook.SourcePosition.IndexStart + 1;
			var movedStatement = statementText.Substring(queryBlockRook.SourcePosition.IndexStart, movedStatementLength);

			var builder = new StringBuilder(statementText);
			var addedOffset = 0;

			var nestedQueryRoot = queryBlockRook.GetAncestor(OracleGrammarDescription.NonTerminals.NestedQuery);
			var lastSubquery = nestedQueryRoot.GetDescendants(OracleGrammarDescription.NonTerminals.SubqueryComponent).LastOrDefault();
			int statementPosition;
			int whiteSpace;
			if (lastSubquery == null)
			{
				addedOffset += builder.InsertAt(0, "WITH " + queryName + " AS (");
				addedOffset += builder.InsertAt(addedOffset, movedStatement);
				addedOffset += builder.InsertAt(addedOffset, ") ");
				statementPosition = addedOffset;
				whiteSpace = 0;
			}
			else
			{
				var insertIndex = lastSubquery.SourcePosition.IndexEnd + 1;
				addedOffset += builder.InsertAt(insertIndex, ", " + queryName + " AS (");
				addedOffset += builder.InsertAt(insertIndex + addedOffset, movedStatement);
				addedOffset += builder.InsertAt(insertIndex + addedOffset, ") ");
				statementPosition = insertIndex + addedOffset;
				whiteSpace = queryBlockRook.SourcePosition.IndexStart - lastSubquery.SourcePosition.IndexEnd - 1;
			}

			builder.Insert(statementPosition, newStatementBuilder.ToString());
			builder.Remove(statementPosition + newStatementBuilder.Length, movedStatementLength + whiteSpace);

			return builder.ToString();
		}
	}

	public static class Extensions
	{
		public static int InsertAt(this StringBuilder stringBuilder, int index, string text)
		{
			stringBuilder.Insert(index, text);
			return text.Length;
		}
	}
}
