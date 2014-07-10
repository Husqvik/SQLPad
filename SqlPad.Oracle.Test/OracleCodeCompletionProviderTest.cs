using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleCodeCompletionProviderTest
	{
		private readonly OracleCodeCompletionProvider _codeCompletionProvider = new OracleCodeCompletionProvider();
		private static readonly OracleSqlParser Parser = new OracleSqlParser();
		private readonly SqlDocumentRepository _documentRepository = new SqlDocumentRepository(new OracleSqlParser(), new OracleStatementValidator(), TestFixture.DatabaseModel);

		[Test(Description = @"")]
		public void TestObjectSuggestionWithSchema()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 37).ToArray();
			items.Length.ShouldBe(9);
			items[0].Name.ShouldBe("\"CaseSensitiveTable\"");
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[8].Name.ShouldBe("VIEW_INSTANTSEARCH");
			items[8].Text.ShouldBe("VIEW_INSTANTSEARCH");
		}

		[Test(Description = @"")]
		public void TestJoinTypeSuggestion()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I ", 52).ToArray();
			// TODO: Filter out outer types depending of nullable columns
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[0].Offset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
			items[4].Name.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
			items[4].Offset.ShouldBe(0);
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
			//items[0].Offset.ShouldBe(0);
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
			items[0].Offset.ShouldBe(0);
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
			items[0].Offset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionsWhenJoiningNestedSubquery()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM (SELECT 1 VAL FROM DUAL) T1 JOIN (SELECT 1 VAL FROM DUAL) T2 ", 75).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON T1.VAL = T2.VAL");
			items[0].Text.ShouldBe("ON T1.VAL = T2.VAL");
			items[0].Offset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJoinConditionSuggestionForTablesWithForeignKeys()
		{
			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID JOIN RESPONDENTBUCKET B ", 106).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Text.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Offset.ShouldBe(0);
			items[1].Name.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Text.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Offset.ShouldBe(0);
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
			items[0].Offset.ShouldBe(0);
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

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 10).ToArray();
			items.Length.ShouldBe(10);
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
			// Schemas
			items[7].Name.ShouldBe("HUSQVIK");
			items[7].Text.ShouldBe("HUSQVIK");
			items[7].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
			items[8].Name.ShouldBe("SYS");
			items[8].Text.ShouldBe("SYS");
			items[8].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
			items[9].Name.ShouldBe("SYSTEM");
			items[9].Text.ShouldBe("SYSTEM");
			items[9].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
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
			items.Length.ShouldBe(3);
			items[0].Name.ShouldBe("RESPONDENTBUCKET_ID");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID");
			items[1].Name.ShouldBe(OracleColumn.RowId);
			items[2].Name.ShouldBe("SELECTION_ID");
			items[2].Text.ShouldBe("SELECTION_ID");

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

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 8, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("DUAL.DUMMY");
			items[0].Text.ShouldBe("DUAL.DUMMY");

			const string query2 = @"SELECT D FROM DUAL X";

			items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query2, 8, OracleCodeCompletionCategory.Column).ToArray();
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
		public void TestNothingIsSuggestedAfterFromTerminal()
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
			var currentSchemaTableCount = OracleTestDatabaseModel.Instance.AllObjects.Values.Count(o => o.Owner == OracleTestDatabaseModel.Instance.CurrentSchema && o.Type.In(OracleSchemaObjectType.Table, OracleSchemaObjectType.View));
			var schemaCount = OracleTestDatabaseModel.Instance.Schemas.Count; // PUBLIC excluded
			items.Length.ShouldBe(currentSchemaTableCount + schemaCount);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionWhenTableWithSchemaInFromClause()
		{
			const string query1 = @"SELECT  1 FROM SYS.DUAL";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, OracleCodeCompletionCategory.SchemaObject, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("SYS.DUAL.DUMMY");
			items[1].Name.ShouldBe("SYS.DUAL");
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

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7).Where(i => !i.Name.Contains("HUSQVIK.SELECTION") && i.Category != OracleCodeCompletionCategory.DatabaseSchema).ToArray();
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

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, OracleCodeCompletionCategory.Column, OracleCodeCompletionCategory.AllColumns).ToArray();
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
			items.Length.ShouldBe(10);
			items[0].Name.ShouldBe("AS_PDF3");
			items[0].Text.ShouldBe("AS_PDF3.");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Package);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
			items[9].Name.ShouldBe("TESTFUNC");
			items[9].Text.ShouldBe("TESTFUNC()");
			items[9].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[9].CaretOffset.ShouldBe(-1);
			items[9].StatementNode.ShouldBe(null);
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
			items.Count.ShouldBe(3);
			items.ForEach(i => i.Name.ShouldBe("SYS.STANDARD.ROUND"));
			items.ForEach(i => i.Parameters.Count.ShouldBe(2));
			items.ForEach(i => i.CurrentParameterIndex.ShouldBe(1));
			items.ForEach(i => i.ReturnedDatatype.ShouldNotBe(null));
		}

		[Test(Description = @"")]
		public void TestResolveFunctionOverloadsOutsideTerminal()
		{
			const string query1 = @"SELECT ROUND(1.23, 1) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 18).ToList();
			items.Count.ShouldBe(0);
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
		}

		[Test(Description = @"")]
		public void TestTableSuggestionAfterQuoteCharacter()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable""";

			var items = _codeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15).ToList();
			items.Count.ShouldBe(13);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
		}

		public class OracleCodeCompletionTypeTest
		{
			private static OracleCodeCompletionType InitializeCodeCompletionType(string statementText, int cursorPosition)
			{
				return new OracleCodeCompletionType(Parser.Parse(statementText), statementText, cursorPosition);
			}

			[Test(Description = @"")]
			public void TestCodeCompletionTypeAfterOnTerminalWithinJoinCondition()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
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
			public void TestCodeCompletionTypeAfterSchemaOrObjectIdentifierCandidate()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
				var completionType = InitializeCodeCompletionType(statement, 16);
				completionType.Schema.ShouldBe(false);
				completionType.Column.ShouldBe(true);
				completionType.AllColumns.ShouldBe(true);
				completionType.Program.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
				//completionType.SchemaDataObjectReference.ShouldBe(true);
			}
		}
	}
}
