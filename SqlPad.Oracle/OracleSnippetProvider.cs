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
				return Snippets.SnippetCollection.Where(s => s.Name.ToUpperInvariant().Contains(statementText.ToUpperInvariant()))
					.Select(s => new OracleCodeSnippet { Name = s.Name, BaseText = s.Text }).ToArray();
			}

			return EmptyCollection;
		}
	}

	public class OracleCodeSnippet : ICodeSnippet
	{
		public string Name { get; set; }

		public string BaseText { get; set; }
	}
}