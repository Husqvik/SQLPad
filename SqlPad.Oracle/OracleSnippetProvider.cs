using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleSnippetProvider : ICodeSnippetProvider
	{
		private static readonly ICodeSnippet[] EmptyCollection = new ICodeSnippet[0];

		internal IEnumerable<ICodeSnippet> GetSnippets(string statementText, int cursorPosition, IDatabaseModel databaseModel)
		{
			var documentStore = new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), databaseModel, statementText);
			return GetSnippets(documentStore, statementText, cursorPosition);
		}

		public IEnumerable<ICodeSnippet> GetCodeGenerationItems(SqlDocumentRepository sqlDocumentRepository)
		{
			return Snippets.CodeGenerationItemCollection.Select(s => BuildCodeSnippet(s, SourcePosition.Empty))
				.OrderBy(s => s.Name);
		}

		public IEnumerable<ICodeSnippet> GetSnippets(SqlDocumentRepository sqlDocumentRepository, string statementText, int cursorPosition)
		{
			if (sqlDocumentRepository?.Statements == null)
				return EmptyCollection;

			var statement = sqlDocumentRepository.Statements.TakeWhile(s => s.SourcePosition.IndexStart <= cursorPosition - 1).LastOrDefault();

			StatementGrammarNode currentNode = null;
			if (statement != null)
			{
				currentNode = statement.GetTerminalAtPosition(cursorPosition)
				              ?? statement.GetNearestTerminalToPosition(cursorPosition);
			}

			if (currentNode != null &&
				String.Equals(currentNode.Id, OracleGrammarDescription.Terminals.RightParenthesis) &&
				!String.Equals(currentNode.ParentNode.Id, OracleGrammarDescription.NonTerminals.CommonTableExpression) &&
				currentNode.PrecedingTerminal?.PrecedingTerminal != null)
			{
				currentNode = currentNode.PrecedingTerminal.PrecedingTerminal;
			}

			var textToReplace = new String(statementText.Substring(0, cursorPosition).Reverse().TakeWhile(c => !c.In(' ', '\n', '\t', '(', '\r')).Reverse().ToArray());

			if (String.IsNullOrWhiteSpace(textToReplace))
			{
				return EmptyCollection;
			}

			var candidates = OracleSqlParser.Instance.GetTerminalCandidates(currentNode).Select(c => c.Id);

			return Snippets.SnippetCollection.Where(s => s.Name.ToUpperInvariant().Contains(textToReplace.ToUpperInvariant()) &&
														 (s.AllowedTerminals == null || s.AllowedTerminals.Length == 0 || s.AllowedTerminals.Select(t => t.Id).Intersect(candidates).Any()))
				.Select(s => BuildCodeSnippet(s, new SourcePosition { IndexStart = cursorPosition - textToReplace.Length, IndexEnd = cursorPosition })).ToArray();
		}

		private static OracleCodeSnippet BuildCodeSnippet(Snippet s, SourcePosition sourcePosition)
		{
			return new OracleCodeSnippet
			{
				Name = s.Name,
				BaseText = s.Text,
				Description = s.Description,
				SourceToReplace = sourcePosition,
				Parameters = new List<ICodeSnippetParameter>(
					(s.Parameters ?? Enumerable.Empty<SnippetParameter>()).Select(p => new OracleCodeSnippetParameter
					{
						Index = p.Index,
						DefaultValue = p.DefaultValue
					})).AsReadOnly()
			};
		}
	}

	public class OracleCodeSnippet : ICodeSnippet
	{
		public string Name { get; set; }
		
		public string Description { get; set; }

		public string BaseText { get; set; }

		public IReadOnlyList<ICodeSnippetParameter> Parameters { get; set; }

		public SourcePosition SourceToReplace { get; set; }
	}

	public class OracleCodeSnippetParameter : ICodeSnippetParameter
	{
		public string Name { get; set; }

		public int Index { get; set; }
	
		public string DefaultValue { get; set; }
	}
}