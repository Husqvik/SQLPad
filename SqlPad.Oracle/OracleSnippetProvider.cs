using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleSnippetProvider : ICodeSnippetProvider
	{
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();
		private static readonly ICodeSnippet[] EmptyCollection = new ICodeSnippet[0];

		internal ICollection<ICodeSnippet> GetSnippets(string statementText, int cursorPosition)
		{
			return GetSnippets(SqlDocument.FromStatementCollection(_oracleParser.Parse(statementText), statementText), statementText, cursorPosition);
		}

		public ICollection<ICodeSnippet> GetSnippets(SqlDocument sqlDocument, string statementText, int cursorPosition)
		{
			if (sqlDocument == null || sqlDocument.StatementCollection == null)
				return EmptyCollection;

			var statement = sqlDocument.StatementCollection.TakeWhile(s => s.SourcePosition.IndexStart <= cursorPosition - 1).LastOrDefault();

			StatementDescriptionNode currentNode = null;
			if (statement != null)
			{
				currentNode = statement.GetNodeAtPosition(cursorPosition)
				                  ?? statement.GetNearestTerminalToPosition(cursorPosition);
			}

			if (currentNode != null && currentNode.Id == OracleGrammarDescription.Terminals.RightParenthesis &&
				currentNode.PrecedingTerminal != null && currentNode.PrecedingTerminal.PrecedingTerminal != null)
			{
				currentNode = currentNode.PrecedingTerminal.PrecedingTerminal;
			}

			var textToReplace = new String(statementText.Substring(0, cursorPosition).Reverse().TakeWhile(c => c != ' ' && c != '\n' && c != '\t' && c!= '(').Reverse().ToArray());

			if (String.IsNullOrWhiteSpace(textToReplace))
				return EmptyCollection;
			
			var candidates = _oracleParser.GetTerminalCandidates(currentNode);

			return Snippets.SnippetCollection.Where(s => s.Name.ToUpperInvariant().Contains(textToReplace.ToUpperInvariant()) &&
			                                             (s.AllowedTerminals.Length == 0 || s.AllowedTerminals.Select(t => t.Id).Intersect(candidates).Any()))
				.Select(s => new OracleCodeSnippet
				             {
					             Name = s.Name,
					             BaseText = s.Text,
								 Description = s.Description,
					             SourceToReplace = new SourcePosition { IndexStart = cursorPosition - textToReplace.Length, IndexEnd = cursorPosition },
					             Parameters = new List<ICodeSnippetParameter>(
						             s.Parameters.Select(p => new OracleCodeSnippetParameter
						                                      {
							                                      Index = p.Index,
							                                      DefaultValue = p.DefaultValue
						                                      }))
						             .AsReadOnly()
				             }).ToArray();
		}
	}

	public class OracleCodeSnippet : ICodeSnippet
	{
		public string Name { get; set; }
		
		public string Description { get; set; }

		public string BaseText { get; set; }

		public ICollection<ICodeSnippetParameter> Parameters { get; set; }

		public SourcePosition SourceToReplace { get; set; }
	}

	public class OracleCodeSnippetParameter : ICodeSnippetParameter
	{
		public string Name { get; set; }

		public int Index { get; set; }
	
		public string DefaultValue { get; set; }
	}
}