using System;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleNavigationServiceTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();
		private readonly OracleNavigationService _navigationService = new OracleNavigationService();

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootInInnerQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";
			var statements = _oracleSqlParser.Parse(query);

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(statements, 89);
			targetIndex.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootInOuterQuery()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P";
			var statements = _oracleSqlParser.Parse(query);

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(statements, 76);
			targetIndex.ShouldBe(34);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootAtFreeSpace()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM\t\r\n PROJECT) P";
			var statements = _oracleSqlParser.Parse(query);

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(statements, 80);
			targetIndex.ShouldBe(34);
		}

		[Test(Description = @""), STAThread]
		public void TestNavigateToQueryBlockRootOutsideStatement()
		{
			const string query = "SELECT P.NAME, P.PROJECT_ID FROM (SELECT PROJECT.NAME, PROJECT.PROJECT_ID FROM PROJECT) P ";
			var statements = _oracleSqlParser.Parse(query);

			var targetIndex = _navigationService.NavigateToQueryBlockRoot(statements, 90);
			targetIndex.ShouldBe(null);
		}
	}
}
