using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleStatementFormatterTest
	{
		private readonly SqlDocumentRepository _documentRepository = TestFixture.CreateDocumentRepository();
		private static readonly OracleStatementFormatter Formatter = new OracleStatementFormatter(new SqlFormatterOptions());

		[Test(Description = @"")]
		public void TestBasicFormat()
		{
			const string sourceFormat = "SELECT SELECTION.NAME, COUNT(*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID) DUMMY_COUNT, MAX(RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID) FROM SELECTION LEFT JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID LEFT JOIN TARGETGROUP ON RESPONDENTBUCKET.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID ORDER BY NAME, SELECTION_ID";
			var executionContext = ExecututeFormatCommand(sourceFormat);

			const string expectedFormat =
@"SELECT
	SELECTION.NAME,
	COUNT (*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID) DUMMY_COUNT,
	MAX (RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID)
FROM
	SELECTION
	LEFT JOIN RESPONDENTBUCKET
		ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID
	LEFT JOIN TARGETGROUP
		ON RESPONDENTBUCKET.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID
ORDER BY
	NAME,
	SELECTION_ID";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		[Test(Description = @"")]
		public void TestSingleLineFormat()
		{
			const string sourceFormat =
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
	SELECTION_ID;";

			_documentRepository.UpdateStatements(sourceFormat);
			var executionContext = new CommandExecutionContext(sourceFormat, 0, 0, 0, _documentRepository);
			Formatter.SingleLineExecutionHandler.ExecutionHandler(executionContext);

			const string expectedFormat = "SELECT SELECTION.NAME, COUNT (*) OVER (PARTITION BY NAME ORDER BY RESPONDENTBUCKET_ID, SELECTION_ID) DUMMY_COUNT, MAX (RESPONDENTBUCKET_ID) KEEP (DENSE_RANK FIRST ORDER BY PROJECT_ID, SELECTION_ID) FROM SELECTION LEFT JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID LEFT JOIN TARGETGROUP ON RESPONDENTBUCKET.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID ORDER BY NAME, SELECTION_ID;";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		[Test(Description = @"")]
		public void TestMultipleStatementSingleLineFormat()
		{
			const string sourceFormat =
@"SELECT
	DUMMY
FROM
	DUAL
;

SELECT
	DUMMY
FROM
	DUAL @ DBLINK
;";

			_documentRepository.UpdateStatements(sourceFormat);
			var executionContext = new CommandExecutionContext(sourceFormat, 0, 0, sourceFormat.Length, _documentRepository);
			Formatter.SingleLineExecutionHandler.ExecutionHandler(executionContext);

			var expectedFormat = "SELECT DUMMY FROM DUAL;" + Environment.NewLine + "SELECT DUMMY FROM DUAL@DBLINK;";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		[Test(Description = @"")]
		public void TestAtSymbolFormatting()
		{
			const string sourceFormat = @"SELECT DUMMY FROM DUAL@DBLINK";

			var executionContext = ExecututeFormatCommand(sourceFormat);

			const string expectedFormat =
@"SELECT
	DUMMY
FROM
	DUAL@DBLINK";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		private static void AssertFormattedResult(CommandExecutionContext executionContext, string expectedFormat)
		{
			executionContext.SegmentsToReplace.Count.ShouldBe(1);
			var formattedSegment = executionContext.SegmentsToReplace.First();
			formattedSegment.Text.ShouldBe(expectedFormat);
			formattedSegment.IndextStart.ShouldBe(0);
			formattedSegment.Length.ShouldBe(executionContext.StatementText.Length);
		}

		private CommandExecutionContext ExecututeFormatCommand(string sourceFormat)
		{
			_documentRepository.UpdateStatements(sourceFormat);
			var executionContext = new CommandExecutionContext(sourceFormat, 0, 0, 0, _documentRepository);
			Formatter.ExecutionHandler.ExecutionHandler(executionContext);
			return executionContext;
		}

		[Test(Description = @"")]
		public void TestFormatDeleteStatement()
		{
			const string sourceFormat = "DELETE FROM DUAL WHERE DUMMY IN (SELECT NULL FROM DUAL)";
			var executionContext = ExecututeFormatCommand(sourceFormat);

			const string expectedFormat =
@"DELETE FROM DUAL
WHERE
	DUMMY IN (
	SELECT
		NULL
	FROM
		DUAL)";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		[Test(Description = @"")]
		public void TestFormatInlineView()
		{
			const string sourceFormat = "SELECT DUMMY FROM (SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) X) X";
			var executionContext = ExecututeFormatCommand(sourceFormat);

			const string expectedFormat =
@"SELECT
	DUMMY
FROM (
	SELECT
		DUMMY
	FROM (
		SELECT
			DUMMY
		FROM
			DUAL
	) X
) X";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		[Test(Description = @"")]
		public void TestFormatInClauseWithSubquery()
		{
			const string sourceFormat = "SELECT DUMMY FROM (SELECT DUMMY FROM DUAL WHERE DUMMY IN (SELECT DUMMY FROM DUAL) OR DUMMY IN (SELECT DUMMY FROM DUAL)) X";
			var executionContext = ExecututeFormatCommand(sourceFormat);

			const string expectedFormat =
@"SELECT
	DUMMY
FROM (
	SELECT
		DUMMY
	FROM
		DUAL
	WHERE
		DUMMY IN (
		SELECT
			DUMMY
		FROM
			DUAL)
		OR DUMMY IN (
		SELECT
			DUMMY
		FROM
			DUAL)
) X";

			AssertFormattedResult(executionContext, expectedFormat);
		}

		[Test(Description = @"")]
		public void TestFormatScalarSubquery()
		{
			const string sourceFormat = "SELECT (SELECT DUMMY FROM DUAL) SQ1, (SELECT DUMMY FROM DUAL) SQ2 FROM DUAL";
			var executionContext = ExecututeFormatCommand(sourceFormat);

			const string expectedFormat =
@"SELECT
	(
	SELECT
		DUMMY
	FROM
		DUAL) SQ1,
	(
	SELECT
		DUMMY
	FROM
		DUAL) SQ2
FROM
	DUAL";

			AssertFormattedResult(executionContext, expectedFormat);
		}
	}
}
