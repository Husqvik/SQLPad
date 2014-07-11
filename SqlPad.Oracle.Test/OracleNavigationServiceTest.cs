using System;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleNavigationServiceTest
	{
		private readonly OracleNavigationService _navigationService = new OracleNavigationService();
		private readonly SqlDocumentRepository _documentRepository = new SqlDocumentRepository(new OracleSqlParser(), new OracleStatementValidator(), TestFixture.DatabaseModel);

		private SqlDocumentRepository GetDocumentRepository(string statementText)
		{
			_documentRepository.UpdateStatements(statementText);
			return _documentRepository;
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootInInnerQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(GetDocumentRepository(query), 89);
			targetIndex.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootInOuterQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(GetDocumentRepository(query), 76);
			targetIndex.ShouldBe(34);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootAtFreeSpace()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM\t\r\n PROJECT) P";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(GetDocumentRepository(query), 80);
			targetIndex.ShouldBe(34);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootOutsideStatement()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P ";

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(GetDocumentRepository(query), 90);
			targetIndex.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToColumnDefinition()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(GetDocumentRepository(query), 10);
			targetIndex.ShouldBe(49);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToColumnDefinitionInAsteriskClause()
		{
			const string query = "SELECT P.PROJECT_ID, P.NAME FROM (SELECT * FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(GetDocumentRepository(query), 13);
			targetIndex.ShouldBe(41);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToColumnDefinitionWhenDatabaseModelNotLoaded()
		{
			const string query = "SELECT P.PROJECT_ID, P.NAME FROM (SELECT * FROM PROJECT) P";

			var databaseModel = new OracleTestDatabaseModel();
			databaseModel.AllObjects.Clear();
			var documentRepository = new SqlDocumentRepository(new OracleSqlParser(), new OracleStatementValidator(), databaseModel);
			documentRepository.UpdateStatements(query);

			var targetIndex = _navigationService.NavigateToDefinition(documentRepository, 13);
			targetIndex.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToObjectDefinition()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";

			var targetIndex = _navigationService.NavigateToDefinition(GetDocumentRepository(query), 7);
			targetIndex.ShouldBe(88);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToAliasedObjectDefinition()
		{
			const string query = "SELECT P.NAME, PP.PROJECT_ID FROM PROJECT P, PROJECT PP";

			var targetIndex = _navigationService.NavigateToDefinition(GetDocumentRepository(query), 7);
			targetIndex.ShouldBe(42);
		}
	}
}
