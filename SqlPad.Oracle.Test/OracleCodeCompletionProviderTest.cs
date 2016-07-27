using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using System;
using System.Diagnostics;
using System.Globalization;
using SqlPad.Oracle.DataDictionary;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleCodeCompletionProviderTest
	{
		private static readonly OracleCodeCompletionProvider CodeCompletionProvider = new OracleCodeCompletionProvider();
		private readonly SqlDocumentRepository _documentRepository = TestFixture.CreateDocumentRepository();
		private static readonly Func<ICodeCompletionItem, bool> FilterProgramItems = i => !i.Category.In(OracleCodeCompletionCategory.PackageFunction, OracleCodeCompletionCategory.Package, OracleCodeCompletionCategory.SchemaFunction, OracleCodeCompletionCategory.BuiltInFunction);

		[SetUp]
		public void Setup()
		{
			OracleConfiguration.Configuration.Formatter.FormatOptions.Reset();
		}

		[Test]
		public void TestCodeCompletionAfterEachCharacter()
		{
			const string query =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 5
)
SEARCH DEPTH FIRST BY VAL SET SEQ#
CYCLE VAL SET CYCLE# TO 'X' DEFAULT 'O'
SELECT * FROM CTE JOIN DUAL ON TO_CHAR(VAL) <> DUMMY CROSS APPLY (SELECT * FROM DUAL) T2 WHERE VAL = SEQ# AND CYCLE# = 'O' ORDER BY SEQ# DESC, VAL";

			var allocatedCharacters = (int)Math.Ceiling(Math.Log10(query.Length));

			for (var i = 1; i < query.Length; i++)
			{
				var effectiveQuery = query.Substring(0, i);
				var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, effectiveQuery, effectiveQuery.Length);

				var paddedCaretPosition = effectiveQuery.Length.ToString(CultureInfo.InvariantCulture).PadLeft(allocatedCharacters);
				Trace.WriteLine($"Caret position: {paddedCaretPosition}; Suggested items: {items.Count}");
			}
		}

		[Test]
		public void TestObjectSuggestionWithSchema()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 37).ToArray();
			items.Length.ShouldBe(10);
			items[0].Label.ShouldBe("\"CaseSensitiveTable\"");
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[9].Label.ShouldBe("VIEW_INSTANTSEARCH");
			items[9].Text.ShouldBe("VIEW_INSTANTSEARCH");
		}

		[Test]
		public void TestJoinTypeSuggestion()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I ", 52, true, OracleCodeCompletionCategory.JoinMethod).ToArray();
			// TODO: Filter out outer types depending of nullable columns
			items.Length.ShouldBe(5);
			items[0].Label.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[0].InsertOffset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
			items[4].Label.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
			items[4].InsertOffset.ShouldBe(0);
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
		}

		[Test]
		public void TestColumnSuggestionAtTheBeginningOfColumnNameWhenAlreadyEntered()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 21).ToArray();
			items.Length.ShouldBe(3);
			items[0].Label.ShouldBe("DUEDATE");
			items[0].Text.ShouldBe("DUEDATE");
			items[1].Label.ShouldBe("ORA_ROWSCN");
			items[2].Label.ShouldBe(TerminalValues.RowIdPseudocolumn);
		}

		[Test]
		public void TestObjectSuggestionInUnfinishedStatements()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 35).ToArray();
			items.Length.ShouldBe(2);
			items[0].Label.ShouldBe("INVOICELINES");
			items[0].Text.ShouldBe("INVOICELINES");
			//items[0].InsertOffset.ShouldBe(0);
			items[1].Label.ShouldBe("INVOICES");
			items[1].Text.ShouldBe("INVOICES");

			items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 57).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("INVOICES");
			items[0].Text.ShouldBe("INVOICES");
		}

		[Test]
		public void TestJoinConditionSuggestions()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P", 50).ToArray();
			items.Length.ShouldBe(0);

			items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ", 51).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].Text.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test]
		public void TestJoinConditionSuggestionsAfterOnKeyword()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON", 53).ToArray();
			items.Length.ShouldBe(0);

			items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON ", 54, categories: new[] { OracleCodeCompletionCategory.JoinConditionByName, OracleCodeCompletionCategory.JoinConditionByReferenceConstraint }).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].Text.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test]
		public void TestJoinConditionSuggestionsWhenJoiningNestedSubquery()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM (SELECT 1 VAL FROM DUAL) T1 JOIN (SELECT 1 VAL FROM DUAL) T2 ", 75).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("ON T1.VAL = T2.VAL");
			items[0].Text.ShouldBe("ON T1.VAL = T2.VAL");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test]
		public void TestJoinConditionSuggestionForTablesWithForeignKeys()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID JOIN RESPONDENTBUCKET B ", 106).ToArray();
			items.Length.ShouldBe(2);
			items[0].Label.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Text.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].InsertOffset.ShouldBe(0);
			items[1].Label.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Text.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].InsertOffset.ShouldBe(0);
		}

		[Test]
		public void TestJoinConditionSuggestionAfterTableFunctionClause()
		{
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, "SELECT * FROM SELECTION JOIN TABLE(PIPELINED_FUNCTION) T ON ", 60, categories: new [] { OracleCodeCompletionCategory.JoinConditionByName }).ToArray();
			items.Length.ShouldBe(0);
			// TODO: Add proper implementation
		}

		[Test]
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

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 173).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("ON CTE1.ID = CTE2.ID");
			items[0].Text.ShouldBe("ON CTE1.ID = CTE2.ID");
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test]
		public void TestObjectSuggestionInJoinClauseWithPartialName()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN P";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 34).ToArray();
			items.Length.ShouldBe(4);
			items[0].Label.ShouldBe("PROJECT");
			items[0].Text.ShouldBe("PROJECT");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Label.ShouldBe("PUBLIC_SYNONYM_TO_SELECTION");
			items[1].Text.ShouldBe("PUBLIC_SYNONYM_TO_SELECTION");
			items[2].Label.ShouldBe("RESPONDENTBUCKET");
			items[2].Text.ShouldBe("RESPONDENTBUCKET");
			items[3].Label.ShouldBe("TARGETGROUP");
			items[3].Text.ShouldBe("TARGETGROUP");
		}

		[Test]
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

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 168).Where(i => i.Category == OracleCodeCompletionCategory.CommonTableExpression).ToArray();
			items.Length.ShouldBe(2);
			items[0].Label.ShouldBe("CTE1");
			items[0].Text.ShouldBe("CTE1");
			items[1].Label.ShouldBe("CTE2");
			items[1].Text.ShouldBe("CTE2");
		}

		[Test]
		public void TestObjectSuggestionAfterEnteredSchema()
		{
			const string query1 = @"SELECT * FROM SYS.";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 18).ToArray();
			items.Length.ShouldBe(3);
			items[0].Label.ShouldBe("ALL_TABLES");
			items[0].Text.ShouldBe("ALL_TABLES");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Label.ShouldBe("DUAL");
			items[1].Text.ShouldBe("DUAL");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[2].Label.ShouldBe("V_$SESSION");
			items[2].Text.ShouldBe("V_$SESSION");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test]
		public void TestSuggestionWhenEnteringNewColumn()
		{
			const string query1 = @"SELECT 1,  FROM SELECTION S";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 10).Where(FilterProgramItems).ToArray();
			items.Length.ShouldBe(16);
			items[0].Label.ShouldBe("S.*");
			items[0].Text.ShouldBe("S.RESPONDENTBUCKET_ID, S.SELECTION_ID, S.PROJECT_ID, S.NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.AllColumns);
			items[1].Label.ShouldBe("S.NAME");
			items[1].Text.ShouldBe("S.NAME");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[2].Label.ShouldBe("S.ORA_ROWSCN");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.Pseudocolumn);
			items[3].Label.ShouldBe("S.PROJECT_ID");
			items[3].Text.ShouldBe("S.PROJECT_ID");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[4].Label.ShouldBe("S.RESPONDENTBUCKET_ID");
			items[4].Text.ShouldBe("S.RESPONDENTBUCKET_ID");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[5].Label.ShouldBe("S.ROWID");
			items[5].Category.ShouldBe(OracleCodeCompletionCategory.Pseudocolumn);
			items[6].Label.ShouldBe("S.SELECTION_ID");
			items[6].Text.ShouldBe("S.SELECTION_ID");
			items[6].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			// Objects
			items[7].Label.ShouldBe("S");
			items[7].Text.ShouldBe("S");
			items[7].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			// Other schema objects
			items[8].Label.ShouldBe("INVALID_OBJECT_TYPE");
			items[8].Text.ShouldBe("INVALID_OBJECT_TYPE()");
			items[8].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[11].Label.ShouldBe("TEST_SEQ");
			items[11].Text.ShouldBe("TEST_SEQ");
			items[11].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[12].Label.ShouldBe("XMLTYPE");
			items[12].Text.ShouldBe("XMLTYPE()");
			items[12].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			// Schemas
			items[13].Label.ShouldBe("HUSQVIK");
			items[13].Text.ShouldBe("HUSQVIK");
			items[13].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
			items[14].Label.ShouldBe("SYS");
			items[14].Text.ShouldBe("SYS");
			items[14].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
			items[15].Label.ShouldBe("SYSTEM");
			items[15].Text.ShouldBe("SYSTEM");
			items[15].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
		}

		[Test]
		public void TestSchemaSuggestionInSelectListWhenPartiallyEntered()
		{
			const string query1 = @"SELECT HU FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 9).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("HUSQVIK");
			items[0].Text.ShouldBe("HUSQVIK");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
		}

		[Test]
		public void TestTableSuggestionWithPartialName()
		{
			const string query1 = @"SELECT 1 FROM SYSTEM.C";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 22).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestColumnSuggestionForMultipleTables()
		{
			const string query1 = @"SELECT SELECTION. FROM SELECTION, TARGETGROUP";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 17).ToArray();
			items.Length.ShouldBe(7);
			items[0].Label.ShouldBe("*");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, SELECTION.PROJECT_ID, SELECTION.NAME");
			items[1].Label.ShouldBe("NAME");
			items[1].Text.ShouldBe("NAME");
			items[5].Label.ShouldBe(TerminalValues.RowIdPseudocolumn);
			items[6].Label.ShouldBe("SELECTION_ID");
			items[6].Text.ShouldBe("SELECTION_ID");

			const string query2 = @"SELECT SELECTION.NAME FROM SELECTION, TARGETGROUP";

			items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query2, 18).ToArray();
			items.Length.ShouldBe(3);
			items[0].Label.ShouldBe("ORA_ROWSCN");
			items[1].Label.ShouldBe("RESPONDENTBUCKET_ID");
			items[1].Text.ShouldBe("RESPONDENTBUCKET_ID");
			items[2].Label.ShouldBe("SELECTION_ID");
			items[2].Text.ShouldBe("SELECTION_ID");

			const string query3 = @"SELECT SELECTION.NAME FROM SELECTION, TARGETGROUP";

			items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query3, 19).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestJoinTypeSuggestionInChainedJoinClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION S LEFT JOIN RESPONDENTBUCKET ON S.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 120, true, OracleCodeCompletionCategory.JoinMethod).ToArray();
			items.Length.ShouldBe(5);
			items[0].Label.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[4].Label.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
		}

		[Test]
		public void TestSchemaSuggestionInJoinConditionWhenAlreadyEnteredAndOnlyOneOptionRemains()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestTableSuggestionInJoinConditionWhenAlreadyEnteredAndOnlyOneOptionRemains()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 43).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestColumnSuggestionWithAlias()
		{
			const string query1 = @"SELECT D FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 8, true, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("DUAL.DUMMY");
			items[0].Text.ShouldBe("DUAL.DUMMY");

			const string query2 = @"SELECT D FROM DUAL X";

			items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query2, 8, true, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("X.DUMMY");
			items[0].Text.ShouldBe("X.DUMMY");
		}

		[Test]
		public void TestTableSuggestionWhenWithinScalarSubquery()
		{
			const string query1 = @"SELECT NULL FROM DUAL WHERE DUMMY = (SELECT * FROM DUAL)";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 53).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestNoColumnSuggestedWhenOnlyOneOptionExistsAndAlreadyInPlace()
		{
			const string query1 = @"SELECT SELECTION.NAME FROM SELECTION";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 8, true, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestColumnSuggestionAfterDotInGroupByClause()
		{
			const string query1 = @"SELECT * FROM PROJECT P GROUP BY P.";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToArray();
			items.Length.ShouldBe(4);
			items[0].Label.ShouldBe("NAME");
			items[0].Text.ShouldBe("NAME");
			items[0].StatementNode.ShouldBe(null);
			items[1].Label.ShouldBe("ORA_ROWSCN");
			items[2].Label.ShouldBe("PROJECT_ID");
			items[2].Text.ShouldBe("PROJECT_ID");
			items[2].StatementNode.ShouldBe(null);
			items[3].Label.ShouldBe(TerminalValues.RowIdPseudocolumn);
			items[3].Text.ShouldBe(TerminalValues.RowIdPseudocolumn);
			items[3].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestObjectsAreSuggestedAfterFromTerminal()
		{
			const string query1 = @"SELECT * FROM ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 14).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestNothingIsSuggestedWhenOnFromTerminal()
		{
			const string query1 = @"SELECT * FROM ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 9).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestUniqueOrDistinctKeywordSuggestion()
		{
			const string query1 = @"SELECT ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, true, OracleCodeCompletionCategory.Keyword).ToArray();
			items.Length.ShouldBe(2);
		}

		[Test]
		public void TestObjectSuggestionInChainedFromClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION, ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 28).ToArray();
			var currentSchemaTableCount = OracleTestDatabaseModel.Instance.AllObjects.Values.Count(FilterRowSources);
			var schemaCount = OracleTestDatabaseModel.Instance.Schemas.Count; // PUBLIC excluded
			items.Length.ShouldBe(currentSchemaTableCount + schemaCount);
		}

		private static bool FilterRowSources(OracleSchemaObject schemaObject)
		{
			var targetObject = schemaObject.GetTargetSchemaObject();
			return targetObject != null && schemaObject.Owner.In(OracleTestDatabaseModel.Instance.CurrentSchema, OracleObjectIdentifier.SchemaPublic) && targetObject.Type.In(OracleObjectType.Table, OracleObjectType.View);
		}

		[Test]
		public void TestColumnSuggestionWhenTableWithSchemaInFromClause()
		{
			const string query1 = @"SELECT  1 FROM SYS.DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, true, OracleCodeCompletionCategory.SchemaObject, OracleCodeCompletionCategory.Column).ToArray();
			items.Length.ShouldBe(7);
			items[0].Label.ShouldBe("SYS.DUAL.DUMMY");
			items[1].Label.ShouldBe("SYS.DUAL");
			items[6].Label.ShouldBe("XMLTYPE");
		}

		[Test]
		public void TestAsteriskIsNotSuggestedWithinNestedExpression()
		{
			const string query1 = @"SELECT CASE WHEN S. FROM SELECTION S";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 19).ToArray();
			items.Length.ShouldBe(6);
			items[0].Label.ShouldBe("NAME");
			items[1].Label.ShouldBe("ORA_ROWSCN");
			items[4].Label.ShouldBe(TerminalValues.RowIdPseudocolumn);
			items[5].Label.ShouldBe("SELECTION_ID");
		}

		[Test]
		public void TestJoinConditionWhenCommonTableExpressionColumnsAreNotAliased()
		{
			const string query1 = @"WITH X AS (SELECT 1 FROM DUAL), Y AS (SELECT 1 FROM DUAL) SELECT * FROM X JOIN Y ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 81).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("ON X.\"1\" = Y.\"1\"");
		}

		[Test]
		public void TestObjectAndSchemaSuggestionWhenTypingSubquery()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM )";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestSchemaObjectSuggestionWhenTypingSubqueryAfterDotAfterSchema()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM HUSQVIK.)";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 43).ToArray();
			items.Length.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestColumnSuggestionWhenQueryContainsNonAliasedSubquery()
		{
			const string query1 = @"SELECT  FROM (SELECT HUSQVIK.SELECTION.NAME FROM HUSQVIK.SELECTION), HUSQVIK.SELECTION";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7).Where(FilterProgramItems).Where(i => !i.Label.Contains("HUSQVIK.SELECTION") && !i.Category.In(OracleCodeCompletionCategory.DatabaseSchema, OracleCodeCompletionCategory.SchemaObject, OracleCodeCompletionCategory.BuiltInFunction, OracleCodeCompletionCategory.Keyword)).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestColumnSuggestionAfterDotInTheMiddleOfSelectList()
		{
			const string query1 = @"SELECT S.NAME, S., 'My column2' FROM SELECTION S";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 17).ToArray();
			items.Length.ShouldBe(7);
			items[0].Label.ShouldBe("*");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID, S.SELECTION_ID, S.PROJECT_ID, S.NAME");
			items[2].Label.ShouldBe("ORA_ROWSCN");
			items[5].Label.ShouldBe(TerminalValues.RowIdPseudocolumn);
			items[6].Label.ShouldBe("SELECTION_ID");
			items[6].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestJoinNotSuggestedAfterUnrecognizedToken()
		{
			const string query1 = @"SELECT NULL FROM SELECTION + ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 29).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestSuggestionWhenJustBeforeClosingParanthesis()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM DUAL,)";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 40).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestSuggestionWhenJustAtCommaWhenPrecedingTokenAlreadyEntered()
		{
			const string query1 = @"SELECT NULL FROM (SELECT NULL FROM DUAL,)";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 39).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestSuggestionWhenAtSelectKeyword()
		{
			const string query1 = @"SELECT NAME FROM SELECTION";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 0).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestColumnSuggestionUsingNestedQueryAndCountAsteriskFunction()
		{
			const string query1 = @"SELECT  DUMMY FROM (SELECT DUMMY, COUNT(*) OVER () ROW_COUNT FROM (SELECT DUMMY FROM DUAL))";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 7, true, OracleCodeCompletionCategory.Column, OracleCodeCompletionCategory.AllColumns).ToArray();
			items.Length.ShouldBe(3);
			items[0].Label.ShouldBe("*");
			items[0].Text.ShouldBe("DUMMY, ROW_COUNT");
			items[1].Label.ShouldBe("DUMMY");
			items[2].Label.ShouldBe("ROW_COUNT");
		}

		[Test]
		public void TestPackageAndFunctionSuggestion()
		{
			const string query1 = @"SELECT HUSQVIK. FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15).ToArray();
			items.Length.ShouldBe(11);
			items[4].Label.ShouldBe("AS_PDF3");
			items[4].Text.ShouldBe("AS_PDF3.");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.Package);
			items[4].CaretOffset.ShouldBe(0);
			items[4].StatementNode.ShouldBe(null);
			items[8].Label.ShouldBe("TESTFUNC");
			items[8].Text.ShouldBe("TESTFUNC()");
			items[8].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[8].CaretOffset.ShouldBe(-1);
			items[8].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestPackageOrFunctionNotSuggestedWhenAtSchemaToken()
		{
			const string query1 = @"SELECT HUSQVIK. FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 14).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestPackageFunctionSuggestionWhenAlreadyEntered()
		{
			const string query1 = @"SELECT HUSQVIK.SQLPAD FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 18).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("SQLPAD_FUNCTION");
			items[0].Text.ShouldBe("SQLPAD_FUNCTION()");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[0].CaretOffset.ShouldBe(-1);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestPackageFunctionSuggestionDoesNotContainParameterListWhenAlreadyEnteredWithParameterList()
		{
			const string query1 = @"SELECT HUSQVIK.SQLFUNCTION('PARAMETER') FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 18).ToArray();
			items.Length.ShouldBe(2);
			items[1].Label.ShouldBe("SQLPAD_FUNCTION");
			items[1].Text.ShouldBe("SQLPAD_FUNCTION");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
			items[1].CaretOffset.ShouldBe(0);
			items[1].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestNodeReplacementWhenAtTokenStartAndTokenAlreadyExists()
		{
			const string query1 = @"SELECT HUSQVIK.SFUNCTION('PARAMETER') FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15, true, OracleCodeCompletionCategory.SchemaFunction).ToArray();
			items.Length.ShouldBeGreaterThan(0);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestPackageFunctionNotSuggestedWhenSameFunctionAlreadyEntered()
		{
			const string query1 = @"SELECT HUSQVIK.SQLPAD.SQLPAD_FUNCTION('') FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 23).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestPackageFunctionSuggestion()
		{
			const string query1 = @"SELECT HUSQVIK.SQLPAD. FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 22).ToArray();
			items.Length.ShouldBe(3);
			items[0].Label.ShouldBe("CURSOR_FUNCTION");
			items[0].Text.ShouldBe("CURSOR_FUNCTION()");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
			items[0].CaretOffset.ShouldBe(-1);
			items[0].StatementNode.ShouldBe(null);
			items[1].Label.ShouldBe("PIPELINED_FUNCTION");
			items[1].Text.ShouldBe("PIPELINED_FUNCTION()");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
			items[1].CaretOffset.ShouldBe(-1);
			items[1].StatementNode.ShouldBe(null);
			items[2].Label.ShouldBe("SQLPAD_FUNCTION");
			items[2].Text.ShouldBe("SQLPAD_FUNCTION()");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
			items[2].CaretOffset.ShouldBe(-1);
			items[2].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestJoinConditionNotSuggestedForNonAliasedSubquery()
		{
			const string query1 = @"SELECT * FROM (SELECT PROJECT_ID FROM PROJECT) JOIN PROJECT ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 60).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestResolveProgramOverloadsFilteredByCurrentArgumentIndex()
		{
			const string query1 = @"SELECT ROUND(1.23, 1) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 19).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SYS.STANDARD.ROUND");
			items[0].Parameters.Count.ShouldBe(2);
			items[0].CurrentParameterIndex.ShouldBe(1);
			items[0].ReturnedDatatype.ShouldBe("NUMBER");
		}

		[Test]
		public void TestResolveProgramOverloadsWithParameterlessOverload()
		{
			const string query1 = @"SELECT DBMS_RANDOM.VALUE(1, 2) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 25).ToList();
			items.Count.ShouldBe(2);
			items[0].Name.ShouldBe("SYS.DBMS_RANDOM.VALUE");
			items[0].Parameters.Count.ShouldBe(0);
			items[0].CurrentParameterIndex.ShouldBe(0);
			items[0].ReturnedDatatype.ShouldBe("NUMBER");
			items[1].Name.ShouldBe("SYS.DBMS_RANDOM.VALUE");
			items[1].Parameters.Count.ShouldBe(2);
			items[1].CurrentParameterIndex.ShouldBe(0);
			items[1].ReturnedDatatype.ShouldBe("NUMBER");
		}

		[Test]
		public void TestResolveProgramOverloadsInUserDefinedTypeConstructor()
		{
			const string query1 = @"SELECT SYS.ODCIARGDESC() FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 23).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SYS.ODCIARGDESC");
			items[0].Parameters.Count.ShouldBe(7);
			items[0].CurrentParameterIndex.ShouldBe(0);
			items[0].ReturnedDatatype.ShouldBe("SYS.ODCIARGDESC");
		}

		[Test]
		public void TestResolveProgramOverloadsInUserDefinedCollectionTypeConstructorAtNonFirstParameter()
		{
			const string query1 = @"SELECT SYS.ODCIRAWLIST(NULL, NULL) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 29).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SYS.ODCIRAWLIST");
			items[0].Parameters.Count.ShouldBe(1);
			items[0].CurrentParameterIndex.ShouldBe(0);
			items[0].ReturnedDatatype.ShouldBe("SYS.ODCIRAWLIST");
		}

		[Test]
		public void TestResolveInsertValueColumnTypes()
		{
			const string query1 = @"INSERT INTO selection (name, created, status, project_id) VALUES (NULL, NULL, NULL, NULL)";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 83).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("SELECTION");
			items[0].Parameters.Count.ShouldBe(4);
			items[0].Parameters[3].ShouldBe("PROJECT_ID: NUMBER(9) NOT NULL");
			items[0].CurrentParameterIndex.ShouldBe(3);
			items[0].ReturnedDatatype.ShouldBe(null);
		}

		[Test]
		public void TestResolveInsertValueColumnTypesBeforeInsertValueList()
		{
			const string query1 = @"INSERT INTO selection (name, created, status, project_id) VALUES (NULL, NULL, NULL, NULL)";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 65).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestResolveInsertValueColumnTypesAfterInsertValueList()
		{
			const string query1 = @"INSERT INTO selection (name, created, status, project_id) VALUES (NULL, NULL, NULL, NULL)";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 89).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestResolveInsertValueColumnTypesWhenWithinFunctionParameterList()
		{
			const string query1 = @"INSERT INTO selection (project_id) VALUES (undefined())";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 53).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestResolveProgramOverloadsWithinPlSqlContext()
		{
			const string query1 = @"BEGIN dbms_output.put_line(''); END;";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 28).ToList();
			items.Count.ShouldBe(1);
		}

		[Test]
		public void TestResolveProgramOverloadsOutsideTerminal()
		{
			const string query1 = @"SELECT ROUND(1.23, 1) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 18).ToList();
			items.Count.ShouldBe(1);
			items[0].CurrentParameterIndex.ShouldBe(1);
		}

		[Test]
		public void TestResolveProgramOverloadsWithNonSchemaFunction()
		{
			const string query1 = @"SELECT MAX(DUMMY) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 11).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("MAX");
			items[0].Parameters.Count.ShouldBe(1);
			items[0].Parameters[0].ShouldBe("EXPR");
			items[0].CurrentParameterIndex.ShouldBe(0);
			items[0].ReturnedDatatype.ShouldBe(null);
		}

		[Test]
		public void TestResolveProgramOverloadsWithStatementDefinedFunction()
		{
			const string query1 =
@"WITH
	FUNCTION F1(p1 IN NUMBER, p2 IN NUMBER) RETURN NUMBER AS BEGIN RETURN DBMS_RANDOM.VALUE; END;
SELECT F1(NULL) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 112).ToList();
			items.Count.ShouldBe(1);
			items[0].Parameters.Count.ShouldBe(2);
			items[0].CurrentParameterIndex = 1;
			items[0].Parameters[0].ShouldBe("P1: NUMBER");
			items[0].ReturnedDatatype.ShouldBe("NUMBER");
		}

		[Test]
		public void TestResolveProgramOverloadsWithNonSchemaAggregateAndAnalyticFunction()
		{
			const string query1 = @"SELECT COUNT(*) FROM DUAL";

			_documentRepository.UpdateStatements(query1);
			var items = CodeCompletionProvider.ResolveProgramOverloads(_documentRepository, 14).ToList();
			items.Count.ShouldBe(1);
			items[0].Name.ShouldBe("COUNT");
			items[0].Parameters.Count.ShouldBe(1);
			items[0].Parameters[0].ShouldBe("EXPR");
			items[0].CurrentParameterIndex.ShouldBe(0);
			items[0].ReturnedDatatype.ShouldBe(null);
		}

		[Test]
		public void TestColumnSuggestionWhenIdentifierWithQuotedNotation()
		{
			const string query1 = @"SELECT IL."""" FROM INVOICELINES IL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 11).ToList();
			items.Count.ShouldBe(8);
			items[0].Text.ShouldBe("\"CaseSensitiveColumn\"");
		}

		[Test]
		public void TestPackageFunctionSuggestionWhenPackageContainsMoreFunctions()
		{
			const string query1 = @"SELECT DBMS_R FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 13).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("DBMS_RANDOM");
			items[0].Text.ShouldBe("DBMS_RANDOM.");
		}

		[Test]
		public void TestTableSuggestionInTheMiddleOfQuotedNotation()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable""";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 19).ToList();
			items.Count.ShouldBe(1);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[0].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestSuggestionWhenTypingDatabaseLinkIdentifier()
		{
			const string query1 = @"SELECT * FROM CUSTOMER@H";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 24).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("HQ_PDB_LOOPBACK");
			items[0].CaretOffset.ShouldBe(0);
			items[0].Text.ShouldBe("HQ_PDB_LOOPBACK");
			items[1].Label.ShouldBe("TESTHOST.SQLPAD.HUSQVIK.COM@HQINSTANCE");
			items[1].CaretOffset.ShouldBe(0);
			items[1].Text.ShouldBe("TESTHOST.SQLPAD.HUSQVIK.COM@HQINSTANCE");
		}

		[Test]
		public void TestSuggestionAtSecondStatementBeginningWithFirstStatementEndingWithQuotedIdentifier()
		{
			const string query1 =
@"SELECT * FROM ""PaymentPlans""
;

se";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 37).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestTableSuggestionAfterQuoteCharacter()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable""";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 15).ToList();
			items.Count.ShouldBe(18);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
		}

		[Test]
		public void TestCodeCompletionWhenTypingOrderAfterTableNameInFromClause()
		{
			const string query1 = @"SELECT * FROM ""CaseUnknownTable"" OR";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, query1, 35).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ORDER BY");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestCodeCompletionWhenTypingWithinParentheses()
		{
			const string statement = @"SELECT SQLPAD.SQLPAD_FUNCTION(D) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(9);
			items[0].Text.ShouldBe("DUAL.DUMMY");
			items[8].Label.ShouldBe("DUMP");
			items[8].Text.ShouldBe("DUMP()");
		}

		[Test]
		public void TestCodeCompletionWhenUsingNameParts()
		{
			const string statement = @"SELECT * FROM SenTab";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 20).ToList();
			items.Count.ShouldBe(1);
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
		}

		[Test]
		public void TestNoParenthesesFunctionCodeCompletionWhenUsingNameParts()
		{
			const string statement = @"SELECT SeTiZo FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 13, false).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("SESSIONTIMEZONE");
			items[0].Text.ShouldBe("SESSIONTIMEZONE");
		}

		[Test]
		public void TestCodeCompletionInComment()
		{
			const string statement = @"SELECT /*+ PARALLEL */ DUMMY FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 15).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestTruncFunctionSpecialParameterCompletion()
		{
			const string statement = @"SELECT TRUNC(SYSDATE, 'IW') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 23).ToList();
			items.Count.ShouldBe(11);
			items[0].Label.ShouldBe("CC - One greater than the first two digits of a four-digit year");
			items[0].Text.ShouldBe("'CC'");
			items[0].StatementNode.ShouldNotBe(null);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[10].Label.ShouldBe("YYYY (YEAR) - Year");
			items[10].Text.ShouldBe("'YYYY'");
			items[10].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[10].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestNumberToYearToMonthIntervalFunctionSpecialParameterCompletion()
		{
			const string statement = @"SELECT NUMTOYMINTERVAL(1, '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 27).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("MONTH");
			items[0].Text.ShouldBe("'MONTH'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("YEAR");
			items[1].Text.ShouldBe("'YEAR'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestNumberToDayToSecondIntervalFunctionSpecialParameterCompletion()
		{
			const string statement = @"SELECT NUMTODSINTERVAL(1, '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 27).ToList();
			items.Count.ShouldBe(4);
			items[0].Label.ShouldBe("DAY");
			items[0].Text.ShouldBe("'DAY'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("HOUR");
			items[1].Text.ShouldBe("'HOUR'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[2].Label.ShouldBe("MINUTE");
			items[2].Text.ShouldBe("'MINUTE'");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[3].Label.ShouldBe("SECOND");
			items[3].Text.ShouldBe("'SECOND'");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestRoundFunctionSpecialParameterCompletion()
		{
			const string statement = @"SELECT ROUND(SYSDATE, 'IW') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 23).ToList();
			items.Count.ShouldBe(11);
			items[0].Label.ShouldBe("CC - One greater than the first two digits of a four-digit year");
			items[0].Text.ShouldBe("'CC'");
			items[0].StatementNode.ShouldNotBe(null);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[10].Label.ShouldBe("YYYY (YEAR) - Year (rounds up on July 1)");
			items[10].Text.ShouldBe("'YYYY'");
			items[10].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[10].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestTruncSpecialParameterCompletionWithNoParameterToken()
		{
			const string statement = @"SELECT TRUNC(SYSDATE, ) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22, true, OracleCodeCompletionCategory.FunctionParameter).ToList();
			items.Count.ShouldBe(11);
			items[0].Label.ShouldBe("CC - One greater than the first two digits of a four-digit year");
			items[0].Text.ShouldBe("'CC'");
			items[0].StatementNode.ShouldBe(null);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[10].Label.ShouldBe("YYYY (YEAR) - Year");
			items[10].Text.ShouldBe("'YYYY'");
			items[10].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[10].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestToCharNlsParameterCompletion()
		{
			const string statement = @"SELECT TO_CHAR(12.34, '9G999D00', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 35).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("NLS_NUMERIC_CHARACTERS = '<decimal separator><group separator>' NLS_CURRENCY = 'currency_symbol' NLS_ISO_CURRENCY = <territory> NLS_DATE_LANGUAGE = <language>");
			items[0].Text.ShouldBe("'NLS_NUMERIC_CHARACTERS = ''<decimal separator><group separator>'' NLS_CURRENCY = ''currency_symbol'' NLS_ISO_CURRENCY = <territory> NLS_DATE_LANGUAGE = <language>'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestDbmsMetadataObjectTypeSpecialParameterCompletion()
		{
			const string statement = @"SELECT dbms_metadata.get_ddl() FROM dual";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 29).ToList();
			items.Count.ShouldBe(76);
			items[0].Label.ShouldBe("AQ_QUEUE");
			items[0].Text.ShouldBe("'AQ_QUEUE'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[72].Label.ShouldBe("XS_USER");
			items[72].Text.ShouldBe("'XS_USER'");
			items[72].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestToCharSpecialParameterCompletionAtIncompatibleParameterIndex()
		{
			const string statement = @"SELECT TO_CHAR('12.34', '9G999D00', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 19).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestToCharFormatParameterCompletion()
		{
			const string statement = @"SELECT TO_CHAR(12.34, '9G999D00', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 30).ToList();
			items.Count.ShouldBe(34);
			items[0].Label.ShouldBe("CC - Century");
			items[0].Text.ShouldBe("'CC'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[33].Label.ShouldStartWith("YYYY-MM-DD\"T\"HH24:MI:SS - XML date time - ");
			items[33].Text.ShouldBe("'YYYY-MM-DD\"T\"HH24:MI:SS'");
			items[33].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}


		[Test]
		public void TestToDateNlsParameterCompletion()
		{
			const string statement = "SELECT TO_DATE('Tue, 24', 'Dy, DD', '') FROM dual";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 37).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("NLS_DATE_LANGUAGE = <language>");
			items[0].Text.ShouldBe("'NLS_DATE_LANGUAGE = <language>'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysContextSpecialParameterCompletion()
		{
			const string statement = @"SELECT SYS_CONTEXT('USERENV', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(59);
			items[0].Label.ShouldBe("ACTION - Identifies the position in the module (application name) and is set through the DBMS_APPLICATION_INFO package or OCI. ");
			items[0].Text.ShouldBe("'ACTION'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[58].Label.ShouldBe("TERMINAL - The operating system identifier for the client of the current session. ");
			items[58].Text.ShouldBe("'TERMINAL'");
			items[58].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysContextUserContextNamespaceCompletion()
		{
			const string statement = @"SELECT SYS_CONTEXT('', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 20).ToList();
			items.Count.ShouldBe(4);
			items[0].Label.ShouldBe("SPECIAL'CONTEXT");
			items[0].Text.ShouldBe("'SPECIAL''CONTEXT'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("TEST_CONTEXT_1");
			items[1].Text.ShouldBe("'TEST_CONTEXT_1'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[2].Label.ShouldBe("TEST_CONTEXT_2");
			items[2].Text.ShouldBe("'TEST_CONTEXT_2'");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[3].Label.ShouldBe("USERENV");
			items[3].Text.ShouldBe("'USERENV'");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysContextUserContextNamespaceCompletionWithEmptyParameterList()
		{
			const string statement = @"SELECT SYS_CONTEXT() FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 19, true, OracleCodeCompletionCategory.FunctionParameter).ToList();
			items.Count.ShouldBe(4);
			items[0].Label.ShouldBe("SPECIAL'CONTEXT");
			items[0].Text.ShouldBe("'SPECIAL''CONTEXT'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("TEST_CONTEXT_1");
			items[1].Text.ShouldBe("'TEST_CONTEXT_1'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[2].Label.ShouldBe("TEST_CONTEXT_2");
			items[2].Text.ShouldBe("'TEST_CONTEXT_2'");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[3].Label.ShouldBe("USERENV");
			items[3].Text.ShouldBe("'USERENV'");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysContextUserContextAttributeCompletion()
		{
			const string statement = @"SELECT SYS_CONTEXT('TEST_context_1', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 38).ToList();
			items.Count.ShouldBe(3);
			items[0].Label.ShouldBe("Special'Attribute'4");
			items[0].Text.ShouldBe("'Special''Attribute''4'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("TestAttribute1");
			items[1].Text.ShouldBe("'TestAttribute1'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[2].Label.ShouldBe("TestAttribute3");
			items[2].Text.ShouldBe("'TestAttribute3'");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysContextUserContextAttributeCompletionWithQuotedIdentifierNamespace()
		{
			const string statement = @"SELECT SYS_CONTEXT(q'|Special'Context|', ) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 41, true, OracleCodeCompletionCategory.FunctionParameter).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("Special'Attribute'5");
			items[0].Text.ShouldBe("'Special''Attribute''5'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysContextSpecialParameterCompletionWithIncompatibleNamespace()
		{
			const string statement = @"SELECT SYS_CONTEXT(X || 'USERENV', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 36).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestConvertSpecialParameterCompletion()
		{
			const string statement = @"SELECT CONVERT('sample text', '', '') FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("US7ASCII");
			items[0].Text.ShouldBe("'US7ASCII'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("WE8ISO8859P1");
			items[1].Text.ShouldBe("'WE8ISO8859P1'");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestBFileNameSpecialParameterCompletion()
		{
			const string statement = @"SELECT bfilename('', 'filename.ext') FROM dual";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 18).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("HUSQVIK.TEST_DIRECTORY_1");
			items[0].Text.ShouldBe("'HUSQVIK.TEST_DIRECTORY_1'");
			items[0].Description.ShouldBe(@"C:\Oracle\product\12.1.0\dbhome_1");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[1].Label.ShouldBe("HUSQVIK.TEST_DIRECTORY_2");
			items[1].Text.ShouldBe("'HUSQVIK.TEST_DIRECTORY_2'");
			items[1].Description.ShouldBe(@"E:\Oracle\ExternalData");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestCryptoHashSpecialParameterCompletion()
		{
			const string statement = @"SELECT DBMS_CRYPTO.HASH(HEXTORAW ('FF'), ) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 41, true, OracleCodeCompletionCategory.FunctionParameter).ToList();
			items.Count.ShouldBe(6);
			items[0].Label.ShouldBe("1 - DBMS_CRYPTO.HASH_MD4 - MD4");
			items[0].Text.ShouldBe("1");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[5].Label.ShouldBe("6 - DBMS_CRYPTO.HASH_SH512 - SH512");
			items[5].Text.ShouldBe("6");
			items[5].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestCryptoHashSpecialParameterWithExistingLiteralCompletion()
		{
			const string statement = @"SELECT DBMS_CRYPTO.HASH(HEXTORAW ('FF'), 1) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 42).ToList();
			items.Count.ShouldBe(6);
			items[0].Label.ShouldBe("1 - DBMS_CRYPTO.HASH_MD4 - MD4");
			items[0].Text.ShouldBe("1");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[5].Label.ShouldBe("6 - DBMS_CRYPTO.HASH_SH512 - SH512");
			items[5].Text.ShouldBe("6");
			items[5].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestTableIdentifierAndAllTableColumnCompletion()
		{
			const string statement = @"SELECT SEL FROM SELECTION, RESPONDENTBUCKET";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			items.Count.ShouldBe(3);
			items[0].Label.ShouldBe("SELECTION.*");
			items[0].Text.ShouldBe("SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, SELECTION.PROJECT_ID, SELECTION.NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.AllColumns);
			items[1].Label.ShouldBe("SELECTION.SELECTION_ID");
			items[1].Text.ShouldBe("SELECTION.SELECTION_ID");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[2].Label.ShouldBe("SELECTION");
			items[2].Text.ShouldBe("SELECTION");
			items[2].CaretOffset.ShouldBe(0);
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test]
		public void TestColumnCodeCompletionWithStatementWithoutQueryBlock()
		{
			const string statement = @"UPDATE SELECTION SET PROJECT_ID = 998 WHERE E";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 45).ToList();
			items.Count.ShouldBe(10);
		}

		[Test]
		public void TestColumnCodeCompletionOfSubqueryMainObjectReference()
		{
			const string statement = @"DELETE (SELECT * FROM SELECTION) WHERE SELECTION_ID = 0";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 41).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("SESSIONTIMEZONE");
			items[0].Text.ShouldBe("SESSIONTIMEZONE");
			items[0].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestSequenceObjectCodeCompletion()
		{
			const string statement = @"SELECT SEQ FROM SELECTION";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].Text.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].CaretOffset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Label.ShouldBe("TEST_SEQ");
			items[1].Text.ShouldBe("TEST_SEQ");
			items[1].CaretOffset.ShouldBe(0);
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test]
		public void TestSchemaTypeCodeCompletion()
		{
			const string statement = @"SELECT XML FROM SELECTION";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("XMLTYPE");
			items[0].Text.ShouldBe("XMLTYPE()");
			items[0].CaretOffset.ShouldBe(-1);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test]
		public void TestSchemaTypeCodeCompletionWithSchemaQualifier()
		{
			const string statement = @"SELECT SYS.XML FROM SELECTION";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 14).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("XMLTYPE");
			items[0].Text.ShouldBe("XMLTYPE()");
			items[0].CaretOffset.ShouldBe(-1);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test]
		public void TestSequenceObjectCodeCompletionInSubquery()
		{
			const string statement = @"SELECT * FROM (SELECT SEQ FROM SELECTION)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 25).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestOtherSchemaObjectCodeCompletionThroughSynonymWithInaccessibleTargetObject()
		{
			const string statement = @"SELECT INACESSIBLE FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 18).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestCaretOffsetWhenTypingSourceRowSource()
		{
			const string statement = @"SELECT * FROM SELECTIO";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22).ToList();
			items.Count.ShouldBe(3);
			items[0].Label.ShouldBe("SELECTION");
			items[0].Text.ShouldBe("SELECTION");
			items[0].CaretOffset.ShouldBe(0);
			items[1].Label.ShouldBe("PUBLIC_SYNONYM_TO_SELECTION");
			items[1].Text.ShouldBe("PUBLIC_SYNONYM_TO_SELECTION");
			items[1].CaretOffset.ShouldBe(0);
			items[2].Label.ShouldBe("SYNONYM_TO_SELECTION");
			items[2].Text.ShouldBe("SYNONYM_TO_SELECTION");
			items[2].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestSequenceSuggestionInInsertValuesClause()
		{
			const string statement = @"INSERT INTO SELECTION (SELECTION_ID) VALUES (SEQ)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 48).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].Text.ShouldBe("SYNONYM_TO_TEST_SEQ");
			items[0].CaretOffset.ShouldBe(0);
			items[1].Label.ShouldBe("TEST_SEQ");
			items[1].Text.ShouldBe("TEST_SEQ");
			items[1].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestSynonymPackageSuggestion()
		{
			const string statement = @"SELECT DBMS_RA FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 14).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("DBMS_RANDOM");
			items[0].Text.ShouldBe("DBMS_RANDOM.");
			items[0].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestSynonymPackageFunctionSuggestion()
		{
			const string statement = @"SELECT DBMS_RANDOM.STR FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("STRING");
			items[0].Text.ShouldBe("STRING()");
			items[0].CaretOffset.ShouldBe(-1);
		}

		[Test]
		public void TestSynonymFunctionSuggestion()
		{
			const string statement = @"SELECT SYNONYM_TO_SQLPAD_FUNC FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 29).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("SYNONYM_TO_SQLPAD_FUNCTION");
			items[0].Text.ShouldBe("SYNONYM_TO_SQLPAD_FUNCTION()");
			items[0].CaretOffset.ShouldBe(-1);
		}

		[Test]
		public void TestPackageSuggestionWhenPackageNameTypedFromMiddle()
		{
			const string statement = @"SELECT RANDOM FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 13, false).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("DBMS_RANDOM");
			items[0].Text.ShouldBe("DBMS_RANDOM.");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Package);
			items[0].CaretOffset.ShouldBe(0);
		}
		
		[Test]
		public void TestIdentifierItemsNotSuggestedWhenInStringLiteral()
		{
			const string statement = @"SELECT 'string FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 13, false).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestColumnIdentifierSuggestionInUpdateSetClause()
		{
			const string statement = @"UPDATE SELECTION SET NAME = 'Dummy name' WHERE SELECTION_ID = 0";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 21).ToList();
			items.Count.ShouldBe(3);
			items[0].Label.ShouldBe("PROJECT_ID");
			items[0].Text.ShouldBe("PROJECT_ID");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[0].CaretOffset.ShouldBe(0);
			items[2].Label.ShouldBe("SELECTION_ID");
			items[2].Text.ShouldBe("SELECTION_ID");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[2].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestColumnIdentifierSuggestionWithoutIdentifier()
		{
			const string statement = @"UPDATE SELECTION SET ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 21).ToList();
			items.Count.ShouldBe(4);
			items[0].Label.ShouldBe("NAME");
			items[0].Text.ShouldBe("NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[0].CaretOffset.ShouldBe(0);
			items[3].Label.ShouldBe("SELECTION_ID");
			items[3].Text.ShouldBe("SELECTION_ID");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[3].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestTableSuggestionWhenTypingUpdateCommand()
		{
			const string statement = @"UPDATE SEL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			AssertTableSuggestionWhenTypingUpdateOrDeleteCommand(items);
		}

		[Test]
		public void TestTableSuggestionWhenTypingDeleteCommand()
		{
			const string statement = @"DELETE SEL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 10).ToList();
			AssertTableSuggestionWhenTypingUpdateOrDeleteCommand(items);
		}

		private static void AssertTableSuggestionWhenTypingUpdateOrDeleteCommand(IReadOnlyList<ICodeCompletionItem> items)
		{
			items.Count.ShouldBe(3);
			items[0].Label.ShouldBe("SELECTION");
			items[0].Text.ShouldBe("SELECTION");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[0].CaretOffset.ShouldBe(0);
			items[1].Label.ShouldBe("PUBLIC_SYNONYM_TO_SELECTION");
			items[1].Text.ShouldBe("PUBLIC_SYNONYM_TO_SELECTION");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].CaretOffset.ShouldBe(0);
			items[2].Label.ShouldBe("SYNONYM_TO_SELECTION");
			items[2].Text.ShouldBe("SYNONYM_TO_SELECTION");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[2].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestTableSuggestionWhenTypingDeleteCommandWithSchema()
		{
			const string statement = @"DELETE HUSQVIK.SEL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 18).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("SELECTION");
			items[0].Text.ShouldBe("SELECTION");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[0].CaretOffset.ShouldBe(0);
			items[1].Label.ShouldBe("SYNONYM_TO_SELECTION");
			items[1].Text.ShouldBe("SYNONYM_TO_SELECTION");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestBuiltInNonSchemaFunctionSuggestion()
		{
			const string statement = @"SELECT LAST_V FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 13).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("LAST_VALUE");
			items[0].Text.ShouldBe("LAST_VALUE() OVER ()");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.BuiltInFunction);
			items[0].CaretOffset.ShouldBe(-9);
		}

		[Test]
		public void TestSuggestionInSubsequentEmptySelectListItem()
		{
			const string statement = @"SELECT DUMMY, , DUMMY FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 14, true, OracleCodeCompletionCategory.Column).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("DUAL.DUMMY");
			items[0].Text.ShouldBe("DUAL.DUMMY");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[0].CaretOffset.ShouldBe(0);
		}

		[Test]
		public void TestColumnSuggestionFromQuotedFullyQualifiedTable()
		{
			const string statement = "SELECT P FROM \"eng\".\"BlacklistPanels\"";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 8).ToList();
			items.Count.ShouldBe(3);
		}

		[Test]
		public void TestSchemaObjectSuggestionInFromClauseBeforeClosingParenthesis()
		{
			const string statement = "SELECT * FROM (SELECT * FROM D)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 30).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestColumnAliasSuggestionInOrderByClause()
		{
			const string statement = "SELECT LENGTH(DUMMY) COLUMN_NAME FROM DUAL ORDER BY C";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 53, false, OracleCodeCompletionCategory.Column).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("COLUMN_NAME");
			items[0].Text.ShouldBe("COLUMN_NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestDirectReferenceColumnAliasSuggestionInOrderByClauseUsingForcedInvocation()
		{
			const string statement = "SELECT DUMMY COLUMN_NAME FROM DUAL ORDER BY ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 44, true, OracleCodeCompletionCategory.Column).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("DUAL.DUMMY");
			items[0].Text.ShouldBe("DUAL.DUMMY");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
			items[1].Label.ShouldBe("COLUMN_NAME");
			items[1].Text.ShouldBe("COLUMN_NAME");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[1].CaretOffset.ShouldBe(0);
			items[1].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestDistinctFunctionSuggestionWhenMultipleOverloadExist()
		{
			const string statement = "SELECT TO_CH FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 12).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("TO_CHAR");
			items[0].Text.ShouldBe("TO_CHAR()");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
			items[0].CaretOffset.ShouldBe(-1);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestFunctionSuggestionWithExistingParameterListWhenCursorIsJustAtOpeningParenthesis()
		{
			const string statement = "SELECT ROUN(1) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 11).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ROUND");
			items[0].Text.ShouldBe("ROUND");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestForcedColumnSuggestionJustAfterWhereKeyword()
		{
			const string statement = "SELECT * FROM DUAL WHERE ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 25).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestForcedColumnSuggestionJustAfterGroupByKeyword()
		{
			const string statement = "SELECT * FROM DUAL GROUP BY ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 28).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestForcedColumnSuggestionJustAfterHavingKeyword()
		{
			const string statement = "SELECT * FROM DUAL GROUP BY 1 HAVING ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 37).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestForcedColumnSuggestionJustAfterOrderByKeyword()
		{
			const string statement = "SELECT * FROM DUAL ORDER BY ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 28).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestForcedObjectSuggestionAfterSchemaAndDotInFromClause()
		{
			const string statement = "SELECT * FROM HUSQVIK.";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestNoSuggestionAvailableJustAfterAsterisk()
		{
			const string statement = "SELECT * FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 8).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestNoSuggestionAvailableWhenTypingColumnAlias()
		{
			const string statement = "SELECT DUMMY D, DUMMY FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 14).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestSuggestionAvailableAtAsteriskStartIndex()
		{
			const string statement = "SELECT * FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 7).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestNodeToReplaceWhenTypingWhereCondition()
		{
			const string statement = "SELECT DUMMY FROM DUAL WHERE D";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 30).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestNameBasedJoinConditionSuggestionWhenChainedJoinClauseAlreadyExists()
		{
			const string statement = "SELECT * FROM DUAL D1 JOIN DUAL  JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 32).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ON D1.DUMMY = DUAL.DUMMY");
			items[0].Text.ShouldBe("ON D1.DUMMY = DUAL.DUMMY");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinConditionByName);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestChildToParentForeignKeyBasedJoinConditionSuggestionWhenChainedJoinClauseAlreadyExists()
		{
			const string statement = "SELECT * FROM SELECTION S JOIN RESPONDENTBUCKET  JOIN TARGETGROUP ON RB.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 48).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ON S.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID");
			items[0].Text.ShouldBe("ON S.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinConditionByReferenceConstraint);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestParentToChildForeignKeyBasedJoinConditionSuggestionWhenChainedJoinClauseAlreadyExists()
		{
			const string statement = "SELECT * FROM RESPONDENTBUCKET RB JOIN SELECTION  JOIN TARGETGROUP ON RB.TARGETGROUP_ID = TARGETGROUP.TARGETGROUP_ID";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 49).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ON RB.RESPONDENTBUCKET_ID = SELECTION.RESPONDENTBUCKET_ID");
			items[0].Text.ShouldBe("ON RB.RESPONDENTBUCKET_ID = SELECTION.RESPONDENTBUCKET_ID");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinConditionByReferenceConstraint);
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestFunctionsNotDuplicatedWhenSuggested()
		{
			const string statement = "SELECT UNCOMPILABLE_F FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 21).ToList();
			items.Count.ShouldBe(1);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaFunction);
		}

		[Test]
		public void TestFunctionSuggestionWhenTypingWithinSameColumnBeforeExistingExpression()
		{
			const string statement = "SELECT ROUN DBMS_RANDOM.VALUE FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 11).ToList();
			items.Count.ShouldBe(1);
		}

		[Test]
		public void TestSchemaFunctionsNotSuggestedWhenSuggestingPackageFunctions()
		{
			const string statement = "SELECT DBMS_RANDOM.NORMAL FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 19, true, OracleCodeCompletionCategory.SchemaFunction, OracleCodeCompletionCategory.Package).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestDbmsRandomStringSpecialParameterCompletion()
		{
			const string statement = @"SELECT DBMS_RANDOM.STRING() FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 26, true, OracleCodeCompletionCategory.FunctionParameter).ToList();
			items.Count.ShouldBe(5);
			items[0].Label.ShouldBe("A (a) - mixed case alpha characters");
			items[0].Text.ShouldBe("'A'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[4].Label.ShouldBe("X (x) - uppercase alpha-numeric characters");
			items[4].Text.ShouldBe("'X'");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestDbmsRandomStringSpecialParameterCompletionWithNamedParameter()
		{
			const string statement = @"SELECT dbms_random.string(len => 16, opt => 'X') FROM dual";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 45, true, OracleCodeCompletionCategory.FunctionParameter).ToList();
			items.Count.ShouldBe(5);
			items[0].Label.ShouldBe("A (a) - mixed case alpha characters");
			items[0].Text.ShouldBe("'A'");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[4].Label.ShouldBe("X (x) - uppercase alpha-numeric characters");
			items[4].Text.ShouldBe("'X'");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
		}

		[Test]
		public void TestSysDateFunctionAsReservedWord()
		{
			const string statement = @"SELECT ROWNU FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 12).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ROWNUM");
			items[0].Text.ShouldBe("ROWNUM");
			items[0].CaretOffset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.PackageFunction);
		}

		[Test]
		public void TestRowIdCodeCompletionWhenOnlyChoice()
		{
			const string statement = @"SELECT DUAL.ROWI FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 16).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("ROWID");
			items[0].Text.ShouldBe("ROWID");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Pseudocolumn);
		}

		[Test]
		public void TestKeywordCompletion()
		{
			const string statement = @"SELECT * FROM DUAL ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 19, true, OracleCodeCompletionCategory.Keyword).ToList();
			items.Count.ShouldBe(8);
			items[0].Label.ShouldBe("CONNECT BY");
			items[1].Label.ShouldBe("GROUP BY");
			items[2].Label.ShouldBe("HAVING");
			items[3].Label.ShouldBe("INTERSECT");
			items[4].Label.ShouldBe("MINUS");
			items[5].Label.ShouldBe("ORDER BY");
			items[6].Label.ShouldBe("UNION");
			items[7].Label.ShouldBe("WHERE");

			items.ForEach(i => i.StatementNode.ShouldBe(null));
		}

		[Test]
		public void TestKeywordCompletionAfterGroupByClause()
		{
			const string statement = @"SELECT * FROM DUAL GROUP BY 1 ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 30, true, OracleCodeCompletionCategory.Keyword).ToList();
			items.Count.ShouldBe(5);
			items[0].Label.ShouldBe("HAVING");
			items[1].Label.ShouldBe("INTERSECT");
			items[2].Label.ShouldBe("MINUS");
			items[3].Label.ShouldBe("ORDER BY");
			items[4].Label.ShouldBe("UNION");

			items.ForEach(i => i.StatementNode.ShouldBe(null));
		}

		[Test]
		public void TestKeywordCompletionAfterWhereClauseWhemTyping()
		{
			const string statement = @"SELECT * FROM DUAL WHERE 1 = 1 GR";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 33, false, OracleCodeCompletionCategory.Keyword).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("GROUP BY");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestKeywordCompletionWhenKeywordAlreadyInPlace()
		{
			const string statement = @"SELECT * FROM DUAL ORDER BY 1";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22, false, OracleCodeCompletionCategory.Keyword).ToList();
			items.Count.ShouldBe(0);
		}
		
		[Test]
		public void TestKeywordCompletionInAnalyticClause()
		{
			const string statement = @"SELECT COUNT(*) OVER (P) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 23, false, OracleCodeCompletionCategory.Keyword).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("PARTITION BY");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestKeywordCompletionInAnalyticClauseAfterUnparsedToken()
		{
			const string statement = @"SELECT COUNT(*) OVER (PART ) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 27, true, OracleCodeCompletionCategory.Keyword).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestCompletionNodeToReplaceWhenSuggestingAsterisk()
		{
			const string statement = @"SELECT SELECTION. FROM SELECTION";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 17, true, OracleCodeCompletionCategory.AllColumns).ToList();
			items.Count.ShouldBe(1);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestCompletionNodeToReplaceJustAfterDotAfterObjectQualifierWhenColumnStartsNotAtCaret()
		{
			const string statement = @"SELECT SELECTION. CREATED FROM SELECTION";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 17).ToList();
			items.Count.ShouldBeGreaterThan(5);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestSequencePseudocolumnSuggestion()
		{
			const string statement = @"SELECT TEST_SEQ.N FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 17).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("NEXTVAL");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Pseudocolumn);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestSequencePseudocolumnSuggestionRightAfterDot()
		{
			const string statement = @"SELECT TEST_SEQ. FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 16).ToList();
			items.Count.ShouldBe(2);
			items[0].Label.ShouldBe("CURRVAL");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Pseudocolumn);
			items[0].StatementNode.ShouldBe(null);
			items[1].Label.ShouldBe("NEXTVAL");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Pseudocolumn);
			items[1].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestSequencePseudocolumnSuggestionWhenAlreadyInPlace()
		{
			const string statement = @"SELECT TEST_SEQ.NEXTVAL FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 17).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestCodeCompletionWhenInvokedAfterSet()
		{
			const string statement = @"UPDATE HUSQVIK.SELECTION SET ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 29).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestTableReferenceColumnCompletionWhenColumnExistsButInaccessible()
		{
			const string statement =
@"SELECT
	*
FROM
	XMLTABLE('/root' PASSING XML_DAT)
	CROSS JOIN
		(SELECT XMLTYPE('<root>value</root>') XML_DATA FROM DUAL)";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 51).ToList();
			items.Count.ShouldBe(0);
		}

		[Test]
		public void TestObjectSuggestionWhenUsingUnfinishedQuotedIdentifier()
		{
			const string testQuery = "SELECT * FROM \"CaseSensitive";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 28).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("\"CaseSensitiveTable\"");
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestSchemaQualifiedObjectSuggestionWhenUsingUnfinishedQuotedIdentifier()
		{
			const string testQuery = "SELECT * FROM HUSQVIK.\"CaseSensitive";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 36).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("\"CaseSensitiveTable\"");
			items[0].Text.ShouldBe("\"CaseSensitiveTable\"");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestColumnSuggestionWhenUsingUnfinishedQuotedIdentifier()
		{
			const string testQuery = "SELECT * FROM \"CaseSensitiveTable\" WHERE \"CaseSensitiveCol";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 58).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("\"CaseSensitiveTable\".\"CaseSensitiveColumn\"");
			items[0].Text.ShouldBe("\"CaseSensitiveTable\".\"CaseSensitiveColumn\"");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestKeywordSuggestionJustBeforeClosingParenthesis()
		{
			const string testQuery = "SELECT COUNT(D) FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 14, false, OracleCodeCompletionCategory.Keyword).ToArray();
			items.Length.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldNotBe(null);
			items[0].StatementNode.Token.Value.ShouldBe("D");
		}

		[Test]
		public void TestKeywordSuggestionJustBeforeClosingParenthesisInWhereClause()
		{
			const string testQuery = "SELECT * FROM (SELECT * FROM DUAL WH)";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 36, true, OracleCodeCompletionCategory.Keyword).ToArray();
			items.Length.ShouldBe(1);
			items[0].Text.ShouldBe("WHERE");
			items[0].StatementNode.ShouldNotBe(null);
			items[0].StatementNode.Token.Value.ShouldBe("WH");
		}

		[Test]
		public void TestColumnSuggestionAtReservedWordRepresentingFunction()
		{
			const string testQuery = "SELECT USER FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 10).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestSuggestionAfterOpeningQuoteInOrderByClause()
		{
			const string testQuery = "SELECT * FROM DUAL ORDER BY \"";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 29).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestForcedSuggestionWhenChainingCondition()
		{
			const string testQuery = "SELECT * FROM V$SESSION WHERE SID = 72 AND ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 43).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestJoinConditionSuggestionBeforeClosingParenthesis()
		{
			const string testQuery = @"SELECT NULL FROM DUAL WHERE EXISTS (SELECT NULL FROM DUAL T1 JOIN DUAL T2 )";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 74).ToArray();
			items.Length.ShouldBe(1);
		}

		[Test]
		public void TestCommonTableExpressionSuggestionWhenContainsConcatenatedSuquery()
		{
			const string testQuery =
@"WITH CTE1 AS (
	SELECT 1, 'W', 3 FROM DUAL UNION ALL
	SELECT 2, 'N', 4 FROM DUAL UNION ALL
	SELECT 3, 'N', 8 FROM DUAL
)
SELECT * FROM CTE";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 143).ToArray();
			items.Length.ShouldBe(1);
		}

		[Test]
		public void TestDataObjectSuggestionWhenNameContainsReservedWord()
		{
			const string testQuery =
@"WITH ALL_DATA AS (
	SELECT * FROM DUAL
)
SELECT * FROM ALL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 61).ToArray();
			items.Length.ShouldBe(2);
			items[0].Label.ShouldBe("ALL_DATA");
			items[0].StatementNode.ShouldNotBe(null);
			items[1].Label.ShouldBe("ALL_TABLES");
			items[1].StatementNode.ShouldNotBe(null);
		}

		[Test, Ignore("difficult to implement")]
		public void TestFunctionSuggestionBeforeStringParameter()
		{
			const string testQuery = @"SELECT DBMS_CRYPTO.HASH(HEXTO '', 1) FROM DUAL";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 29).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("HEXTORAW");
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestJoinConditionSuggestionWithNonAliasedInlineView()
		{
			const string testQuery = @"SELECT * FROM SELECTION LEFT JOIN (SELECT 1 SELECTION_ID FROM DUAL) ON ";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, testQuery, 71, categories: new[] { OracleCodeCompletionCategory.JoinConditionByName }).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestCodeCompletionForExplicitPartition()
		{
			const string statement = @"SELECT * FROM INVOICES PARTITION (P)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 35).ToArray();
			items.Length.ShouldBe(2);
			items[0].Label.ShouldBe("P2014");
			items[0].StatementNode.ShouldNotBe(null);
			items[0].StatementNode.Token.Value.ShouldBe("P");
			items[1].Label.ShouldBe("P2015");
			items[1].StatementNode.ShouldNotBe(null);
			items[1].StatementNode.Token.Value.ShouldBe("P");
		}

		[Test]
		public void TestCodeCompletionForExplicitSubPartition()
		{
			const string statement = @"SELECT * FROM INVOICES SUBPARTITION ()";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 37).ToArray();
			items.Length.ShouldBe(4);
			items[0].Label.ShouldBe("P2014_ENTERPRISE");
			items[0].StatementNode.ShouldBe(null);
			items[1].Label.ShouldBe("P2014_PRIVATE");
			items[1].StatementNode.ShouldBe(null);
			items[2].Label.ShouldBe("P2015_ENTERPRISE");
			items[2].StatementNode.ShouldBe(null);
			items[3].Label.ShouldBe("P2015_PRIVATE");
			items[3].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestCodeCompletionWithSchemaQualification()
		{
			const string statement = @"SELECT * FROM HUSQVIK.";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 22).ToArray();
			items.Length.ShouldBeGreaterThan(0);
			items[0].StatementNode.ShouldBe(null);
			items[0].InsertOffset.ShouldBe(0);
		}

		[Test]
		public void TestDataTypeCodeCompletion()
		{
			const string statement = @"SELECT CAST(NULL AS V) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 21).ToArray();
			items.Length.ShouldBe(6);
			items[0].StatementNode.ShouldNotBe(null);
			items[0].Text.ShouldBe("HUSQVIK.INVALID_OBJECT_TYPE");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.DataType);
			items[0].InsertOffset.ShouldBe(0);
			items[4].StatementNode.ShouldNotBe(null);
			items[4].Label.ShouldBe("VARCHAR2");
			items[4].Text.ShouldBe("VARCHAR2()");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.DataType);
			items[4].InsertOffset.ShouldBe(0);
			items[5].StatementNode.ShouldNotBe(null);
			items[5].Text.ShouldBe("HUSQVIK");
			items[5].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
		}

		[Test]
		public void TestExtractElementCompletion()
		{
			const string statement = @"SELECT EXTRACT( FROM SYSDATE) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 15).ToArray();
			items.Length.ShouldBe(10);
			items[0].StatementNode.ShouldBe(null);
			items[0].Text.ShouldBe("DAY");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Keyword);
			items[9].StatementNode.ShouldBe(null);
			items[9].Text.ShouldBe("YEAR");
			items[9].Category.ShouldBe(OracleCodeCompletionCategory.Keyword);
		}

		[Test]
		public void TestExtractElementCompletionWithExistingToken()
		{
			const string statement = @"SELECT EXTRACT(DAY FROM SYSDATE) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 15).ToArray();
			items.Length.ShouldBe(9);
			items[0].StatementNode.ShouldNotBe(null);
			items[0].Text.ShouldBe("HOUR");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Keyword);
			items[8].StatementNode.ShouldNotBe(null);
			items[8].Text.ShouldBe("YEAR");
			items[8].Category.ShouldBe(OracleCodeCompletionCategory.Keyword);
		}

		[Test]
		public void TestExtractFunctionCodeCompletion()
		{
			const string statement = @"SELECT EXTRAC FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 13).ToArray();
			items.Length.ShouldBe(1);
			items[0].StatementNode.ShouldNotBe(null);
			items[0].Label.ShouldBe("EXTRACT");
			items[0].Text.ShouldBe("EXTRACT(DAY FROM )");
			items[0].CaretOffset.ShouldBe(-1);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.BuiltInFunction);
		}

		[Test]
		public void TestQuotedBindVariableCodeCompletionWhileTyping()
		{
			const string statement = @"SELECT NULL FROM DUAL WHERE DUMMY = (SELECT NULL FROM :""""";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 57).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test]
		public void TestIntervalCodeCompletion()
		{
			const string statement = @"SELECT CAST(NULL AS INTERVAL DAY TO ) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 36).ToArray();
			items.Length.ShouldBeGreaterThan(0);
			items[0].Label.ShouldBe("DAY");
			items[0].CaretOffset.ShouldBe(0);
			items[0].StatementNode.ShouldBe(null);
		}

		[Test]
		public void TestAggregateMaxFunctionSuggestion()
		{
			const string statement = @"SELECT MA FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 9).ToArray();
			items.Length.ShouldBe(1);
			items[0].Label.ShouldBe("MAX");
			items[0].Text.ShouldBe("MAX()");
			items[0].CaretOffset.ShouldBe(-1);
			items[0].StatementNode.ShouldNotBe(null);
		}

		[Test]
		public void TestCorrelatedSubqueryColumnSuggestion()
		{
			const string statement = @"SELECT * FROM SELECTION WHERE NOT EXISTS (SELECT NULL FROM RESPONDENTBUCKET WHERE RESPONDENTBUCKET_ID = SELECTION.R)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 115).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestSuggestionInUpdateSubquery()
		{
			const string statement = @"UPDATE (SELECT DUAL.DUMMY FROM DUAL JOIN DUAL TARGET ON D)";
			Should.NotThrow(() => CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 57));
		}

		[Test]
		public void TestSuggestionInMergeSubquery()
		{
			const string statement =
@"MERGE INTO EVENTS
USING (SELECT :EVENT_ID, COUNTER FROM DUAL LEFT JOIN EVENTS ON E) SRC
ON (EVENTS.ID = SRC.ID)";
			Should.NotThrow(() => CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 83));
		}

		[Test]
		public void TestCorrelatedSubqueryColumnSuggestionWhenSameTableInBothSubqueries()
		{
			const string statement = @"SELECT * FROM SELECTION WHERE EXISTS (SELECT NULL FROM SELECTION WHERE SELECTION.)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 81).ToArray();
			items.Length.ShouldBe(6);
			items[0].Label.ShouldBe("NAME");
			items[1].Label.ShouldBe("ORA_ROWSCN");
			items[2].Label.ShouldBe("PROJECT_ID");
			items[3].Label.ShouldBe("RESPONDENTBUCKET_ID");
			items[4].Label.ShouldBe("ROWID");
			items[5].Label.ShouldBe("SELECTION_ID");
		}

		[Test]
		public void TestSuggestionAfterColonStartingBindVariable()
		{
			const string statement = @"SELECT COUNT(*) FROM SELECTION WHERE SELECTION_ID = :";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 53).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items.ForEach(i => i.Category.ShouldBe(OracleCodeCompletionCategory.BindVariable));
		}

		[Test]
		public void TestSuggestionAfterColonStartingBindVariableBeforeClosingParenthesis()
		{
			const string statement = @"SELECT TO_DATE(:) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 16).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items.ForEach(i => i.Category.ShouldBe(OracleCodeCompletionCategory.BindVariable));
		}

		[Test]
		public void TestNextDaySpecialParameterCompletion()
		{
			const string statement = @"SELECT NEXT_DAY(SYSDATE, ) FROM DUAL";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 25, categories: new[] { OracleCodeCompletionCategory.FunctionParameter }).ToArray();
			items.Length.ShouldBe(7);
			items[0].Label.ShouldBe("Friday");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.FunctionParameter);
			items[0].Text.ShouldBe("'Friday'");
			items[1].Label.ShouldBe("Monday");
			items[2].Label.ShouldBe("Saturday");
			items[3].Label.ShouldBe("Sunday");
			items[4].Label.ShouldBe("Thursday");
			items[5].Label.ShouldBe("Tuesday");
			items[6].Label.ShouldBe("Wednesday");
		}

		[Test]
		public void TestDatabaseLinkSuggestionWhenUnfinishedQuotedIdentifier()
		{
			const string statement = @"SELECT * FROM ""Respondent""@""HQ_PDB_LOOPBACK";
			Should.NotThrow(() => CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 43));
		}

		[Test]
		public void TestJoinCodeCompletionAtClosingParenthesis()
		{
			const string statement = @"SELECT NULL FROM (SELECT NULL FROM RESPONDENTBUCKET J)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 53, true, OracleCodeCompletionCategory.JoinMethod).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestColumnCodeCompletionWhenStartingChainedSetClause()
		{
			const string statement = @"UPDATE DUAL SET DUMMY = NULL, ";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 30, true, OracleCodeCompletionCategory.Column).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestColumnCodeCompletionInIntoColumnList()
		{
			const string statement = @"INSERT INTO DUAL (D";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 19, true, OracleCodeCompletionCategory.Column).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestColumnAliasSuggestionInPartitionByClauseInOrderByClause()
		{
			const string statement = @"SELECT COUNT(DUMMY) DUMMY FROM DUAL ORDER BY COUNT(DUAL.DUMMY) OVER (ORDER BY D)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 79, true, OracleCodeCompletionCategory.Column).ToList();
			items.Count.ShouldBeGreaterThan(0);
		}

		[Test]
		public void TestJoinTypeSuggestingWithCursorAtClosingParenthesis()
		{
			const string statement = @"(SELECT count(*) FROM dual JO)";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 29).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items[0].StatementNode.FirstTerminalNode.Token.Value.ShouldBe("JO");
		}

		[Test]
		public void TestAsteriskSuggestionWithInvisibleColumns()
		{
			const string statement = @"SELECT T. FROM ""CaseSensitiveTable"" T";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 9).ToList();
			items.Count.ShouldBeGreaterThan(0);
			items[0].Text.ShouldBe("\"CaseSensitiveColumn\", T.VIRTUAL_COLUMN");
		}

		[Test]
		public void TestFunctionSuggestionRepresentedByKeyword()
		{
			OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier = FormatOption.Lower;
			OracleConfiguration.Configuration.Formatter.FormatOptions.ReservedWord = FormatOption.Upper;

			const string statement = "SELECT NULL FROM dual WHERE exi";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 31).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("EXISTS");
			items[0].Text.ShouldBe("EXISTS");
		}

		[Test]
		public void TestOrderBySuggestionFormat()
		{
			OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier = FormatOption.Lower;

			const string statement = "SELECT NULL TEST_COLUMN FROM dual ORDER BY TEST_COLU";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 52).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("test_column");
			items[0].Text.ShouldBe("test_column");
		}

		[Test]
		public void TestCodeCompletionAtDotAfterSchemaIdentifier()
		{
			const string statement = @"SELECT NULL FROM husqvi.selection";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 23).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("HUSQVIK");
			items[0].Text.ShouldBe("HUSQVIK");
		}

		[Test]
		public void TestParenthesisNotSuggestedWhenAlreadyInPlace()
		{
			const string statement = @"BEGIN dbms_output.put_lin(); END;";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 25).ToList();
			items.Count.ShouldBe(1);
			items[0].Label.ShouldBe("PUT_LINE");
			items[0].Text.ShouldBe("PUT_LINE");
		}

		[Test, Ignore("not solved yet; looks like somethings goes really wrong when building grammar tree although it requires document repository to replicate this issue. ")]
		public void TestSpecialCrashingCaseWithinTerminalCandidates()
		{
			const string statement = "SELECT row_number() OVER (PARTITION BY dummy, d, dummy ORDER BY NULL) FROM dual";
			Should.NotThrow(() => CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 47));
		}

		[Test]
		public void TestRegexModifierParameterSuggestion()
		{
			const string statement = @"SELECT regexp_replace (null, null, null, 1, 0, '') FROM dual";
			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 48).ToList();
			items.Count.ShouldBe(5);
			items[0].Label.ShouldBe("c - specifies case-sensitive matching");
			items[1].Label.ShouldBe("i - specifies case-insensitive matching");
			items[2].Label.ShouldBe("m - treats the source string as multiple lines. Oracle interprets the caret (^) and dollar sign ($) as the start and end, respectively, of any line anywhere in the source string, rather than only at the start or end of the entire source string. If you omit this parameter, then Oracle treats the source string as a single line. ");
			items[3].Label.ShouldBe("n - allows the period (.), which is the match-any-character character, to match the newline character. If you omit this parameter, then the period does not match the newline character. ");
			items[4].Label.ShouldBe("x - ignores whitespace characters. By default, whitespace characters match themselves. ");
		}

		[Test]
		public void TestCodeCompletionWithPublicSynonymHavingSameNameAsCurrentSchemaObject()
		{
			const string statement = @"SELECT * FROM all_table";

			var databaseModel = new OracleTestDatabaseModel { CurrentSchema = OracleObjectIdentifier.SchemaSys };
			var items = CodeCompletionProvider.ResolveItems(databaseModel, statement, 23);
			items.Count.ShouldBe(1);
		}

		[Test]
		public void TestCodeCompletionGatherTableStatsOwnerName()
		{
			const string statement = @"BEGIN dbms_stats.gather_table_stats(ownname => '', tabname => ''); END;";

			var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 48);
			items.Count.ShouldBe(TestFixture.DatabaseModel.Schemas.Count);
		}

		public class OracleCodeCompletionTypeTest
		{
			private static OracleCodeCompletionType InitializeCodeCompletionType(string statementText, int cursorPosition)
			{
				var documentRepository = TestFixture.CreateDocumentRepository();
				documentRepository.UpdateStatements(statementText);
				return new OracleCodeCompletionType(documentRepository, statementText, cursorPosition);
			}

			[Test]
			public void TestCodeCompletionTypeAfterOnTerminalWithinJoinCondition()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
				var completionType = InitializeCodeCompletionType(statement, statement.Length);
				completionType.JoinCondition.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test, Ignore("not solved yet")]
			public void TestCodeCompletionTypeAfterExistingConditionInJoinClause()
			{
				const string statement = @"SELECT * FROM SELECTION JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET AND ";
				var completionType = InitializeCodeCompletionType(statement, statement.Length);
				completionType.JoinCondition.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeAtTheEndOnTerminalWithinJoinCondition()
			{
				const string statement = @"SELECT CUSTOMER. FROM CUSTOMER JOIN COMPANY ON ";
				var completionType = InitializeCodeCompletionType(statement, statement.Length - 1);
				completionType.JoinCondition.ShouldBe(false);
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
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
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(true);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
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
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(true);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
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
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}
			
			[Test]
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
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
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
				completionType.ColumnAlias.ShouldBe(false);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeWhenTypingColumnAliasInOrderByClause()
			{
				const string statement = @"SELECT LENGTH(DUMMY) COLUMN_NAME FROM DUAL ORDER BY C";
				var completionType = InitializeCodeCompletionType(statement, 53);
				completionType.ColumnAlias.ShouldBe(true);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeInFromClauseInCorrelatedSubquery()
			{
				const string statement = @"SELECT * FROM SELECTION WHERE SELECTIONNAME IN (SELECT * FROM SELE)";
				var completionType = InitializeCodeCompletionType(statement, 66);
				completionType.SchemaDataObject.ShouldBe(true);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeWhenWritingChainedJoinClause()
			{
				const string statement = @"SELECT * FROM SELECTION JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID J";
				var completionType = InitializeCodeCompletionType(statement, 119);
				completionType.JoinType.ShouldBe(true);
				completionType.SpecialFunctionParameter.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeWithMissingSelectList()
			{
				const string statement = @"SELECT FROM V$TRANSACTION";
				InitializeCodeCompletionType(statement, 7);
			}

			[Test]
			public void TestCodeCompletionTypeWhenWritingInsertIntoTarget()
			{
				const string statement = @"INSERT INTO S";
				var completionType = InitializeCodeCompletionType(statement, 13);
				completionType.SchemaDataObject.ShouldBe(true);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeWhenInvokedAfterInsertInto()
			{
				const string statement = @"INSERT INTO ";
				var completionType = InitializeCodeCompletionType(statement, 12);
				completionType.SchemaDataObject.ShouldBe(true);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestKeywordClauseCodeCompletionTypeWhenInvokedWithinNonAggregateFunction()
			{
				const string statement = @"SELECT NVL() FROM DUAL";
				var completionType = InitializeCodeCompletionType(statement, 11);
				completionType.KeywordsClauses.Count.ShouldBe(0);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestKeywordClauseCodeCompletionTypeWhenTypingSelectDistinct()
			{
				const string statement = @"SELECT DIS";
				var completionType = InitializeCodeCompletionType(statement, 10);
				completionType.KeywordsClauses.Count.ShouldBe(2);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeForExplicitPartition()
			{
				const string statement = @"SELECT * FROM INVOICES PARTITION (P)";
				var completionType = InitializeCodeCompletionType(statement, 35);
				completionType.ExplicitPartition.ShouldBe(true);
				completionType.ExplicitSubPartition.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeForExplicitSubPartition()
			{
				const string statement = @"SELECT * FROM INVOICES SUBPARTITION ()";
				var completionType = InitializeCodeCompletionType(statement, 37);
				completionType.ExplicitPartition.ShouldBe(false);
				completionType.ExplicitSubPartition.ShouldBe(true);
				completionType.SchemaDataObject.ShouldBe(false);
				completionType.JoinType.ShouldBe(false);
				completionType.DataType.ShouldBe(false);
			}

			[Test]
			public void TestCodeCompletionTypeForDataType()
			{
				const string statement = @"SELECT CAST(NULL AS ) FROM DUAL";
				var completionType = InitializeCodeCompletionType(statement, 20);
				completionType.DataType.ShouldBe(true);
			}

			[Test]
			public void TestCodeCompletionTypeForDataTypeWithExistingToken()
			{
				const string statement = @"SELECT CAST(NULL AS NUMBER) FROM DUAL";
				var completionType = InitializeCodeCompletionType(statement, 23);
				completionType.DataType.ShouldBe(true);
			}

			[Test]
			public void TestCodeCompletionTypeInFromClauseInSubqueryWithinJoinClause()
			{
				const string statement = @"SELECT NULL FROM SELECTION S1 JOIN SELECTION S2 ON S1.SELECTION_ID = S2.SELECTION_ID AND S2.PROJECT_ID >= (SELECT MIN(PROJECT_ID) FROM )";
				var completionType = InitializeCodeCompletionType(statement, 135);
				completionType.SchemaDataObject.ShouldBe(true);
			}

			[Test]
			public void TestCodeCompletionTypeAtDotAfterSchemaIdentifier()
			{
				const string statement = @"SELECT NULL FROM husqvi.selection";
				var completionType = InitializeCodeCompletionType(statement, 23);
				completionType.Schema.ShouldBe(true);
			}

			[Test]
			public void TestCodeCompletionTypeAtBeginningOfCreatePackage()
			{
				const string statement =
					@"CREATE OR REPLACE PACKAGE test_package IS
    PROCEDURE test_procedure;
END;";

				Should.NotThrow(() => InitializeCodeCompletionType(statement, 0));
			}

			[Test]
			public void TestCodeCompletionTypeAtOpeningParenthesis()
			{
				const string statement = @"BEGIN dbms_output.put_lin(); END;";

				var completionType = InitializeCodeCompletionType(statement, 25);
				completionType.PackageFunction.ShouldBe(true);
			}

			[Test]
			public void TestCodeCompletionTypeAtEmptyInsertColumnList()
			{
				const string statement = @"INSERT INTO selection ()";

				var completionType = InitializeCodeCompletionType(statement, 23);
				completionType.InsertIntoColumn.ShouldBe(true);
			}

			public class ReferenceIdentifierTest
			{
				public class SelectList
				{
					[Test]
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

					[Test]
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

					[Test]
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

					[Test]
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

					[Test]
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

					[Test]
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

					[Test]
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
					[Test]
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

					[Test]
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

					[Test]
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

					[Test]
					public void TestTableCollectionExpressionReferenceIdentifiers()
					{
						const string statement = @"SELECT column_value FROM TABLE (SYS.ODCI)";
						var referenceIdentifier = InitializeCodeCompletionType(statement, 39).ReferenceIdentifier;
						referenceIdentifier.CursorPosition.ShouldBe(39);
						referenceIdentifier.SchemaIdentifierOriginalValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierOriginalValue.ShouldBe("SYS");
						referenceIdentifier.IdentifierOriginalValue.ShouldBe("ODCI");
						referenceIdentifier.SchemaIdentifierEffectiveValue.ShouldBe(null);
						referenceIdentifier.ObjectIdentifierEffectiveValue.ShouldBe("SYS");
						referenceIdentifier.IdentifierEffectiveValue.ShouldBe("ODC");
					}
				}
			}
		}

		public class PlSql
		{
			[Test]
			public void TestBodyStatementWhileTyping()
			{
				const string statement = "DECLARE PROCEDURE P1(P1 NUMBER) IS BEGIN NULL; END; BEGIN N; END;";
				var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 59).ToList();
				items.Count.ShouldBe(0);
			}

			[Test]
			public void TestPackageCodeCompletion()
			{
				const string statement = "BEGIN OUTPUT END;";
				var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 12, false).ToList();
				items.Count.ShouldBe(1);
				items[0].Label.ShouldBe("DBMS_OUTPUT");
				items[0].StatementNode.ShouldNotBe(null);
			}

			[Test]
			public void TestPackageCodeCompletionWithStatementTerminator()
			{
				const string statement = "BEGIN OUTPUT; END;";
				var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 12, false).ToList();
				items.Count.ShouldBe(1);
				items[0].Label.ShouldBe("DBMS_OUTPUT");
				items[0].StatementNode.ShouldNotBe(null);
			}

			[Test]
			public void TestPackageProcedureCodeCompletionWithStatementTerminator()
			{
				const string statement = "BEGIN SYS.DBMS_OUTPUT.PUT_L; END;";
				var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 27, false).ToList();
				items.Count.ShouldBe(1);
				items[0].Label.ShouldBe("PUT_LINE");
				items[0].StatementNode.ShouldNotBe(null);
			}

			[Test]
			public void TestFromObjectCompletionInPlSqlBeforeCommandTerminator()
			{
				const string statement =
@"CREATE PROCEDURE test_procedure (p OUT SYS_REFCURSOR)
IS
BEGIN
  OPEN p FOR SELECT * FROM selectio;
END;";

				var items = CodeCompletionProvider.ResolveItems(TestFixture.DatabaseModel, statement, 101).ToList();
				items.Count.ShouldBe(3);
				items[0].Label.ShouldBe("SELECTION");
				items[0].StatementNode.ShouldNotBe(null);
				items[0].StatementNode.Token.Value.ShouldBe("selectio");
			}
		}
	}
}
