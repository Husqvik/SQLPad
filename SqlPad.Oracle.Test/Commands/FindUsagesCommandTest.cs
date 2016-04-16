using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

namespace SqlPad.Oracle.Test.Commands
{
	[TestFixture]
	public class FindUsagesCommandTest
	{
		private SqlDocumentRepository _documentRepository;

		private const string FindUsagesStatementText =
		@"SELECT
	NAME,
	TARGETGROUP_NAME,
	PNAME,
	RESPONDENTBUCKET_NAME,
	CONSTANT_COLUMN
FROM
	(SELECT
		NAME,
		TARGETGROUP_NAME || TARGETGROUP_NAME TARGETGROUP_NAME,
		PROJECT_NAME PNAME,
		RESPONDENTBUCKET_NAME,
		CONSTANT_COLUMN
	FROM
		(SELECT
			S.NAME,
			TG.NAME TARGETGROUP_NAME,
			P.NAME PROJECT_NAME,
			RB.NAME RESPONDENTBUCKET_NAME,
			'My column1' CONSTANT_COLUMN,
			'My column2'
		FROM
			HUSQVIK.SELECTION S
			LEFT JOIN RESPONDENTBUCKET RB ON S.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID
			LEFT JOIN TARGETGROUP TG ON RB.TARGETGROUP_ID = TG.TARGETGROUP_ID AND RB.NAME = TG.NAME
			JOIN PROJECT P ON S.PROJECT_ID = P.PROJECT_ID
		WHERE
			TG.NAME IN ('X1', 'X2') OR S.NAME IS NOT NULL OR P.NAME <> ''
		)
	) TMP
ORDER BY
	TMP.RESPONDENTBUCKET_NAME";

		private const string FindFunctionUsagesStatementText =
@"SELECT
	COUNT(*) OVER () CNT1,
	COUNT(1) OVER () CNT2,
	FIRST_VALUE(DUMMY) IGNORE NULLS OVER () FIRST_VAL1,
	FIRST_VALUE(DUMMY) OVER () FIRST_VAL2,
	COALESCE(DUMMY, 1) COALESCE1,
	COALESCE(DUMMY, 1) COALESCE2,
	TO_CHAR(0) C1,
	TO_CHAR(0) C2
FROM
	DUAL";

		private const string FindLiteralUsagesStatementText =
@"SELECT
	:BV,
	'123',
	'456',
	'123',
	'456',
	123,
	456,
	123,
	456,
	:BV
FROM
	SELECTION
WHERE
	'456' != '123'
	AND 1 = :BV";

		[SetUp]
		public void SetUp()
		{
			_documentRepository = TestFixture.CreateDocumentRepository();
		}

		[Test]
		public void TestFindObjectUsages()
		{
			const string statementText = "SELECT \"SELECTION\".RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION WHERE SELECTION.NAME = NAME GROUP BY SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(SELECTION.SELECTION_ID) = COUNT(SELECTION_ID)";

			var foundSegments = FindUsagesOrdered(statementText, 8);
			foundSegments.Count.ShouldBe(5);
			foundSegments[0].Length.ShouldBe("\"SELECTION\"".Length);
			foundSegments[1].Length.ShouldBe("SELECTION".Length);
		}

		[Test]
		public void TestFindObjectWithAliasUsages()
		{
			const string statementText = "SELECT S.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION \"S\" WHERE S.NAME = NAME GROUP BY S.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(S.SELECTION_ID) = COUNT(SELECTION_ID)";

			var foundSegments = FindUsagesOrdered(statementText, 56);
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].Length.ShouldBe("S".Length);
			foundSegments[1].Length.ShouldBe("SELECTION".Length);
			foundSegments[2].Length.ShouldBe("\"S\"".Length);
			foundSegments[3].Length.ShouldBe("S".Length);
		}

		[Test]
		public void TestFindSchemaUsages()
		{
			const string statementText = "SELECT HUSQVIK.SELECTION.PROJECT_ID FROM (SELECT HUSQVIK.SELECTION.NAME FROM HUSQVIK.SELECTION), HUSQVIK.SELECTION";

			var foundSegments = FindUsagesOrdered(statementText, 9);
			foundSegments.Count.ShouldBe(4);
			foundSegments.ForEach(s => s.Length.ShouldBe("HUSQVIK".Length));
		}

		[Test]
		public void TestBasicFindColumnUsages()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 11);
			foundSegments.Count.ShouldBe(4);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(106);
			foundSegments[2].IndextStart.ShouldBe(262);
			foundSegments[3].IndextStart.ShouldBe(709);
			foundSegments.ForEach(s => s.Length.ShouldBe(4));
		}

		[Test]
		public void TestFindColumnUsagesOfColumnAliases()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 40);
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].IndextStart.ShouldBe(37);
			foundSegments[0].Length.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(173);
			foundSegments[1].Length.ShouldBe(12);
			foundSegments[2].IndextStart.ShouldBe(186);
			foundSegments[2].Length.ShouldBe(5);
			foundSegments[3].IndextStart.ShouldBe(304);
			foundSegments[3].Length.ShouldBe(4);
			foundSegments[4].IndextStart.ShouldBe(309);
			foundSegments[4].Length.ShouldBe(12);
			foundSegments[5].IndextStart.ShouldBe(731);
			foundSegments[5].Length.ShouldBe(4);
		}

		[Test]
		public void TestFindColumnUsagesOfIndirectColumnReferenceAtAliasNode()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 25);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(17);
			foundSegments[0].Length.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(152);
			foundSegments[1].Length.ShouldBe(16);
		}

		[Test]
		public void TestFindColumnUsagesInParentQueryBlockWhenCursorInWhereClauseInChildQueryBlock()
		{
			var foundSegments = FindUsagesOrdered("SELECT DUMMY FROM (SELECT DUMMY FROM DUAL WHERE DUMMY = 'X')", 49);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(7);
			foundSegments[0].Length.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(26);
			foundSegments[1].Length.ShouldBe(5);
			foundSegments[2].IndextStart.ShouldBe(48);
			foundSegments[2].Length.ShouldBe(5);
		}

		[Test]
		public void TestFindColumnUsagesAtPivotColumnUsage()
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

			var foundSegments = FindUsagesOrdered(sqlText, 19);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(19);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[1].IndextStart.ShouldBe(113);
			foundSegments[1].Length.ShouldBe(3);
		}

		[Test]
		public void TestFindColumnUsagesOfIndirectColumnReferenceAtColumnNode()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 121);
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].IndextStart.ShouldBe(115);
			foundSegments[0].Length.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(135);
			foundSegments[1].Length.ShouldBe(16);
			foundSegments[2].IndextStart.ShouldBe(275);
			foundSegments[2].Length.ShouldBe(4);
			foundSegments[3].IndextStart.ShouldBe(280);
			foundSegments[3].Length.ShouldBe(16);
			foundSegments[4].IndextStart.ShouldBe(612);
			foundSegments[4].Length.ShouldBe(4);
			foundSegments[5].IndextStart.ShouldBe(683);
			foundSegments[5].Length.ShouldBe(4);
		}

		[Test]
		public void TestFindColumnUsagesInRecursiveCommonTableExpression()
		{
			var foundSegments = FindUsagesOrdered("WITH GENERATOR(VAL) AS (SELECT 1 FROM DUAL UNION ALL SELECT VAL + 1 FROM GENERATOR WHERE VAL <= 10) SELECT VAL FROM GENERATOR", 90);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(60);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[1].IndextStart.ShouldBe(89);
			foundSegments[1].Length.ShouldBe(3);
		}

		[Test]
		public void TestFindColumnUsagesAtMeasureDeclarationAlias()
		{
			const string sql =
@"SELECT
	DIMENSION, MEASURE1
FROM
	DUAL
MODEL
	DIMENSION BY (0 DIMENSION)
	MEASURES (0 MEASURE1)
	RULES (
		MEASURE1[ANY] = 0
    )";

			var foundSegments = FindUsagesOrdered(sql, 92);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(20);
			foundSegments[0].Length.ShouldBe(8);
			foundSegments[1].IndextStart.ShouldBe(92);
			foundSegments[1].Length.ShouldBe(8);
			foundSegments[2].IndextStart.ShouldBe(115);
			foundSegments[2].Length.ShouldBe(8);
		}

		[Test]
		public void TestFindColumnUsagesAtDimensionDeclarationAlias()
		{
			const string sql =
@"SELECT
	DIMENSION, MEASURE1
FROM
	DUAL
MODEL
	DIMENSION BY (0 DIMENSION)
	MEASURES (0 MEASURE1)
	RULES (
		MEASURE1[ANY] = 0
    )";

			var foundSegments = FindUsagesOrdered(sql, 67);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[0].Length.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(67);
			foundSegments[1].Length.ShouldBe(9);
		}

		[Test]
		public void TestFindColumnUsagesAtDimensionAtSelectColumn()
		{
			const string sql =
@"SELECT
	DIMENSION, MEASURE1
FROM
	DUAL
MODEL
	DIMENSION BY (0 DIMENSION)
	MEASURES (0 MEASURE1)
	RULES (
		MEASURE1[ANY] = 0
    )";

			var foundSegments = FindUsagesOrdered(sql, 9);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[0].Length.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(67);
			foundSegments[1].Length.ShouldBe(9);
		}

		[Test]
		public void TestFindColumnUsagesAtColumnAliasWithinSubqueryWithOuterModelClause()
		{
			const string sql =
@"SELECT
	DIMENSION, VAL
FROM
	(SELECT 0 VAL FROM DUAL)
MODEL
	DIMENSION BY (0 DIMENSION)
	MEASURES (VAL)
	RULES (
		VAL[ANY] = 0
    )";

			var foundSegments = FindUsagesOrdered(sql, 42);
			foundSegments.Count.ShouldBe(4);
			foundSegments[0].IndextStart.ShouldBe(20);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[1].IndextStart.ShouldBe(42);
			foundSegments[1].Length.ShouldBe(3);
			foundSegments[2].IndextStart.ShouldBe(105);
			foundSegments[2].Length.ShouldBe(3);
			foundSegments[3].IndextStart.ShouldBe(123);
			foundSegments[3].Length.ShouldBe(3);
		}

		[Test]
		public void TestFindColumnUsagesAtMeasureDirectReferencedColumnWithinModelClause()
		{
			const string sql =
@"SELECT
	DIMENSION, VAL
FROM
	(SELECT 0 VAL FROM DUAL)
MODEL
	DIMENSION BY (0 DIMENSION)
	MEASURES (VAL)
	RULES (
		VAL[ANY] = 0
    )";

			var foundSegments = FindUsagesOrdered(sql, 105);
			foundSegments.Count.ShouldBe(4);
			foundSegments[0].IndextStart.ShouldBe(20);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[1].IndextStart.ShouldBe(42);
			foundSegments[1].Length.ShouldBe(3);
			foundSegments[2].IndextStart.ShouldBe(105);
			foundSegments[2].Length.ShouldBe(3);
			foundSegments[3].IndextStart.ShouldBe(123);
			foundSegments[3].Length.ShouldBe(3);
		}

		private const string PivotTableSql =
@"SELECT
    OBJECT_TYPE, HUSQVIK, SYSTEM, SYS, CTXSYS
FROM
(
    SELECT
        OBJECT_TYPE, OWNER
    FROM
        ALL_OBJECTS
    WHERE
        OWNER IN ('HUSQVIK', 'SYSTEM', 'SYS', 'CTXSYS')
)
PIVOT
(
    COUNT(OWNER)
    FOR OWNER IN
    (
        'HUSQVIK' HUSQVIK,
        'SYSTEM' SYSTEM,
        'SYS' SYS,
        'CTXSYS' CTXSYS
    )
)
ORDER BY
    OBJECT_TYPE";

		[Test]
		public void TestFindPivotTableColumnUsages()
		{
			var foundSegments = FindUsagesOrdered(PivotTableSql, 12);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(12);
			foundSegments[0].Length.ShouldBe(11);
			foundSegments[1].IndextStart.ShouldBe(84);
			foundSegments[1].Length.ShouldBe(11);
			foundSegments[2].IndextStart.ShouldBe(382);
			foundSegments[2].Length.ShouldBe(11);
		}

		[Test]
		public void TestFindPivotTableColumnUsagesAtColumnWithinInnerSubquery()
		{
			var foundSegments = FindUsagesOrdered(PivotTableSql, 84);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(12);
			foundSegments[0].Length.ShouldBe(11);
			foundSegments[1].IndextStart.ShouldBe(84);
			foundSegments[1].Length.ShouldBe(11);
			foundSegments[2].IndextStart.ShouldBe(382);
			foundSegments[2].Length.ShouldBe(11);
		}

		[Test]
		public void TestFindColumnUsagesOfComputedColumnAtUsage()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 80);
			ValidateCommonResults3(foundSegments);
		}

		[Test]
		public void TestFindColumnUsagesOfComputedColumnAtDefinition()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 382);
			ValidateCommonResults3(foundSegments);
		}

		private void ValidateCommonResults3(List<TextSegment> foundSegments)
		{
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(71);
			foundSegments[1].IndextStart.ShouldBe(222);
			foundSegments[2].IndextStart.ShouldBe(375);
			foundSegments.ForEach(s => s.Length.ShouldBe(15));
		}

		[Test]
		public void TestFindObjectUsagesAtCommonTableExpressionDefinition()
		{
			const string statement = "WITH CTE AS (SELECT SELECTION.NAME FROM SELECTION) SELECT CTE.NAME FROM CTE";

			var foundSegments = FindUsagesOrdered(statement, 6);
			ValidateCommonResults2(foundSegments);
		}

		[Test]
		public void TestFindObjectUsagesAtCommonTableExpressionUsage()
		{
			const string statement = "WITH CTE AS (SELECT SELECTION.NAME FROM SELECTION) SELECT CTE.NAME FROM CTE";

			var foundSegments = FindUsagesOrdered(statement, 72);
			ValidateCommonResults2(foundSegments);
		}

		[Test]
		public void TestFindColumnUsagesPassedThroughModelClause()
		{
			const string statement =
@"SELECT
	D
FROM
	(SELECT DUMMY FROM DUAL)
	MODEL
		DIMENSION BY (DUMMY D)
		MEASURES (0 MEASURE)
		RULES (
			MEASURE[ANY] = 0
	    )";

			var foundSegments = FindUsagesOrdered(statement, 9);
			foundSegments.Count.ShouldBe(4);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(27);
			foundSegments[2].IndextStart.ShouldBe(69);
			foundSegments[3].IndextStart.ShouldBe(75);
		}

		[Test]
		public void TestFindObjectUsagesAtRecursiveCommonTableExpressionDefinition()
		{
			var foundSegments = FindUsagesOrdered("WITH GENERATOR(VAL) AS (SELECT 1 FROM DUAL UNION ALL SELECT VAL + 1 FROM GENERATOR WHERE VAL <= 10) SELECT VAL FROM GENERATOR", 6);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(5);
			foundSegments[0].Length.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(73);
			foundSegments[1].Length.ShouldBe(9);
			foundSegments[2].IndextStart.ShouldBe(116);
			foundSegments[2].Length.ShouldBe(9);
		}

		private void ValidateCommonResults2(List<TextSegment> foundSegments)
		{
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(58);
			foundSegments[2].IndextStart.ShouldBe(72);
			foundSegments.ForEach(s => s.Length.ShouldBe(3));
		}

		[Test]
		public void TestFindColumnUsagesAtJoinCondition()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 602);
			ValidateCommonResults1(foundSegments);
		}

		[Test]
		public void TestFindColumnUsagesInOrderByClause()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 771);
			ValidateCommonResults1(foundSegments);
		}

		[Test]
		public void TestFindColumnUsagesAtAliasDefinition()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 350);
			ValidateCommonResults1(foundSegments);
		}

		[Test]
		public void TestFindObjectUsageInOrderByClause()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 767);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(751);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[1].IndextStart.ShouldBe(767);
			foundSegments[1].Length.ShouldBe(3);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		[Test]
		public void TestFindObjectUsagesInCorrelatedSubquery()
		{
			const string sql =
@"WITH data (val) AS (
    SELECT 1 FROM DUAL
)
SELECT
    *
FROM
    data
WHERE
    EXISTS (
        SELECT
            NULL
        FROM
            data B
        WHERE
            data.val = B.val
    )";

			var foundSegments = FindUsagesOrdered(sql, 74);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(5);
			foundSegments[0].Length.ShouldBe(4);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[1].IndextStart.ShouldBe(74);
			foundSegments[1].Length.ShouldBe(4);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[2].IndextStart.ShouldBe(196);
			foundSegments[2].Length.ShouldBe(4);
			foundSegments[2].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		[Test]
		public void TestFindColumnUsagesInCommonTableExpression()
		{
			const string sql =
@"WITH data (seq) AS (
	SELECT 1 FROM dual
)
SELECT
	seq
FROM
	data";

			var foundSegments = FindUsagesOrdered(sql, 11);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(11);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[1].IndextStart.ShouldBe(55);
			foundSegments[1].Length.ShouldBe(3);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		private void ValidateCommonResults1(IList<TextSegment> foundSegments)
		{
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].IndextStart.ShouldBe(46);
			foundSegments[0].Length.ShouldBe(21);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[1].IndextStart.ShouldBe(196);
			foundSegments[1].Length.ShouldBe(21);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[2].IndextStart.ShouldBe(330);
			foundSegments[2].Length.ShouldBe(4);
			foundSegments[2].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[3].IndextStart.ShouldBe(335);
			foundSegments[3].Length.ShouldBe(21);
			foundSegments[3].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[4].IndextStart.ShouldBe(602);
			foundSegments[4].Length.ShouldBe(4);
			foundSegments[4].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[5].IndextStart.ShouldBe(771);
			foundSegments[5].Length.ShouldBe(21);
			foundSegments[5].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		[Test]
		public void TestAsteriskNotHighlightedWhenFindUsages()
		{
			const string statement = "SELECT NAME FROM (SELECT * FROM (SELECT NAME FROM SELECTION))";

			var foundSegments = FindUsagesOrdered(statement, 7);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(7);
			foundSegments[1].IndextStart.ShouldBe(40);
		}

		[Test]
		public void TestOrderByUsageWhenAtColumnAlias()
		{
			const string statement = "SELECT NULL counter FROM DUAL ORDER BY counter";

			var foundSegments = FindUsagesOrdered(statement, 12);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(12);
			foundSegments[1].IndextStart.ShouldBe(39);
		}

		[Test]
		public void TestFindGrammarSpecificFunctionUsages()
		{
			var foundSegments = FindUsagesOrdered(FindFunctionUsagesStatementText, 9);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(34);
			foundSegments.ForEach(s => s.Length.ShouldBe(5));
		}

		[Test]
		public void TestFindGenericSqlFunctionUsages()
		{
			var foundSegments = FindUsagesOrdered(FindFunctionUsagesStatementText, 154);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(154);
			foundSegments[1].IndextStart.ShouldBe(186);
			foundSegments.ForEach(s => s.Length.ShouldBe(8));
		}

		[Test]
		public void TestFindLiteralUsages()
		{
			var foundSegments = FindUsagesOrdered(FindLiteralUsagesStatementText, 17);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(34);
			foundSegments[2].IndextStart.ShouldBe(120);
			foundSegments.ForEach(s => s.Length.ShouldBe(5));
		}

		[Test]
		public void TestFindBindVariableUsages()
		{
			var foundSegments = FindUsagesOrdered(FindLiteralUsagesStatementText, 10);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(10);
			foundSegments[1].IndextStart.ShouldBe(81);
			foundSegments[2].IndextStart.ShouldBe(137);
			foundSegments.ForEach(s => s.Length.ShouldBe(2));
		}

		[Test]
		public void TestFindColumnUsagesWithAliasedColumnWithInvalidReference()
		{
			const string statement = "SELECT CPU_SECONDS X FROM (SELECT CPU_TIME / 1000000 CPU_SECONDS FROM V$SESSION)";

			var foundSegments = FindUsagesOrdered(statement, 10);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(7);
			foundSegments[0].Length.ShouldBe(11);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[1].IndextStart.ShouldBe(19);
			foundSegments[1].Length.ShouldBe(1);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[2].IndextStart.ShouldBe(53);
			foundSegments[2].DisplayOptions.ShouldBe(DisplayOptions.Definition);
		}

		[Test]
		public void TestFindColumnUsagesAtRecursiveCommonTableExpressionExplicitColumnDefinition()
		{
			const string statement =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 10
)
SELECT VAL FROM CTE";

			var foundSegments = FindUsagesOrdered(statement, 10);
			foundSegments.Count.ShouldBe(4);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[1].IndextStart.ShouldBe(61);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[2].IndextStart.ShouldBe(84);
			foundSegments[2].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[3].IndextStart.ShouldBe(104);
			foundSegments[3].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		[Test]
		public void TestFindObjectUsagesWithinRecursiveCommonTableExpression()
		{
			const string statement =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 10
)
SELECT VAL FROM CTE";

			var foundSegments = FindUsagesOrdered(statement, 75);
			foundSegments.Count.ShouldBe(2);
		}

		private List<TextSegment> FindUsagesOrdered(string statementText, int currentPosition)
		{
			_documentRepository.UpdateStatements(statementText);
			var executionContext = new ActionExecutionContext(statementText, currentPosition, currentPosition, 0, _documentRepository);
			FindUsagesCommand.FindUsages.ExecutionHandler(executionContext);
			return executionContext.SegmentsToReplace.OrderBy(s => s.IndextStart).ToList();
		}
	}
}
