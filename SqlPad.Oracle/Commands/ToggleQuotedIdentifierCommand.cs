using System.Linq;
using System.Text;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class ToggleQuotedIdentifierCommand : IToggleQuotedIdentifierCommand
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

			bool enableQuoting;
			if (selectedToken.Id == OracleGrammarDescription.Terminals.Alias)
			{
				enableQuoting = !selectedToken.Token.Value.StartsWith("\"");
			}
			else
			{
				var firstAlias = queryBlockRoot.GetDescendants(OracleGrammarDescription.Terminals.Alias, OracleGrammarDescription.Terminals.Identifier, OracleGrammarDescription.Terminals.ObjectIdentifier).FirstOrDefault();
				if (firstAlias == null)
					return statementText;

				enableQuoting = !firstAlias.Token.Value.StartsWith("\"");
			}

			var aliases = queryBlockRoot.GetDescendants(OracleGrammarDescription.Terminals.Alias, OracleGrammarDescription.Terminals.Identifier, OracleGrammarDescription.Terminals.ObjectIdentifier);

			foreach (var alias in aliases.OrderByDescending(a => a.SourcePosition.IndexEnd))
			{
				var aliasTerminal = alias;
				if ((aliasTerminal.Token.Value.StartsWith("\"") && enableQuoting) ||
					(!aliasTerminal.Token.Value.StartsWith("\"") && !enableQuoting))
					continue;

				if (aliasTerminal.Token.Value == aliasTerminal.Token.Value.ToUpperInvariant())
				{
					if (enableQuoting)
					{
						builder.Insert(aliasTerminal.SourcePosition.IndexEnd + 1, "\"");
						builder.Insert(aliasTerminal.SourcePosition.IndexStart, "\"");
					}
					else
					// TODO: Add check for unsupported characters when non-quoted identifiers are used.
					{
						builder.Remove(aliasTerminal.SourcePosition.IndexEnd, 1);
						builder.Remove(aliasTerminal.SourcePosition.IndexStart, 1);
					}
				}
			}

			return builder.ToString();
		}
	}
}
