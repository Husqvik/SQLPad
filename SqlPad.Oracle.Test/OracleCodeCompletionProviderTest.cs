using System.Linq;
using NUnit.Framework;
using Shouldly;
using System;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleCodeCompletionProviderTest
	{
		private readonly OracleCodeCompletionProvider _codeCompletionProvider = new OracleCodeCompletionProvider();
		private readonly SqlDocumentRepository _documentRepository = TestFixture.CreateDocumentRepository();
		private static readonly Func<ICodeCompletionItem, bool> FilterProgramItems = i => !i.Category.In(OracleCodeCompletionCategory.PackageFunction, OracleCodeCompletionCategory.Package, OracleCodeCompletionCategory.SchemaFunction);

		[Test(Description = @"")]
		public void TestObjectSuggestionWithSchema()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 37).ToArray();
			items.Length.ShouldBe(10);
			items[0].Name.ShouldBe("\"CaseSensitiveTable\"");
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[9].Name.ShouldBe("VIEW_INSTANTSEARCH");
			items[9].Text.ShouldBe("VIEW_INSTANTSEARCH");
		}

		[Test(Description = @"")]
		public void TestJoinTypeSuggestion()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I ", 52).ToArray();
			// TODO: Filter out outer types depending of nullable columns
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[0].InsertOffset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
			items[4].Name.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
			items[4].InsertOffset.ShouldBe(0);
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionAtTheBeginningOfColumnNameWhenAlreadyEntered()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 21).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("DUEDATE");
			items[0].Text.ShouldBe("DUEDATE");
			items[1].Name.ShouldBe(OracleColumn.RowId);
		}

		[Test(Description = @"")]
		public void TestObjectSuggestionInUnfinishedStatements()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 35).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("INVOICELINES");
			items[0].Text.ShouldBe("INVOICELINES");
			//items[0].InsertOffset.ShouldBe(0);
			items[1].Name.ShouldBe("INVOICES");
			items[1].Text.ShouldBe("INVOICES");

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 57).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("INVOICES");
			items[0].Text.ShouldBe("INVOICES");
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestions()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P", 50).ToArray();
			items.Length.ShouldBe(0);

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ", 51).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].Text.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionsAfterOnKeyword()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON", 53).ToArray();
			items.Length.ShouldBe(0);

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON ", 54).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].Text.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionsWhenJoiningNestedSubquery()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM (SELECT 1 VAL FROM DUAL) T1 JOIN (SELECT 1 VAL FROM DUAL) T2 ", 75).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON T1.VAL = T2.VAL");
			items[0].Text.ShouldBe("ON T1.VAL = T2.VAL");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionForTablesWithForeignKeys()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID JOIN RESPONDENTBUCKET B ", 106).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Text.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].InsertOffset.ShouldBe(0);
			items[1].Name.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Text.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].InsertOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionAfterTableFunctionClause()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM SELECTION JOIN TABLE(PIPELINED_FUNCTION) T ON ", 60).ToArray();
			items.Length.ShouldBe(0);
			// TODO: Add proper implementation
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionForTablesWithoutForeignKeys()
		{
			const string query1 = @"WITH
	CTE1 AS (SELECT '' NAME, '' DESCRIPTION, 1 ID FROM DUAL),
	CTE2 AS (SELECT '' OTHER_NAME, '' OTHER_DESCRIPTION, 1 ID FROM DUAL)
SELECT
	*
FROM
	CTE1
	JOIN CTE2 ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 173).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON CTE1.ID = CTE2.ID");
			items[0].Text.ShouldBe("ON CTE1.ID = CTE2.ID");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestObjectSuggestionInJoinClauseWithPartialName()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN P";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 34).ToArray();
			items.Length.ShouldBe(3);
			items[0].Name.ShouldBe("PROJECT");
			items[0].Text.ShouldBe("PROJECT");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Name.ShouldBe("RESPONDENTBUCKET");
			items[1].Text.ShouldBe("RESPONDENTBUCKET");
			items[2].Name.ShouldBe("TARGETGROUP");
			items[2].Text.ShouldBe("TARGETGROUP");
		}

		[Test(Description = @"")]
		public void TestCommonTableExpressionSuggestion()
		{
			const string query1 = @"WITH
	CTE1 AS (SELECT '' NAME, '' DESCRIPTION, 1 ID FROM DUAL),
	CTE2 AS (SELECT '' OTHER_NAME, '' OTHER_DESCRIPTION, 1 ID FROM DUAL)
SELECT
	*
FROM
	CTE1
	JOIN ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 168).Where(i => i.Category == OracleCodeCompletionCategory.CommonTableExpression).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("CTE1");
			items[0].Text.ShouldBe("CTE1");
			items[1].Name.ShouldBe("CTE2");
			items[1].Text.ShouldBe("CTE2");
		}

		[Test(Description = @"")]
		public void TestObjectSuggestionAfterEnteredSchema()
		{
			const string query1 = @"SELECT * FROM SYS.";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 18).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("DUAL");
			items[0].Text.ShouldBe("DUAL");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Name.ShouldBe("V_$SESSION");
			items[1].Text.ShouldBe("V_$SESSION");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test(Description = @"")]
		public void TestSuggestionWhenEnteringNewColumn()
		{
			const string query1 = @"SELECT 1,  FROM SELECTION S";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 10).Where(FilterProgramItems).ToArray();
			items.Length.ShouldBe(14);
			items[0].Name.ShouldBe("S.*");
			items[0].Text.ShouldBe("S.RESPONDENTBUCKET_ID, S.SELECTION_ID, S.PROJECT_ID, S.NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.AllColumns);
			items[1].Name.ShouldBe("S.NAME");
			items[1].Text.ShouldBe("S.NAME");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[2].Name.ShouldBe("S.PROJECT_ID");
			items[2].Text.ShouldBe("S.PROJECT_ID");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[3].Name.ShouldBe("S.RESPONDENTBUCKET_ID");
			items[3].Text.ShouldBe("S.RESPONDENTBUCKET_ID");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[4].Name.ShouldBe("S.ROWID");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.PseudoColumn);
			items[5].Name.ShouldBe("S.SELECTION_ID");
			items[5].Text.ShouldBe("S.SELECTION_ID");
			items[5].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			// Objects
			items[6].Name.ShouldBe("S");
			items[6].Text.ShouldBe("S");
			items[6].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			// Other schema objects
			items[7].Name.ShouldBe("INVALID_OBJECT_TYPE");
			items[7].Text.ShouldBe("INVALID_OBJECT_TYPE()");
			items[7].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[9].Name.ShouldBe("TEST_SEQ");
			items[9].Text.ShouldBe("TEST_SEQ");
			items[9].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[10].Name.ShouldBe("XMLTYPE");
			items[10].Text.ShouldBe("XMLTYPE()");
			items[10].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			// Schemas
			items[11].Name.ShouldBe("HUSQVIK");
			items[11].Text.ShouldBe("HUSQVIK");
			items[11].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
			items[12].Name.ShouldBe("SYS");
			items[12].Text.ShouldBe("SYS");
			items[12].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
			items[13].Name.ShouldBe("SYSTEM");
			items[13].Text.ShouldBe("SYSTEM");
			items[13].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
		}

		[Test(Description = @"")]
		public void TestSchemaSuggestionInSelectListWhenPartiallyEntered()
		{
			const string query1 = @"SELECT HU FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 9).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("HUSQVIK");
			items[0].Text.ShouldBe("HUSQVIK");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
		}

		[Test(Description = @"")]
		public void TestTableSuggestionWithPartialName()
		{
			const string query1 = @"SELECT 1 FROM SYSTEM.C";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 22).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionForMultipleTables()
		{
			const string query1 = @"SELECT SELECTION. FROM SELECTION, TARGETGROUP";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 17).ToArray();
			items.Length.ShouldBe(6);
			items[0].Name.ShouldBe("*");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, SELECTION.PROJECT_ID, SELECTION.NAME");
			items[1].Name.ShouldBe("NAME");
			items[1].Text.ShouldBe("NAME");
			items[4].Name.ShouldBe(OracleColumn.RowId);
			items[5].Name.ShouldBe("SELECTION_ID");
			items[5].Text.ShouldBe("SELECTION_ID");

			const string query2 = @"SELECT SELECTION.NAME FROM SELECTION, TARGETGROUP";

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query2, 18).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("RESPONDENTBUCKET_ID");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID");
			items[1].Name.ShouldBe("SELECTION_ID");
			items[1].Text.ShouldBe("SELECTION_ID");

			const string query3 = @"SELECT SELECTION.NAME FROM SELECTION, TARGETGROUP";

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query3, 19).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinTypeSuggestionInChainedJoinClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION S LEFT JOIN RESPONDENTBUCKET ON S.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 120).ToArray();
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[4].Name.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
		}

		[Test(Description = @"")]
		public void TestSchemaSuggestionInJoinConditionWhenAlreadyEnteredAndOnlyOneOptionRemains()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestTableSuggestionInJoinConditionWhenAlreadyEnteredAndOnlyOneOptionRemains()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 43).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionWithAlias()
		{
			const string query1 = @"SELECT D FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 8, true, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("DUAL.DUMMY");
			items[0].Text.ShouldBe("DUAL.DUMMY");

			const string query2 = @"SELECT D FROM DUAL X";

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query2, 8, true, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("X.DUMMY");
			items[0].Text.ShouldBe("X.DUMMY");
		}

		[Test(Description = @"")]
		public void TestTableSuggestionWhenWithinScalarSubquery()
		{
			const string query1 = @"SELECT NULL FROM DUAL WHERE DUMMY = (SELECT * FROM DUAL)";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 53).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestNoColumnSuggestedWhenOnlyOneOptionExistsAndAlreadyInPlace()
		{
			const string query1 = @"SELECT SELECTION.NAME FROM SELECTION";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 8).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionAfterDotInGroupByClause()
		{
			const string query1 = @"SELECT * FROM PROJECT P GROUP BY P.";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToArray();
			items.Length.ShouldBe(3);
			items[0].Name.ShouldBe("NAME");
			items[0].Text.ShouldBe("NAME");
			items[1].Name.ShouldBe("PROJECT_ID");
			items[1].Text.ShouldBe("PROJECT_ID");
			items[2].Name.ShouldBe(OracleColumn.RowId);
			items[2].Text.ShouldBe(OracleColumn.RowId);
		}

		[Test(Description = @"")]
		public void TestObjectsAreSuggestedAfterFromTerminal()
		{
			const string query1 = @"SELECT * FROM ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 14).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test(Description = @"")]
		public void TestNothingIsSuggestedWhenOnFromTerminal()
		{
			const string query1 = @"SELECT * FROM ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 9).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestObjectSuggestionInChainedFromClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION, ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 28).ToArray();
			var currentSchemaTableCount = OracleTestDatabaseModel.Instance.AllObjects.Values.Count(FilterRowSources);
			var schemaCount = OracleTestDatabaseModel.Instance.Schemas.Count; // PUBLIC excluded
			items.Length.ShouldBe(currentSchemaTableCount + schemaCount);
		}

		private static bool FilterRowSources(OracleSchemaObject schemaObject)
		{
			var targetObject = schemaObject.GetTargetSchemaObject();
			return targetObject != null && schemaObject.Owner.In(OracleTestDatabaseModel.Instance.CurrentSchema, OracleDatabaseModelBase.SchemaPublic) && targetObject.Type.In(OracleSchemaObjectType.Table, OracleSchemaObjectType.View);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionWhenTableWithSchemaInFromClause()
		{
			const string query1 = @"SELECT  1 FROM SYS.DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, true, OracleCodeCompletionCategory.SchemaObject, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(6);
			items[0].Name.ShouldBe("SYS.DUAL.DUMMY");
			items[1].Name.ShouldBe("SYS.DUAL");
			items[5].Name.ShouldBe("XMLTYPE");
		}

		[Test(Description = @"")]
		public void TestAsteriskIsNotSuggestedWithinNestedExpression()
		{
			const string query1 = @"SELECT CASE WHEN S. FROM SELECTION S";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 19).ToArray();
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("NAME");
			items[3].Name.ShouldBe(OracleColumn.RowId);
			items[4].Name.ShouldBe("SELECTION_ID");
		}

		[Test(Description = @"")]
		public void TestJoinConditionNotSuggestedWhenCommonTableExpressionColumnsAreNotAliased()
		{
			const string query1 = @"WITH X AS (SELECT 1 FROM DUAL), Y AS (SELECT 1 FROM DUAL) SELECT * FROM X JOIN Y ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 81).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestObjectAndSchemaSuggestionWhenTypingSubquery()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM )";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test(Description = @"")]
		public void TestSchemaObjectSuggestionWhenTypingSubqueryAfterDotAfterSchema()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM HUSQVIK.)";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 43).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionWhenQueryContainsNonAliasedSubquery()
		{
			const string query1 = @"SELECT  FROM (SELECT HUSQVIK.SELECTION.NAME FROM HUSQVIK.SELECTION), HUSQVIK.SELECTION";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7).Where(FilterProgramItems).Where(i => !i.Name.Contains("HUSQVIK.SELECTION") && !i.Category.In(OracleCodeCompletionCategory.DatabaseSchema, OracleCodeCompletionCategory.SchemaObject)).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionAfterDotInTheMiddleOfSelectList()
		{
			const string query1 = @"SELECT S.NAME, S., 'My column2' FROM SELECTION S";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 17).ToArray();
			items.Length.ShouldBe(6);
			items[0].Name.ShouldBe("*");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID, S.SELECTION_ID, S.PROJECT_ID, S.NAME");
			items[4].Name.ShouldBe(OracleColumn.RowId);
			items[5].Name.ShouldBe("SELECTION_ID");
		}

		[Test(Description = @"")]
		public void TestJoinNotSuggestedAfterUnrecognizedToken()
		{
			const string query1 = @"SELECT NULL FROM SELECTION + ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 29).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSuggestionWhenJustBeforeClosingParanthesis()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM DUAL,)";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 40).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test(Description = @"")]
		public void TestSuggestionWhenJustAtCommaWhenPrecedingTokenAlreadyEntered()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM DUAL,)";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 39).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSuggestionWhenAtSelectKeyword()
		{
			const string query1 = @"SELECT NAME FROM SELECTION";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 0).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionUsingNestedQueryAndCountAsteriskFunction()
		{
			const string query1 = @"SELECT  DUMMY FROM (SELECT DUMMY, COUNT(*) OVER () ROW_COUNT FROM (SELECT DUMMY FROM DUAL))";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, true, OracleCodeCompletionCategory.Column, OracleCodeCompletionCategory.AllColumns).ToArray();
			items.Length.ShouldBe(3);
			items[0].Name.ShouldBe("*");
			items[0].Text.ShouldBe("DUMMY, ROW_COUNT");
			items[1].Name.ShouldBe("DUMMY");
			items[2].Name.ShouldBe("ROW_COUNT");
		}

		[Test(Description = @"")]
		public void TestPackageAndFunctionSuggestion()
		{
			const string query1 = @"SELECT HUSQVIK. FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15).ToArray();
			items.Length.ShouldBe(7);
			items[0].Name.ShouldBe("AS_PDF3");
			items[0].Text.ShouldBe("AS_PDF3.");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Package);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
			items[4].Name.ShouldBe("TESTFUNC");
			items[4].Text.ShouldBe("TESTFUNC()");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[4].CaretOffset.ShouldBe(-1);
			items[4].StatementNode.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestPackageOrFunctionNotSuggestedWhenAtSchemaToken()
		{
			const string query1 = @"SELECT HUSQVIK. FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 14).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestPackageFunctionSuggestionWhenAlreadyEntered()
		{
			const string query1 = @"SELECT HUSQVIK.SQLPAD FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 16).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("SQLPAD_FUNCTION");
			items[0].Text.ShouldBe("SQLPAD_FUNCTION()");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[0].CaretOffset.ShouldBe(-1);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestPackageFunctionSuggestionDoesNotContainParameterListWhenAlreadyEnteredWithParameterList()
		{
			const string query1 = @"SELECT HUSQVIK.SFUNCTION('PARAMETER') FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 16).ToArray();
			items.Length.ShouldBe(2);
			items[1].Name.ShouldBe("SQLPAD_FUNCTION");
			items[1].Text.ShouldBe("SQLPAD_FUNCTION");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[1].CaretOffset.ShouldBe(0);
			items[1].StatementNode.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestNodeReplacementWhenAtTokenStartAndTokenAlreadyExists()
		{
			const string query1 = @"SELECT HUSQVIK.SFUNCTION('PARAMETER') FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15).ToArray();
			items.Length.ShouldBeGreaterThan(0);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestPackageFunctionNotSuggestedWhenSameFunctionAlreadyEntered()
		{
			const string query1 = @"SELECT HUSQVIK.SQLPAD.SQLPAD_FUNCTION('') FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 23).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestPackageFunctionSuggestion()
		{
			const string query1 = @"SELECT HUSQVIK.SQLPAD. FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 22).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("SQLPAD_FUNCTION");
			items[0].Text.ShouldBe("SQLPAD_FUNCTION()");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
			items[0].CaretOffset.ShouldBe(-1);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestJoinConditionNotSuggestedForNonAliasedSubquery()
		{
			const string query1 = @"SELECT * FROM (SELECT PROJECT_ID FROM PROJECT) JOIN PROJECT ";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 60).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestResolveFunctionOverloadsFilteredByCurrentArgumentIndex()
		{
			const string query1 = @"SELECT ROUND(1.23, 1) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 19).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SYS.STANDARD.ROUND");
			items[0].Parameters.Count.ShouldBe(2);
			items[0].CurrentParameterIndex.ShouldBe(1);
			items[0].ReturnedDatatype.ShouldNotBe(null);
			items[0].HasSchemaDefinition.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestResolveFunctionOverloadsOutsideTerminal()
		{
			const string query1 = @"SELECT ROUND(1.23, 1) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 18).ToList();
			items.Count.ShouldBe(1);
			items[0].CurrentParameterIndex.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestResolveFunctionOverloadsWithNonSchemaFunction()
		{
			const string query1 = @"SELECT MAX(DUMMY) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 11).ToList();
			items.Count.ShouldBe(1);
			items[0].HasSchemaDefinition.ShouldBe(false);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionWhenIdentifierWithQuotedNotation()
		{
			const string query1 = @"SELECT IL."""" FROM INVOICELINES IL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 11).ToList();
			items.Count.ShouldBe(6);
			items[0].Text.ShouldBe("\"CaseSensitiveColumn\"");
		}

		[Test(Description = @"")]
		public void TestTableSuggestionInTheMiddleOfQuotedNotation()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable""";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 19).ToList();
			items.Count.ShouldBe(1);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[0].CaretOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSuggestionWhenTypingDatabaseLinkIdentifier()
		{
			const string query1 = @"SELECT * FROM CUSTOMER@H";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 24).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("HQ_PDB_LOOPBACK");
			items[0].CaretOffset.ShouldBe(0);
			items[0].Text.ShouldBe("HQ_PDB_LOOPBACK");
		}

		[Test(Description = @"")]
		public void TestSuggestionAtSecondStatementBeginningWithFirstStatementEndingWithQuotedIdentifier()
		{
			const string query1 =
@"SELECT * FROM ""PaymentPlans""
;

se";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 37).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestTableSuggestionAfterQuoteCharacter()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable""";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15).ToList();
			items.Count.ShouldBe(16);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
		}

		[Test(Description = @"")]
		public void TestCodeCompletionWhenTypingOrderAfterTableNameInFromClause()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable"" OR";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestCodeCompletionWhenTypingWithinParentheses()
		{
			const string statement = @"SELECT SQLPAD.SQLPAD_FUNCTION(D) FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(6);
			items[0].Text.ShouldBe("DUAL.DUMMY");
			items[5].Name.ShouldBe("DUMP");
			items[5].Text.ShouldBe("DUMP()");
		}

		[Test(Description = @"")]
		public void TestCodeCompletionWhenUsingNameParts()
		{
			const string statement = @"SELECT * FROM SenTab";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 20).ToList();
			items.Count.ShouldBe(1);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
		}

		[Test(Description = @"")]
		public void TestNoParenthesesFunctionCodeCompletionWhenUsingNameParts()
		{
			const string statement = @"SELECT SeTiZo FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 13, false).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SESSIONTIMEZONE");
			items[0].Text.ShouldBe("SESSIONTIMEZONE");
		}

		[Test(Description = @"")]
		public void TestCodeCompletionInComment()
		{
			const string statement = @"SELECT /*+ PARALLEL */ DUMMY FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 15).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestTruncSpecialParameterCompletion()
		{
			const string statement = @"SELECT TRUNC(SYSDATE, 'IW') FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 23).ToList();
			items.Count.ShouldBe(16);
			items[0].Name.ShouldBe("CC - One greater than the first two digits of a four-digit year");
			items[0].Text.ShouldBe("'CC'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[15].Name.ShouldBe("YYYY - Year (rounds up on July 1)");
			items[15].Text.ShouldBe("'YYYY'");
			items[15].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test(Description = @"")]
		public void TestToCharSpecialParameterCompletion()
		{
			const string statement = @"SELECT TO_CHAR(12.34, '9G999D00', '') FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 35).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("NLS_NUMERIC_CHARACTERS = '<decimal separator><group separator>' NLS_CURRENCY = 'currency_symbol' NLS_ISO_CURRENCY = <territory>");
			items[0].Text.ShouldBe("'NLS_NUMERIC_CHARACTERS = ''<decimal separator><group separator>'' NLS_CURRENCY = ''currency_symbol'' NLS_ISO_CURRENCY = <territory>'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test(Description = @"")]
		public void TestToCharSpecialParameterCompletionAtIncompatibleParameterIndex()
		{
			const string statement = @"SELECT TO_CHAR(12.34, '9G999D00', '') FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 30).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSysContextSpecialParameterCompletion()
		{
			const string statement = @"SELECT SYS_CONTEXT('USERENV', '') FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(59);
			items[0].Name.ShouldBe("ACTION - Identifies the position in the module (application name) and is set through the DBMS_APPLICATION_INFO package or OCI. ");
			items[0].Text.ShouldBe("'ACTION'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[58].Name.ShouldBe("TERMINAL - The operating system identifier for the client of the current session. ");
			items[58].Text.ShouldBe("'TERMINAL'");
			items[58].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test(Description = @"")]
		public void TestSysContextSpecialParameterCompletionWithIncompatibleNamespace()
		{
			const string statement = @"SELECT SYS_CONTEXT(X || 'USERENV', '') FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 36).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestConvertSpecialParameterCompletion()
		{
			const string statement = @"SELECT CONVERT('sample text', '', '') FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(2);
			items[0].Name.ShouldBe("US7ASCII");
			items[0].Text.ShouldBe("'US7ASCII'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Name.ShouldBe("WE8ISO8859P1");
			items[1].Text.ShouldBe("'WE8ISO8859P1'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test(Description = @"")]
		public void TestTableIdentifierAndAllTableColumnCompletion()
		{
			const string statement = @"SELECT SEL FROM SELECTION, RESPONDENTBUCKET";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			items.Count.ShouldBe(3);
			items[0].Name.ShouldBe("SELECTION.*");
			items[0].Text.ShouldBe("SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, SELECTION.PROJECT_ID, SELECTION.NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.AllColumns);
			items[1].Name.ShouldBe("SELECTION.SELECTION_ID");
			items[1].Text.ShouldBe("SELECTION.SELECTION_ID");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[2].Name.ShouldBe("SELECTION");
			items[2].Text.ShouldBe("SELECTION");
			items[2].CaretOffset.ShouldBe(0);
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test(Description = @"")]
		public void TestColumnCodeCompletionWithStatementWithoutQueryBlock()
		{
			const string statement = @"UPDATE SELECTION SET PROJECT_ID = 998 WHERE E";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 45).ToList();
			items.Count.ShouldBe(8);
		}

		[Test(Description = @"")]
		public void TestColumnCodeCompletionOfSubqueryMainObjectReference()
		{
			const string statement = @"DELETE (SELECT * FROM SELECTION) WHERE SELECTION_ID = 0";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 41).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SESSIONTIMEZONE");
			items[0].CaretOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSequenceObjectCodeCompletion()
		{
			const string statement = @"SELECT SEQ FROM SELECTION";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			items.Count.ShouldBe(2);
			items[0].Name.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].Text.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].CaretOffset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Name.ShouldBe("TEST_SEQ");
			items[1].Text.ShouldBe("TEST_SEQ");
			items[1].CaretOffset.ShouldBe(0);
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test(Description = @"")]
		public void TestSchemaTypeCodeCompletion()
		{
			const string statement = @"SELECT XML FROM SELECTION";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("XMLTYPE");
			items[0].Text.ShouldBe("XMLTYPE()");
			items[0].CaretOffset.ShouldBe(-1);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test(Description = @"")]
		public void TestSequenceObjectCodeCompletionInSubquery()
		{
			const string statement = @"SELECT * FROM (SELECT SEQ FROM SELECTION)";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 25).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestOtherSchemaObjectCodeCompletionThroughSynonymWithInaccessibleTargetObject()
		{
			const string statement = @"SELECT INACESSIBLE FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 18).ToList();
			items.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestCaretOffsetWhenTypingSourceRowSource()
		{
			const string statement = @"SELECT * FROM SELECTIO";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22).ToList();
			items.Count.ShouldBe(2);
			items[0].Name.ShouldBe("SELECTION");
			items[0].Text.ShouldBe("SELECTION");
			items[0].CaretOffset.ShouldBe(0);
			items[1].Name.ShouldBe("SYNONYM_TO_SELECTION");
			items[1].Text.ShouldBe("SYNONYM_TO_SELECTION");
			items[1].CaretOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSequenceSuggestionInInsertValuesClause()
		{
			const string statement = @"INSERT INTO SELECTION (SELECTION_ID) VALUES (SEQ)";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 48).ToList();
			items.Count.ShouldBe(2);
			items[0].Name.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].Text.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].CaretOffset.ShouldBe(0);
			items[1].Name.ShouldBe("TEST_SEQ");
			items[1].Text.ShouldBe("TEST_SEQ");
			items[1].CaretOffset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSynonymPackageSuggestion()
		{
			const string statement = @"SELECT DBMS_RA FROM DUAL";
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 14).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("DBMS_RANDOM");
			items[0].Text.ShouldBe("DBMS_RANDOM.");
			items[0].CaretOffset.ShouldBe(0);
		}

		public class OracleCodeCompletionTypeTest
		{
			private static OracleCodeCompletionType InitializeCodeCompletionType(string statementText, int cursorPosition)
			{
				var documentRepository = TestFixture.CreateDocumentRepository();
				documentRepository.UpdateStatements(statementText);
				return new OracleCodeCompletionType(documentRepository, statementText, cursorPosition);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeAfterOnTerminalWithinJoinCondition()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
				var completionType = InitializeCodeCompletionType(statement, statement.Length);
				completionType.JoinCondition.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
			}

			[Test(Description = @""), Ignore]
			public void TestCodeCompletionTypeAfterExistingConditionInJoinClause()
			{
				const string statement = @"SELECT * FROM SELECTION JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET AND ";
				var completionType = InitializeCodeCompletionType(statement, statement.Length);
				completionType.JoinCondition.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeAtTheEndOnTerminalWithinJoinCondition()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
				var completionType = InitializeCodeCompletionType(statement, statement.Length - 1);
				completionType.JoinCondition.ShouldBe(false);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeWithinFunctionParameterList()
			{
				const string statement = @"SELECT SQLPAD.SQLPAD_FUNCTION(D) FROM DUAL";
				var completionType = InitializeCodeCompletionType(statement, 31);
				completionType.SchemaDataObjectReference.ShouldBe(true);
				completionType.Schema.ShouldBe(true);
				completionType.SchemaProgram.ShouldBe(true);
				completionType.PackageFunction.ShouldBe(false);
				completionType.Column.ShouldBe(true);
				completionType.AllColumns.ShouldBe(false);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeWithinChainedFunctionParameterList()
			{
				const string statement = @"SELECT LENGTH(SELECTION.NAME) + LENGTH(N) FROM SELECTION";
				var completionType = InitializeCodeCompletionType(statement, 40);
				completionType.SchemaDataObjectReference.ShouldBe(true);
				completionType.Schema.ShouldBe(true);
				completionType.SchemaProgram.ShouldBe(true);
				completionType.PackageFunction.ShouldBe(false);
				completionType.Column.ShouldBe(true);
				completionType.AllColumns.ShouldBe(false);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeAfterSchemaOrObjectIdentifierCandidate()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
				var completionType = InitializeCodeCompletionType(statement, 16);
				completionType.Schema.ShouldBe(false);
				completionType.Column.ShouldBe(true);
				completionType.AllColumns.ShouldBe(true);
				completionType.SchemaProgram.ShouldBe(true);
				completionType.PackageFunction.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
				//completionType.SchemaDataObjectReference.ShouldBe(true);
			}
			
			[Test(Description = @"")]
			public void TestCodeCompletionTypeAfterSemicolonAfterExpectedJoinCondition()
			{
				const string statement = @"SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES;";
				var completionType = InitializeCodeCompletionType(statement, statement.Length);
				completionType.Schema.ShouldBe(false);
				completionType.JoinType.ShouldBe(false);
				completionType.JoinCondition.ShouldBe(false);
				completionType.Column.ShouldBe(false);
				completionType.AllColumns.ShouldBe(false);
				completionType.SchemaProgram.ShouldBe(false);
				completionType.PackageFunction.ShouldBe(false);
				completionType.SchemaDataObject.ShouldBe(false);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeWhenTypingDatabaseLinkIdentifier()
			{
				const string statement = @"SELECT * FROM CUSTOMER@H";
				var completionType = InitializeCodeCompletionType(statement, 24);
				completionType.Schema.ShouldBe(false);
				completionType.Column.ShouldBe(false);
				completionType.AllColumns.ShouldBe(false);
				completionType.SchemaProgram.ShouldBe(false);
				completionType.PackageFunction.ShouldBe(false);
				completionType.SchemaDataObject.ShouldBe(false);
				completionType.SchemaDataObjectReference.ShouldBe(false);
				completionType.DatabaseLink.ShouldBe(true);
			}

			public class ReferenceIdentifierTest
			{
				public class SelectList
				{
					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenCursorAtIdentifierOfAlreadyEnteredFullyQualifiedColumnIdentifier()
					{
						const string statement = @"SELECT HUSQVIK.SELECTION.SELECTION_ID FROM HUSQVIK.SELECTION";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 29).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(29);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("SELECTION_ID");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe("SELE");
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenCursorAtIdentifierOfAlreadyEnteredFullyQualifiedPackageFunctionIdentifier()
					{
						const string statement = @"SELECT HUSQVIK.SQLPAD.SQLPAD_FUNCTION() FROM DUAL";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 25).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(25);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SQLPAD");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("SQLPAD_FUNCTION");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SQLPAD");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe("SQL");
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenCursorAtIdentifierOfAlreadyEnteredFullyQualifiedFunctionIdentifier()
					{
						const string statement = @"SELECT HUSQVIK.SQLPAD_FUNCTION() FROM DUAL";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 18).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(18);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("SQLPAD_FUNCTION");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("HUSQVIK");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe("SQL");
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenCursorAtObjectIdentifierOfAlreadyEnteredFullyQualifiedColumnIdentifier()
					{
						const string statement = @"SELECT HUSQVIK.SELECTION.SELECTION_ID FROM HUSQVIK.SELECTION";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 19).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(19);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("SELECTION_ID");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SELE");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe(null);
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenCursorAtSchemaIdentifierOfAlreadyEnteredFullyQualifiedColumnIdentifier()
					{
						const string statement = @"SELECT HUSQVIK.SELECTION.SELECTION_ID FROM HUSQVIK.SELECTION";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 10).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(10);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("SELECTION_ID");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe("HUS");
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe(null);
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenTypingColumnIdentifier()
					{
						const string statement = @"SELECT NAM FROM SELECTION";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 10).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(10);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("NAM");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe("NAM");
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenTypingObjectIdentifier()
					{
						const string statement = @"SELECT SELECTION.NAM FROM SELECTION";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 20).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(20);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("NAM");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe("NAM");
					}
				}

				public class FromClause
				{
					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenTypingFullyQualifiedObjectIdentifier()
					{
						const string statement = @"SELECT * FROM HUSQVIK.SELE";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 26).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(26);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELE");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SELE");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe(null);
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenTypingObjectIdentifier()
					{
						const string statement = @"SELECT * FROM SELE";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 18).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(18);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELE");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SELE");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe(null);
					}

					[Test(Description = @"")]
					public void TestReferenceIdentifiersWhenCursorAtObjectIdentifierOfAlreadyEnteredFullyQualifiedObjectName()
					{
						const string statement = @"SELECT * FROM HUSQVIK.SELECTION";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 26).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(26);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SELECTION");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe("HUSQVIK");
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SELE");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe(null);
					}
				}
			}
		}
	}
}
