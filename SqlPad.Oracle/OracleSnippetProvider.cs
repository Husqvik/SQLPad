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
			var statement = _oracleParser.Parse(statementText).FirstOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			if (statement == null)
			{
				var offsetToReplace = statementText.Substring(0, cursorPosition).Reverse().TakeWhile(c => c != ' ' || c != '\n' || c != '\t').Count();

				return Snippets.SnippetCollection.Where(s => s.Name.ToUpperInvariant().Contains(statementText.ToUpperInvariant()))
					.Select(s => new OracleCodeSnippet
					             {
						             Name = s.Name,
						             BaseText = s.Text,
									 SourceToReplace = new SourcePosition { IndexStart = cursorPosition - offsetToReplace, IndexEnd = cursorPosition },
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