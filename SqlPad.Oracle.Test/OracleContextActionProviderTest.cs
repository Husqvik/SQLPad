using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleContextActionProviderTest
	{
		private readonly OracleContextActionProvider _actionProvider = new OracleContextActionProvider();

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionAtTheNameBeginning()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as t2.DUMMY");
			actions[1].Name.ShouldBe("Resolve as Dual.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionAtTheNameEnd()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 12).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as t2.DUMMY");
			actions[1].Name.ShouldBe("Resolve as Dual.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionWithFullyQualifiedName()
		{
			const string query1 = @"SELECT DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 12).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as SYS.DUAL.DUMMY");
			actions[1].Name.ShouldBe("Resolve as \"PUBLIC\".DUAL.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAddTableAlias()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 49).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Add Alias");
		}

		[Test(Description = @""), STAThread]
		public void TestAliasNotSuggestedAtNestedTableAlias()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 44).ToArray();
			actions.Length.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestResolveColumnIsNotsuggestedWhenTableIsNotAliased()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT 1 DUMMY FROM DUAL), SYS.DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Resolve as SYS.DUAL.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestResolveColumnIsNotsuggestedWhenTableAliasIsSameAsPhysicalTableName()
		{
			const string query1 = @"SELECT DUAL.DUMMY FROM (SELECT 1 DUMMY FROM DUAL) DUAL, SYS.DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 14).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Resolve as SYS.DUAL.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestWrapCommandsNotAvailableWhenQueryBlockHasNoNamedColumn()
		{
			const string query1 = @"SELECT NULL FROM DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 1).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Toggle quoted identifiers");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandSuggestion()
		{
			const string query1 = @"SELECT IV.TEST_COLUMN || ' ADDED' FROM (SELECT SELECTION.NAME || ' FROM CTE ' TEST_COLUMN FROM SELECTION) IV";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 41).ToArray();
			actions.Length.ShouldBe(4);
			actions[3].Name.ShouldBe("Unnest");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandIsNotSuggestedWhenInlineViewContainsGroupByClause()
		{
			const string query1 = @"SELECT * FROM (SELECT NAME FROM SELECTION GROUP BY NAME)";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 18).ToArray();
			actions.Length.ShouldBe(3);
		}
		
		[Test(Description = @""), STAThread]
		public void TestUnnestCommandIsNotSuggestedWhenInlineViewContainsDistinctClause()
		{
			const string query1 = @"SELECT * FROM (SELECT DISTINCT NAME FROM SELECTION)";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 18).ToArray();
			actions.Length.ShouldBe(3);
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandNotAvailableAtObjectAliasWhichIsNotCommonTableExpressionAlias()
		{
			const string query1 = @"SELECT 1 FROM DUAL ALIAS";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 20).ToArray();
			actions.Length.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandNotAvailableWithSourceWithoutNamedColumn()
		{
			const string query1 = "SELECT * FROM (SELECT 1 FROM SELECTION)";
			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).ToArray();
			actions.Length.ShouldBe(0);

			const string query2 = "SELECT S.* FROM (SELECT 1 FROM SELECTION) S";
			actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query2, 9).ToArray();
			actions.Length.ShouldBe(0);
		}
	}
}