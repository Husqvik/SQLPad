using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

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
			const string sourceFormat = "SELECT SELECTION.NAME, COUNT(*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID) DUMMY_COUNT, MAX(RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID) FROM SELECTION LEFT JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID LEFT JOIN TARGETGROUP ON RESPONDENTBUCKET.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID ORDER BY NAME, SELECTION_ID";
			var statements = Parser.Parse(sourceFormat);
			var executionContext = new CommandExecutionContext(sourceFormat, 0, statements, null);
			Formatter.ExecutionHandler.ExecutionHandler(executionContext);

			const string expectedFormat =
@"SELECT
	SELECTION.NAME,
	COUNT(*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID) DUMMY_COUNT,
	MAX(RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID)
FROM
	SELECTION
	LEFT JOIN RESPONDENTBUCKET
		ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID
	LEFT JOIN TARGETGROUP
		ON RESPONDENTBUCKET.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID
ORDER BY
	NAME,
	SELECTION_ID";

			executionContext.SegmentsToReplace.Count.ShouldBe(1);
			var formattedSegment = executionContext.SegmentsToReplace.First();
			formattedSegment.Text.ShouldBe(expectedFormat);
			formattedSegment.IndextStart.ShouldBe(0);
			formattedSegment.Length.ShouldBe(sourceFormat.Length);
		}
	}
}
