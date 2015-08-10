using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

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

		[Test(Description = @"")]
		public void TestNavigateToQueryBlockRootInInnerQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 89));
			targetIndex.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestNavigateToQueryBlockRootInOuterQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 76));
			targetIndex.ShouldBe(34);
		}

		[Test(Description = @"")]
		public void TestNavigateToQueryBlockRootAtFreeSpace()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM\t\r\n PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 80));
			targetIndex.ShouldBe(34);
		}

		[Test(Description = @"")]
		public void TestNavigateToQueryBlockRootOutsideStatement()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P ";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(CreateExecutionContext(query, 90));
			targetIndex.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestNavigateToColumnDefinition()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 10));
			targetIndex.ShouldBe(49);
		}

		[Test(Description = @"")]
		public void TestNavigateToColumnDefinitionInAsteriskClause()
		{
			const string query = "SELECT P.PROJECT_ID, P.NAME FROM (SELECT * FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 13));
			targetIndex.ShouldBe(41);
		}

		[Test(Description = @"")]
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

		[Test(Description = @"")]
		public void TestNavigateToColumnDefinitionWithCommonTableExpressionExplicitColumnNames()
		{
			const string query = "WITH GENERATOR(VAL) AS (SELECT 1 FROM DUAL UNION ALL SELECT VAL + 1 FROM GENERATOR WHERE VAL <= 10) SELECT VAL FROM GENERATOR";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 90));
			targetIndex.ShouldBe(60);
		}

		[Test(Description = @"")]
		public void TestNavigateToObjectDefinition()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 7));
			targetIndex.ShouldBe(88);
		}

		[Test(Description = @"")]
		public void TestNavigateToAliasedObjectDefinition()
		{
			const string query = "SELECT P.NAME, PP.PROJECT_ID FROM PROJECT P, PROJECT PP";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 7));
			targetIndex.ShouldBe(42);
		}

		[Test(Description = @"")]
		public void TestNavigateToCommonTableExpressionDefinition()
		{
			const string query = @"WITH CTE AS (SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME FROM SELECTION)
SELECT CTE.RESPONDENTBUCKET_ID, CTE.SELECTION_ID, CTE.PROJECT_ID, CTE.NAME FROM CTE";

			var targetIndex = _navigationService.NavigateToDefinition(CreateExecutionContext(query, 155));
			targetIndex.ShouldBe(5);
		}

		[Test(Description = @"")]
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

		[Test(Description = @"")]
		public void TestFindCorrespondingTerminals()
		{
			const string query = "SELECT CASE WHEN 1 = 1 THEN 1 END DUAL";

			var context = CreateExecutionContext(query, 8);
			var correspondingTerminals = _navigationService.FindCorrespondingTerminals(context).ToArray();
			correspondingTerminals.Length.ShouldBe(2);
			correspondingTerminals[0].Id.ShouldBe(Terminals.Case);
			correspondingTerminals[1].Id.ShouldBe(Terminals.End);
		}

		[Test(Description = @"")]
		public void TestFindCorrespondingTerminalsWithMissingCorrespondingTerminal()
		{
			const string query = "SELECT CASE WHEN 1 = 1 THEN 1 DUAL";

			var context = CreateExecutionContext(query, 8);
			var correspondingTerminals = _navigationService.FindCorrespondingTerminals(context).ToArray();
			correspondingTerminals.Length.ShouldBe(0);
		}
	}
}
