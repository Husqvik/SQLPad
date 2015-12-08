using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleMultiNodeEditorDataProviderTest
	{
		private static readonly OracleMultiNodeEditorDataProvider MultiNodeEditorDataProvider = new OracleMultiNodeEditorDataProvider();
		private static readonly SqlDocumentRepository DocumentRepository = new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), OracleTestDatabaseModel.Instance);

		const string CteSqlText =
@"WITH cte(val) AS (
	SELECT 1 FROM DUAL UNION ALL
	SELECT cte.val + 1 FROM cte WHERE val < 5
)
SELECT
	cte.VAL
FROM
	cte";

		private static MultiNodeEditorData GetMultiNodeEditorData(string sqlText, int caretOffset)
		{
			DocumentRepository.UpdateStatements(sqlText);
			var executionContext = new ActionExecutionContext(sqlText, caretOffset, caretOffset, 0, DocumentRepository);
			return MultiNodeEditorDataProvider.GetMultiNodeEditorData(executionContext);
		}

		[Test(Description = @"")]
		public void TestRecursiveCteNodesAtCteAlias()
		{
			var multiNodeEditorData = GetMultiNodeEditorData(CteSqlText, 5);

			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.CurrentNode.SourcePosition.IndexStart.ShouldBe(5);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(4);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(59);
			segments[1].IndexStart.ShouldBe(76);
			segments[2].IndexStart.ShouldBe(107);
			segments[3].IndexStart.ShouldBe(123);
		}

		[Test(Description = @"")]
		public void TestRecursiveCteNodesAtOuterCteReference()
		{
			var multiNodeEditorData = GetMultiNodeEditorData(CteSqlText, 126);

			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.CurrentNode.SourcePosition.IndexStart.ShouldBe(123);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(4);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(5);
			segments[1].IndexStart.ShouldBe(59);
			segments[2].IndexStart.ShouldBe(76);
			segments[3].IndexStart.ShouldBe(107);
		}

		[Test(Description = @"")]
		public void TestRecursiveCteNodesAtInnerObjectReference()
		{
			var multiNodeEditorData = GetMultiNodeEditorData(CteSqlText, 76);

			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.CurrentNode.SourcePosition.IndexStart.ShouldBe(76);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(4);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(5);
			segments[1].IndexStart.ShouldBe(59);
			segments[2].IndexStart.ShouldBe(107);
			segments[3].IndexStart.ShouldBe(123);
		}

		[Test(Description = @"")]
		public void TestRecursiveCteNodesAtOuterColumnReference()
		{
			var multiNodeEditorData = GetMultiNodeEditorData(CteSqlText, 107);

			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.CurrentNode.SourcePosition.IndexStart.ShouldBe(107);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(4);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(5);
			segments[1].IndexStart.ShouldBe(59);
			segments[2].IndexStart.ShouldBe(76);
			segments[3].IndexStart.ShouldBe(123);
		}

		[Test(Description = @"")]
		public void TestAtPivotColumnAlias()
		{
			const string sqlText =
@"SELECT
    DUMMY, ONE
FROM (
    SELECT DUMMY, 1 VALUE FROM DUAL)
PIVOT (
    COUNT(*)
    FOR VALUE IN (1 ONE)
)";

			var multiNodeEditorData = GetMultiNodeEditorData(sqlText, 113);

			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.CurrentNode.SourcePosition.IndexStart.ShouldBe(113);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(1);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(19);
		}

		[Test(Description = @"")]
		public void TestObjectAliasNodesWithoutCte()
		{
			const string sqlText = @"SELECT ALIAS.DUMMY FROM DUAL ALIAS";
			var multiNodeEditorData = GetMultiNodeEditorData(sqlText, 29);
			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestMultipleAliasedCteUsages()
		{
			const string sqlText =
@"WITH data (val) AS (
    SELECT 1 FROM DUAL
)
SELECT
    *
FROM
    data A
WHERE
    EXISTS (
        SELECT
            NULL
        FROM
            data B
        WHERE
            A.val = B.val
    )";
			var multiNodeEditorData = GetMultiNodeEditorData(sqlText, 5);
			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(2);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(74);
			segments[1].IndexStart.ShouldBe(163);
		}

		[Test(Description = @"")]
		public void TestMultipleAliasedCteUsagesAtUsage()
		{
			const string sqlText =
@"WITH data (val) AS (
    SELECT 1 FROM DUAL
)
SELECT
    *
FROM
    data A
WHERE
    EXISTS (
        SELECT
            NULL
        FROM
            data B
        WHERE
            A.val = B.val
    )";
			var multiNodeEditorData = GetMultiNodeEditorData(sqlText, 74);
			multiNodeEditorData.CurrentNode.ShouldNotBe(null);
			multiNodeEditorData.SynchronizedSegments.Count.ShouldBe(2);
			var segments = multiNodeEditorData.SynchronizedSegments.OrderBy(s => s.IndexStart).ToArray();
			segments[0].IndexStart.ShouldBe(5);
			segments[1].IndexStart.ShouldBe(163);
		}
	}
}
