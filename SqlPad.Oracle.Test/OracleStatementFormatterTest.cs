using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleStatementFormatterTest
	{
		private static readonly OracleSqlParser Parser = new OracleSqlParser();
		private static readonly OracleStatementFormatter Formatter = new OracleStatementFormatter(new SqlFormatterOptions());

		[Test(Description = @"")]
		public void TestBasicFormat()
		{
			const string sourceFormat = "SELECT SELECTION.NAME, COUNT(*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID), MAX(RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID) FROM SELECTION ORDER BY NAME, SELECTION_ID";
			var statements = Parser.Parse(sourceFormat);
			var formattedStatement = Formatter.FormatStatement(statements, 0, 0).Single();

			const string expectedFormat =
@"SELECT
	SELECTION.NAME,
	COUNT(*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID),
	MAX(RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID)
FROM
	SELECTION
ORDER BY
	NAME,
	SELECTION_ID";

			formattedStatement.Text.ShouldBe(expectedFormat);
			formattedStatement.IndextStart.ShouldBe(0);
			formattedStatement.Length.ShouldBe(sourceFormat.Length);
		}
	}
}
