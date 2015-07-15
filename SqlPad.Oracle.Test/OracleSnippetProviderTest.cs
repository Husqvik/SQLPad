using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Test;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleSnippetProviderTest : TemporaryDirectoryTestFixture
	{
		private static readonly OracleSnippetProvider SnippetProvider = new OracleSnippetProvider();

		[SetUp]
		public void SetUpSnippets()
		{
			ConfigurationProvider.SetSnippetsFolder(TempDirectoryName);
			ConfigurationProvider.SetCodeGenerationItemFolder(TempDirectoryName);
			File.Copy(@"TestFiles\TestSnippet.xml", Path.Combine(TempDirectoryName, "TestSnippet.xml"), true);			
		}

		[Test(Description = @"")]
		public void TestSnippetSuggestionWithinStatementWhileTyping()
		{
			const string statementText = "SELECT DUMMY FROM\r\nD\r\nDUAL";
			var snippets = SnippetProvider.GetSnippets(statementText, 21, TestFixture.DatabaseModel).ToArray();
			snippets.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSnippetWithoutParameterAndAllowedTerminals()
		{
			var snippets = SnippetProvider.GetSnippets("TES", 3, TestFixture.DatabaseModel).ToArray();
			snippets.Length.ShouldBe(1);
			snippets[0].Name.ShouldBe("Test");
		}
	}
}
