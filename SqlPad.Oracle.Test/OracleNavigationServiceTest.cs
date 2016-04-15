using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleNavigationServiceTest
	{
		private readonly OracleNavigationService _navigationService = new OracleNavigationService();
		private readonly SqlDocumentRepository _documentRepository = TestFixture.CreateDocumentRepository();

		private ActionExecutionContext CreateExecutionContext(string statementText, int currentPosition)
		{
			_documentRepository.UpdateStatements(statementText);
			return new ActionExecutionContext(statementText, currentPosition, currentPosition, currentPosition, _documentRepository);
		}

		[Test]
		public void TestNavigateToQueryBlockRootInInnerQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 89));
			targetIndex.ShouldBe(0);
		}

		[Test]
		public void TestNavigateToQueryBlockRootInOuterQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 76));
			targetIndex.ShouldBe(34);
		}

		[Test]
		public void TestNavigateToQueryBlockRootAtFreeSpace()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM\t\r\n PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 80));
			targetIndex.ShouldBe(34);
		}

		[Test]
		public void TestNavigateToQueryBlockRootOutsideStatement()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P ";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 90));
			targetIndex.ShouldBe(null);
		}

		[Test]
		public void TestNavigateToColumnDefinition()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 10));
			targetIndex.ShouldBe(49);
		}

		[Test]
		public void TestNavigateToColumnDefinitionInAsteriskClause()
		{
			const string query = "SELECT P.PROJECT_ID, P.NAME FROM (SELECT * FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 13));
			targetIndex.ShouldBe(41);
		}

		[Test]
		public void TestNavigateToColumnDefinitionWhenDatabaseModelNotLoaded()
		{
			const string query = "SELECT P.PROJECT_ID, P.NAME FROM (SELECT * FROM PROJECT) P";

			var databaseModel = new OracleTestDatabaseModel();
			databaseModel.AllObjects.Clear();
			var documentRepository = new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), databaseModel);
			documentRepository.UpdateStatements(query);

			const int caretOffset = 13;
			var executionContext = new ActionExecutionContext(query, caretOffset, caretOffset, caretOffset, documentRepository);
			var targetIndex = _navigationService.NavigateToDefinition(executionContext);
			targetIndex.ShouldBe(null);
		}

		[Test]
		public void TestNavigateToColumnDefinitionWithCommonTableExpressionExplicitColumnNames()
		{
			const string query = "WITH GENERATOR(VAL) AS (SELECT 1 FROM DUAL UNION ALL SELECT VAL + 1 FROM GENERATOR WHERE VAL <= 10) SELECT VAL FROM GENERATOR";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 90));
			targetIndex.ShouldBe(60);
		}

		[Test]
		public void TestNavigateToObjectDefinition()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 7));
			targetIndex.ShouldBe(88);
		}

		[Test]
		public void TestNavigateToAliasedObjectDefinition()
		{
			const string query = "SELECT P.NAME, PP.PROJECT_ID FROM PROJECT P, PROJECT PP";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 7));
			targetIndex.ShouldBe(42);
		}

		[Test]
		public void TestNavigateToCommonTableExpressionDefinition()
		{
			const string query = @"WITH CTE AS (SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME FROM SELECTION)
SELECT CTE.RESPONDENTBUCKET_ID, CTE.SELECTION_ID, CTE.PROJECT_ID, CTE.NAME FROM CTE";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 155));
			targetIndex.ShouldBe(5);
		}

		[Test]
		public void TestDisplayBindVariableUsages()
		{
			const string query = "SELECT :B FROM DUAL; SELECT :B FROM DUAL";

			var context = CreateExecutionContext(query, 8);
			_navigationService.DisplayBindVariableUsages(context);
			context.SegmentsToReplace.Count.ShouldBe(2);
			context.SegmentsToReplace[0].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			context.SegmentsToReplace[0].IndextStart.ShouldBe(8);
			context.SegmentsToReplace[0].Length.ShouldBe(1);
			context.SegmentsToReplace[0].Text.ShouldBe(null);
			context.SegmentsToReplace[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			context.SegmentsToReplace[1].IndextStart.ShouldBe(29);
			context.SegmentsToReplace[1].Length.ShouldBe(1);
			context.SegmentsToReplace[1].Text.ShouldBe(null);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInSqlCase()
		{
			const string query = "SELECT CASE WHEN 1 = 1 THEN 1 END DUAL";

			var context = CreateExecutionContext(query, 8);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(7);
			correspondingSegments[0].Length.ShouldBe(4);
			correspondingSegments[1].IndexStart.ShouldBe(30);
			correspondingSegments[1].Length.ShouldBe(3);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInInsertCommandAtExplicitColumn()
		{
			const string query =
@"WITH cte (val) AS (
SELECT dummy from dual UNION ALL
SELECT dummy || 'x' from dual)
SELECT val FROM cte";

			var context = CreateExecutionContext(query, 11);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(3);
			correspondingSegments[0].IndexStart.ShouldBe(10);
			correspondingSegments[0].Length.ShouldBe(3);
			correspondingSegments[1].IndexStart.ShouldBe(28);
			correspondingSegments[1].Length.ShouldBe(5);
			correspondingSegments[2].IndexStart.ShouldBe(62);
			correspondingSegments[2].Length.ShouldBe(12);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInConcatenatedSubqueries()
		{
			const string query =
@"SELECT dummy from dual UNION ALL
SELECT dummy || 'x' from dual";

			var context = CreateExecutionContext(query, 11);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(7);
			correspondingSegments[0].Length.ShouldBe(5);
			correspondingSegments[1].IndexStart.ShouldBe(41);
			correspondingSegments[1].Length.ShouldBe(12);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInSingleQueryBlock()
		{
			const string query =
@"SELECT dummy from dual";

			var context = CreateExecutionContext(query, 11);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(0);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInInsertCommandAtSelectColumn()
		{
			const string query =
@"WITH cte (val) AS (
SELECT dummy from dual UNION ALL
SELECT dummy || 'x' from dual)
SELECT val FROM cte";

			var context = CreateExecutionContext(query, 62);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(3);
			correspondingSegments[0].IndexStart.ShouldBe(10);
			correspondingSegments[0].Length.ShouldBe(3);
			correspondingSegments[1].IndexStart.ShouldBe(28);
			correspondingSegments[1].Length.ShouldBe(5);
			correspondingSegments[2].IndexStart.ShouldBe(62);
			correspondingSegments[2].Length.ShouldBe(12);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInCommonTableExpressionCommandAtInsertColumn()
		{
			const string query =
@"INSERT INTO dual (dummy)
SELECT dummy val from dual UNION ALL
SELECT dummy || 'X' from dual";

			var context = CreateExecutionContext(query, 19);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(3);
			correspondingSegments[0].IndexStart.ShouldBe(18);
			correspondingSegments[0].Length.ShouldBe(5);
			correspondingSegments[1].IndexStart.ShouldBe(33);
			correspondingSegments[1].Length.ShouldBe(9);
			correspondingSegments[2].IndexStart.ShouldBe(71);
			correspondingSegments[2].Length.ShouldBe(12);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInCommonTableExpressionCommandAtSelectColumn()
		{
			const string query =
@"INSERT INTO dual (dummy)
SELECT dummy val from dual UNION ALL
SELECT dummy || 'X' from dual";

			var context = CreateExecutionContext(query, 71);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(3);
			correspondingSegments[0].IndexStart.ShouldBe(18);
			correspondingSegments[0].Length.ShouldBe(5);
			correspondingSegments[1].IndexStart.ShouldBe(33);
			correspondingSegments[1].Length.ShouldBe(9);
			correspondingSegments[2].IndexStart.ShouldBe(71);
			correspondingSegments[2].Length.ShouldBe(12);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInPlSqlCase()
		{
			const string query = "BEGIN CASE WHEN 1 = 1 THEN NULL; END CASE x; END;";

			var context = CreateExecutionContext(query, 8);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(6);
			correspondingSegments[0].Length.ShouldBe(4);
			correspondingSegments[1].IndexStart.ShouldBe(33);
			correspondingSegments[1].Length.ShouldBe(8);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInPlSqlIfJustBeforeSemicolon()
		{
			const string query = "BEGIN IF 1 = 1 THEN NULL; END IF; END;";

			var context = CreateExecutionContext(query, 32);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(6);
			correspondingSegments[0].Length.ShouldBe(2);
			correspondingSegments[1].IndexStart.ShouldBe(26);
			correspondingSegments[1].Length.ShouldBe(6);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInPlSqlLoop()
		{
			const string query = "BEGIN FOR i IN 1..10 LOOP NULL; END LOOP x; END;";

			var context = CreateExecutionContext(query, 21);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(21);
			correspondingSegments[0].Length.ShouldBe(4);
			correspondingSegments[1].IndexStart.ShouldBe(32);
			correspondingSegments[1].Length.ShouldBe(8);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInPlSqlSubprogram()
		{
			const string query = "BEGIN NULL; END x;";

			var context = CreateExecutionContext(query, 0);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(0);
			correspondingSegments[0].Length.ShouldBe(5);
			correspondingSegments[1].IndexStart.ShouldBe(12);
			correspondingSegments[1].Length.ShouldBe(3);
		}

		[Test]
		public void TestFindCorrespondingSegmentsInPlSqlSubprogramAtEndReservedWord()
		{
			const string query = "BEGIN NULL; END x;";

			var context = CreateExecutionContext(query, 12);
			var correspondingSegments = _navigationService.FindCorrespondingSegments(context).OrderBy(s => s.IndexStart).ToArray();
			correspondingSegments.Length.ShouldBe(2);
			correspondingSegments[0].IndexStart.ShouldBe(0);
			correspondingSegments[0].Length.ShouldBe(5);
			correspondingSegments[1].IndexStart.ShouldBe(12);
			correspondingSegments[1].Length.ShouldBe(3);
		}

		[Test]
		public void TestFindCorrespondingTerminalsWithMissingCorrespondingTerminal()
		{
			const string query = "SELECT CASE WHEN 1 = 1 THEN 1 DUAL";

			var context = CreateExecutionContext(query, 8);
			var correspondingTerminals = _navigationService.FindCorrespondingSegments(context).ToArray();
			correspondingTerminals.Length.ShouldBe(0);
		}
	}
}
