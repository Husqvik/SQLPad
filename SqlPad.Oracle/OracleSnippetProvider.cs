using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleSnippetProvider : ICodeSnippetProvider
	{
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();
		private static readonly ICodeSnippet[] EmptyCollection = new ICodeSnippet[0];

		public ICollection<ICodeSnippet> GetSnippets(string statementText, int cursorPosition)
		{
			var currentNode = _oracleParser.Parse(statementText).Select(s => s.GetNodeAtPosition(cursorPosition)).FirstOrDefault(n => n != null);
			if (currentNode != null && currentNode.Id == OracleGrammarDescription.Terminals.RightParenthesis &&
				currentNode.PreviousTerminal != null && currentNode.PreviousTerminal.PreviousTerminal != null)
			{
				currentNode = currentNode.PreviousTerminal.PreviousTerminal;
			}

			var candidates = _oracleParser.GetTerminalCandidates(currentNode);

			var textToReplace = new String(statementText.Substring(0, cursorPosition).Reverse().TakeWhile(c => c != ' ' && c != '\n' && c != '\t' && c!= '(').Reverse().ToArray());

			if (!String.IsNullOrWhiteSpace(textToReplace) && candidates.Any(t => t.ToUpperInvariant().Contains(textToReplace.ToUpperInvariant())))
			{
				return Snippets.SnippetCollection.Where(s => s.Name.ToUpperInvariant().Contains(textToReplace.ToUpperInvariant()))
					.Select(s => new OracleCodeSnippet
					             {
						             Name = s.Name,
						             BaseText = s.Text,
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

			return EmptyCollection;
		}
	}

	public class OracleCodeSnippet : ICodeSnippet
	{
		public string Name { get; set; }

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