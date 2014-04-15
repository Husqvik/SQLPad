using System.Linq;
using System.Text;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class WrapAsCommonTableExpressionCommandOld : IWrapAsCommonTableExpressionCommand
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

			var queryBlockRoot = selectedToken.GetAncestor(OracleGrammarDescription.NonTerminals.QueryBlock);
			if (queryBlockRoot == null)
				return statementText;

			var columnAliases = queryBlockRoot
				.GetDescendants(OracleGrammarDescription.NonTerminals.AliasedExpression)
				.Select(e => e.Terminals.Last());
			
			var newStatementBuilder = new StringBuilder("SELECT ");
			var isFirst = true;
			foreach (var column in columnAliases)
			{
				if (!isFirst)
					newStatementBuilder.Append(", ");

				isFirst = false;

				newStatementBuilder.Append(column.Token.Value);
			}

			newStatementBuilder.Append(" FROM ");
			newStatementBuilder.Append(queryName);

			var lastEffectiveNode = queryBlockRoot.Terminals.LastOrDefault(t => t.Id != OracleGrammarDescription.Terminals.Semicolon);
			var indexEnd = lastEffectiveNode == null
				? queryBlockRoot.SourcePosition.IndexEnd
				: lastEffectiveNode.SourcePosition.IndexEnd;

			var movedStatementLength = indexEnd - queryBlockRoot.SourcePosition.IndexStart + 1;
			var movedStatement = statementText.Substring(queryBlockRoot.SourcePosition.IndexStart, movedStatementLength);

			var builder = new StringBuilder(statementText);
			var addedOffset = 0;

			var nestedQueryRoot = queryBlockRoot.GetAncestor(OracleGrammarDescription.NonTerminals.NestedQuery);
			var lastSubquery = nestedQueryRoot.GetDescendants(OracleGrammarDescription.NonTerminals.SubqueryComponent).LastOrDefault();
			int whiteSpace;
			int insertIndex;
			string initialToken;
			if (lastSubquery == null)
			{
				insertIndex = nestedQueryRoot.SourcePosition.IndexStart;
				initialToken = "WITH ";
				whiteSpace = 0;
			}
			else
			{
				insertIndex = lastSubquery.SourcePosition.IndexEnd + 1;
				initialToken = ", ";
				whiteSpace = queryBlockRoot.SourcePosition.IndexStart - lastSubquery.SourcePosition.IndexEnd - 1;
			}

			addedOffset += builder.InsertAt(insertIndex + addedOffset, initialToken + queryName + " AS (" + movedStatement + ") ");

			var statementPosition = insertIndex + addedOffset;

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
