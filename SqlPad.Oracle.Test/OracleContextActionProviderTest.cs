using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.Commands;

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

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as t2.DUMMY");
			actions[1].Name.ShouldBe("Resolve as Dual.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionAtTheNameEnd()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 12).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as t2.DUMMY");
			actions[1].Name.ShouldBe("Resolve as Dual.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionInWhereClause()
		{
			const string query1 = @"SELECT * FROM SELECTION, PROJECT WHERE NAME = 'Name'";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 39).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as SELECTION.NAME");
			actions[1].Name.ShouldBe("Resolve as PROJECT.NAME");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionInOrderClause()
		{
			const string query1 = @"SELECT * FROM DUAL D1 JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY ORDER BY DUMMY";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 72).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as D1.DUMMY");
			actions[1].Name.ShouldBe("Resolve as D2.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAmbiguousColumnReferenceResolutionWithFullyQualifiedName()
		{
			const string query1 = @"SELECT DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 12).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as SYS.DUAL.DUMMY");
			actions[1].Name.ShouldBe("Resolve as \"PUBLIC\".DUAL.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestSuggestingAddTableAlias()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 49).SingleOrDefault(a => a.Name == AddAliasCommand.Title);
			action.ShouldNotBe(null);
			action.Name.ShouldBe("Add Alias");
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

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Resolve as SYS.DUAL.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestResolveColumnIsNotsuggestedWhenTableAliasIsSameAsPhysicalTableName()
		{
			const string query1 = @"SELECT DUAL.DUMMY FROM (SELECT 1 DUMMY FROM DUAL) DUAL, SYS.DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 14).Where(a => a.Name.StartsWith("Resolve as")).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Resolve as SYS.DUAL.DUMMY");
		}

		[Test(Description = @""), STAThread]
		public void TestWrapCommandsAvailableWhenQueryBlockHasNoNamedColumn()
		{
			const string query1 = @"SELECT NULL FROM DUAL";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 1)
				.Count(a => a.Name.In(WrapAsCommonTableExpressionCommand.Title, WrapAsInlineViewCommand.Title));
			
			actions.ShouldBe(2);
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandSuggestion()
		{
			const string query1 = @"SELECT IV.TEST_COLUMN || ' ADDED' FROM (SELECT SELECTION.NAME || ' FROM CTE ' TEST_COLUMN FROM SELECTION) IV";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 41).SingleOrDefault(a => a.Name == UnnestInlineViewCommand.Title);
			action.ShouldNotBe(null);
			action.Name.ShouldBe("Unnest");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandIsNotSuggestedWhenInlineViewContainsGroupByClause()
		{
			const string query1 = @"SELECT * FROM (SELECT NAME FROM SELECTION GROUP BY NAME)";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 18).Count(a => a.Name == UnnestInlineViewCommand.Title);
			actions.ShouldBe(0);
		}
		
		[Test(Description = @""), STAThread]
		public void TestUnnestCommandIsNotSuggestedWhenInlineViewContainsDistinctClause()
		{
			const string query1 = @"SELECT * FROM (SELECT DISTINCT NAME FROM SELECTION)";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 18).Count(a => a.Name == UnnestInlineViewCommand.Title);
			actions.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandNotAvailableAtObjectAliasWhichIsNotCommonTableExpressionAlias()
		{
			const string query1 = @"SELECT 1 FROM DUAL ALIAS";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 20).Count(a => a.Name == UnnestInlineViewCommand.Title);
			actions.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandAvailableWithSourceWithoutNamedColumn()
		{
			const string query1 = "SELECT * FROM (SELECT 1 FROM SELECTION)";
			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).ToArray();
			actions.Length.ShouldBe(1);

			const string query2 = "SELECT S.* FROM (SELECT 1 FROM SELECTION) S";
			actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query2, 9).ToArray();
			actions.Length.ShouldBe(1);
		}

		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandNotAvailableWithSourceNamedColumnContainingQuotes()
		{
			const string query1 = @"SELECT * FROM (SELECT ""1"" || ""2"" FROM(SELECT 1, 2 FROM DUAL))";
			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).ToArray();
			actions.Length.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandAvailableBeforeComma()
		{
			const string query1 = "SELECT SELECTION.*, 1 FROM SELECTION";
			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 18).SingleOrDefault(a => a.Name == ExpandAsteriskCommand.Title);
			action.ShouldNotBe(null);
			action.Name.ShouldBe("Expand");
		}
		
		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandAvailableBeforeFromKeyword()
		{
			const string query1 = "SELECT*FROM DUAL";
			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).SingleOrDefault(a => a.Name == ExpandAsteriskCommand.Title);
			action.ShouldNotBe(null);
			action.Name.ShouldBe("Expand");
		}

		[Test(Description = @""), STAThread]
		public void TestToggleFullyQualifiedReferencesSuggested()
		{
			const string query1 = @"SELECT DUMMY FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == ToggleFullyQualifiedReferencesCommand.Title);
			action.ShouldNotBe(null);
			action.Name.ShouldBe("Toggle fully qualified references");
		}

		[Test(Description = @""), STAThread]
		public void TestAddMissingColumnCommandSuggestion()
		{
			const string query1 = @"SELECT NOT_EXISTING_COLUMN FROM SELECTION";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).SingleOrDefault(a => a.Name == AddMissingColumnCommand.Title);
			action.ShouldNotBe(null);
			action.Name.ShouldBe("Add missing column");
		}

		[Test(Description = @""), STAThread]
		public void TestAddMissingColumnCommandNotSuggestedWhenAlreadyExists()
		{
			const string query1 = @"SELECT DUMMY FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).SingleOrDefault(a => a.Name == AddMissingColumnCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestToggleQuotedNotationNotSuggestedWhenNotConvertibleIdentifierOrAliasAvailable()
		{
			const string query1 = @"SELECT ""Balance"" FROM ""Accounts""";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == ToggleQuotedNotationCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestAddColumnAliasSuggestion()
		{
			const string query1 = @"SELECT DUMMY FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 10).SingleOrDefault(a => a.Name == AddAliasCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestAddColumnAliasNotSuggestedWhenAliasExists()
		{
			const string query1 = @"SELECT DUMMY NOT_DUMMY FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 10).SingleOrDefault(a => a.Name == AddAliasCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestAddColumnAliasNotSuggestedWhenNotDirectReference()
		{
			const string query1 = @"SELECT DUMMY + 1 FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 10).SingleOrDefault(a => a.Name == AddAliasCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestCreateScriptSuggestion()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).SingleOrDefault(a => a.Name == CreateScriptCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestObjectAliasSuggestedAtInsertMainObjectReference()
		{
			const string query1 = @"INSERT INTO SELECTION SELECT * FROM SELECTION";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 17).Where(a => a.Name == AddAliasCommand.Title).ToArray();
			actions.Length.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestAddInsertIntoColumns()
		{
			const string query1 = @"INSERT INTO SELECTION SELECT * FROM SELECTION";

			var actions = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 9).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Add Column List");
		}

		[Test(Description = @""), STAThread]
		public void TestCleanRedundantQualifierCommand()
		{
			const string query1 = @"SELECT SELECTION.NAME FROM HUSQVIK.SELECTION";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == CleanRedundantSymbolCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestCleanRedundantQualifierNotSuggestedWhenNoRedundantQualifiersAvailable()
		{
			const string query1 = @"SELECT SELECTION.NAME, RESPONDENTBUCKET.NAME FROM SELECTION, RESPONDENTBUCKET";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == CleanRedundantSymbolCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestGenerateCreateTableScriptAvailable()
		{
			const string query1 = @"SELECT SELECTION.NAME, RESPONDENTBUCKET.NAME FROM SELECTION, RESPONDENTBUCKET";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == AddCreateTableAsCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestGenerateCreateTableScriptNoAvailableWhenObjectReferencesNotResolved()
		{
			const string query1 = @"SELECT * FROM";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == AddCreateTableAsCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestUnquoteCommandAvailable()
		{
			const string query1 = @"SELECT ""CaseSensitiveColumn"" FROM INVOICELINES";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 0).SingleOrDefault(a => a.Name == UnquoteCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertBindVariableToLiteralAvailable()
		{
			const string query1 = @"SELECT :1 FROM DUAL";

			var actionCount = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 8).Count(a => a.Name.StartsWith("Convert"));
			actionCount.ShouldBe(1);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertAllBindVariableOccurencesToLiteralAvailable()
		{
			const string query1 = @"SELECT :1, :1 FROM DUAL";

			var actionCount = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 13).Count(a => a.Name.StartsWith("Convert"));
			actionCount.ShouldBe(2);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertLiteralToBindVariableAvailable()
		{
			const string query1 = @"SELECT 1 FROM DUAL";

			var actionCount = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).Count(a => a.Name.StartsWith("Convert"));
			actionCount.ShouldBe(1);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertAllLiteralOccurencesToBindVariablesAvailable()
		{
			const string query1 = @"SELECT 'VALUE', 'VALUE' FROM DUAL";

			var actionCount = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 10).Count(a => a.Name.StartsWith("Convert"));
			actionCount.ShouldBe(2);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertSingleLiteralToBindVariablesAvailableWhenSameLiteralRepresentsMultipleTypes()
		{
			const string query1 = @"SELECT DATE'2014-10-04', DATE'2014-10-04', '2014-10-04' FROM DUAL";

			var actionCount = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 50).Count(a => a.Name.StartsWith("Convert"));
			actionCount.ShouldBe(1);
		}

		[Test(Description = @""), STAThread]
		public void TestPropagateColumnAvailable()
		{
			const string query1 = @"SELECT 1 C1 FROM (SELECT 2 C2 FROM DUAL)";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 28).SingleOrDefault(a => a.Name == PropagateColumnCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestPropagateColumnNotAvailableAtReferencedColumn()
		{
			const string query1 = @"SELECT C2 C1 FROM (SELECT 2 C2 FROM DUAL)";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 28).SingleOrDefault(a => a.Name == PropagateColumnCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestAddToGroupByAvailableAtTheEndOfIdentifier()
		{
			const string query1 = @"SELECT DUMMY FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 12).SingleOrDefault(a => a.Name == AddToGroupByCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertOrderByNumberColumnReferences()
		{
			const string query1 = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 1, 2";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 77).SingleOrDefault(a => a.Name == ConvertOrderByNumberColumnReferencesCommand.Title);
			action.ShouldNotBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestConvertOrderByNumberColumnReferencesAtIncompatibleTerminal()
		{
			const string query1 = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 1, 2.0";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 79).SingleOrDefault(a => a.Name == ConvertOrderByNumberColumnReferencesCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestGenerateCustomTypeCSharpWrapperClassCommandAtXmlTypes()
		{
			const string query1 = @"SELECT XMLTYPE() FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).SingleOrDefault(a => a.Name == GenerateCustomTypeCSharpWrapperClassCommand.Title);
			action.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
		public void TestGenerateCustomTypeCSharpWrapperClassCommandAtColumnIdentifier()
		{
			const string query1 = @"SELECT DUMMY FROM DUAL";

			var action = _actionProvider.GetContextActions(TestFixture.DatabaseModel, query1, 7).SingleOrDefault(a => a.Name == GenerateCustomTypeCSharpWrapperClassCommand.Title);
			action.ShouldBe(null);
		}
	}
}
