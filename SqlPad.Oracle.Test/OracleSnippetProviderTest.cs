using System;
using System.IO;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleSnippetProviderTest
	{
		private static readonly OracleSnippetProvider SnippetProvider = new OracleSnippetProvider();

		public OracleSnippetProviderTest()
		{
			var sqlPadDirectory = new Uri(Path.GetDirectoryName(typeof(Snippets).Assembly.CodeBase)).LocalPath;
			Snippets.SelectSnippetDirectory(Path.Combine(sqlPadDirectory, Snippets.SnippetDirectoryName));
		}

		[Test(Description = @"")]
		public void TestSnippetSuggestionWithinStatementWhileTyping()
		{
			const string statementText = "SELECT DUMMY FROM D DUAL";
			var snippets = SnippetProvider.GetSnippets(statementText, 19);
			snippets.Count.ShouldBe(0);
		}
	}
}
