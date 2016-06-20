using System;
using System.Linq;
using System.Threading;
using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.Test.Commands
{
	[TestFixture]
	public class CommandTest
	{
		private readonly OracleTestCommandSettingsProviderFactory _commandSettingsProviderFactory = new OracleTestCommandSettingsProviderFactory();

		private SqlDocumentRepository _documentRepository;
		private SqlTextEditor _editor;

		[SetUp]
		public void SetUp()
		{
			_editor = new SqlTextEditor();
			_documentRepository = TestFixture.CreateDocumentRepository();
			OracleConfiguration.Configuration.Formatter.FormatOptions.Reset();
		}

		private ActionExecutionContext CreateExecutionContext()
		{
			_documentRepository.UpdateStatements(_editor.Text);
			return ActionExecutionContext.Create(_editor, _documentRepository);
		}

		private bool CanExecuteCommand(CommandExecutionHandler executionHandler)
		{
			var executionContext = CreateExecutionContext();
			return executionHandler.CanExecuteHandler(executionContext);
		}

		private void ExecuteCommand(CommandExecutionHandler executionHandler, ICommandSettingsProvider commandSettings = null)
		{
			var executionContext = CreateExecutionContext();
			executionContext.SettingsProvider = commandSettings;
			ExecuteCommand(executionHandler, executionContext);
		}

		private void ExecuteCommand(CommandExecutionHandler executionHandler, ActionExecutionContext executionContext)
		{
			executionHandler.ExecutionHandler(executionContext);
			GenericCommandHandler.UpdateDocument(_editor, executionContext);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddObjectAliasCommand()
		{
			_editor.Text = @"SELECT SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION";
			_editor.CaretOffset = 87;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "S"} ));

			_editor.Text.ShouldBe(@"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION S");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddObjectAliasWithExistingModelClause()
		{
			_editor.Text =
@"SELECT
	*
FROM
	DUAL
	MODEL
		DIMENSION BY (0 AS KEY)
		MEASURES (
			CAST(NULL AS VARCHAR2(4000)) AS M_1
		)
		RULES UPDATE (
			M_1[ANY] = 'x'
		)";
			_editor.CaretOffset = 19;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
		}


		[Test, Apartment(ApartmentState.STA)]
		public void TestAddMissingColumnOverModelDimensionColumn()
		{
			_editor.Text =
@"SELECT
	DUMMY
FROM
	(SELECT DUMMY FROM DUAL)
	MODEL
		DIMENSION BY (DUMMY)
		MEASURES (0 MEASURE)
		RULES (
			MEASURE[ANY] = 0
	    )";

			_editor.CaretOffset = 73;

			CanExecuteCommand(OracleCommands.AddMissingColumn).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddColumnAliasCommand()
		{
			_editor.Text = @"SELECT 'Prefix' || TBL.RESPONDENTBUCKET_ID || 'Postfix', NAME FROM (SELECT RESPONDENTBUCKET_ID, NAME FROM (SELECT RESPONDENTBUCKET_ID, NAME FROM SELECTION) WHERE RESPONDENTBUCKET_ID > 0) TBL";
			_editor.CaretOffset = 114;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "RBID"} ));

			_editor.Text.ShouldBe(@"SELECT 'Prefix' || TBL.RBID || 'Postfix', NAME FROM (SELECT RBID, NAME FROM (SELECT RESPONDENTBUCKET_ID RBID, NAME FROM SELECTION) WHERE RBID > 0) TBL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddAliasCommandAtTableWithAlias()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION S";
			_editor.CaretOffset = 70;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddAliasCommandAtTableWithFlashbackClause()
		{
			_editor.Text = @"SELECT dual.dummy FROM dual AS OF TIMESTAMP sysdate - 1";
			_editor.CaretOffset = 23;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "d" }));

			_editor.Text.ShouldBe(@"SELECT d.dummy FROM dual AS OF TIMESTAMP sysdate - 1 d");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddAliasCommandWithWhereGroupByAndHavingClauses()
		{
			_editor.Text = "SELECT SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION WHERE SELECTION.NAME = NAME GROUP BY SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(SELECTION.SELECTION_ID) = COUNT(SELECTION_ID)";
			_editor.CaretOffset = 60;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "S" } ));

			_editor.Text.ShouldBe("SELECT S.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION S WHERE S.NAME = NAME GROUP BY S.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(S.SELECTION_ID) = COUNT(SELECTION_ID)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddObjectAliasWithPivotTable()
		{
			_editor.Text =
@"SELECT
	*
FROM
	DUAL
	PIVOT (
		COUNT(DUAL.DUMMY)
		FOR (DUMMY) IN ('X' AS X)
	)";
			
			_editor.CaretOffset = 20;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "D" }));

			const string expectedText =
@"SELECT
	*
FROM
	DUAL D
	PIVOT (
		COUNT(D.DUMMY)
		FOR (DUMMY) IN ('X' AS X)
	)";

			_editor.Text.ShouldBe(expectedText);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddColumnAliasWithPivotTable()
		{
			_editor.Text =
@"SELECT
	*
FROM
	(SELECT DUMMY FROM DUAL)
	PIVOT (
		COUNT(DUMMY)
		FOR (DUMMY) IN ('X' AS X)
	)";

			_editor.CaretOffset = 28;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "C1" }));

			const string expectedText =
@"SELECT
	*
FROM
	(SELECT DUMMY C1 FROM DUAL)
	PIVOT (
		COUNT(C1)
		FOR (C1) IN ('X' AS X)
	)";

			_editor.Text.ShouldBe(expectedText);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddColumnAliasWithNestedPivotTable()
		{
			_editor.Text =
@"SELECT
    DUMMY
FROM
(
    SELECT
        DUMMY, 1 VALUE
    FROM
        DUAL
)
PIVOT
(
    COUNT(*)
    FOR VALUE IN
    (
        1 ONE
    )
)
ORDER BY
    DUMMY";

			_editor.CaretOffset = 48;

			CanExecuteCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddAlias, new TestCommandSettings(new CommandSettingsModel { Value = "C1" }));

			const string expectedText =
@"SELECT
    C1
FROM
(
    SELECT
        DUMMY C1, 1 VALUE
    FROM
        DUAL
)
PIVOT
(
    COUNT(*)
    FOR VALUE IN
    (
        1 ONE
    )
)
ORDER BY
    C1";

			_editor.Text.ShouldBe(expectedText);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicWrapAsInlineViewCommand()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S";

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel { Value = "IV" } ));

			_editor.Text.ShouldBe(@"SELECT IV.RESPONDENTBUCKET_ID, IV.SELECTION_ID, IV.PROJECT_ID, IV.NAME, IV.""1"" FROM (SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S) IV");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestWrapAsInlineViewCommandWithTableWithInvisibleColumns()
		{
			_editor.Text = @"SELECT NULL FROM ""CaseSensitiveTable""";
			_editor.CaretOffset = 18;

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe(@"SELECT NULL FROM (SELECT ""CaseSensitiveColumn"", VIRTUAL_COLUMN FROM ""CaseSensitiveTable"") ""CaseSensitiveTable""");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestWrapAsInlineViewCommandWithExistingModelClause()
		{
			_editor.Text =
@"SELECT
	*
FROM
	DUAL
	MODEL
		DIMENSION BY (0 AS KEY)
		MEASURES (
			CAST(NULL AS VARCHAR2(4000)) AS M_1
		)
		RULES UPDATE (
			M_1[ANY] = 'x'
		)";
			_editor.CaretOffset = 19;

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel()));

			const string expectedResult =
@"SELECT
	*
FROM
	(SELECT DUMMY FROM DUAL) DUAL
	MODEL
		DIMENSION BY (0 AS KEY)
		MEASURES (
			CAST(NULL AS VARCHAR2(4000)) AS M_1
		)
		RULES UPDATE (
			M_1[ANY] = 'x'
		)";
			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestWrapAsInlineViewCommandWithExistingPivotClause()
		{
			_editor.Text =
@"SELECT
	*
FROM
	DUAL
	PIVOT (
		COUNT(DUMMY)
		FOR (DUMMY)	IN ('X')
	)";

			_editor.CaretOffset = 19;

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel()));

			const string expectResult =
@"SELECT
	*
FROM
	(SELECT DUMMY FROM DUAL) DUAL
	PIVOT (
		COUNT(DUMMY)
		FOR (DUMMY)	IN ('X')
	)";

			_editor.Text.ShouldBe(expectResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestWrapAsInlineViewCommandWithoutAlias()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S";

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe(@"SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME, ""1"" FROM (SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestWrapAsInlineViewCommandWithOrderByClause()
		{
			_editor.Text = @"SELECT DUMMY FROM DUAL ORDER BY DUAL.DUMMY";

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe(@"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL ORDER BY DUAL.DUMMY)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableReferenceWrapAsInlineView()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S";
			_editor.CaretOffset = 82;

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView);

			_editor.Text.ShouldBe(@"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM (SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME FROM SELECTION) S");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableReferenceWrapAsInlineViewWithXmlTable()
		{
			_editor.Text = @"SELECT * FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA COLUMNS SEQ# FOR ORDINALITY, TITLE VARCHAR2(4000) PATH 'title', DESCRIPTION CLOB PATH 'description')";
			_editor.CaretOffset = 17;

			CanExecuteCommand(OracleCommands.WrapAsInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.WrapAsInlineView);

			_editor.Text.ShouldBe(@"SELECT * FROM (SELECT SEQ#, TITLE, DESCRIPTION FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA COLUMNS SEQ# FOR ORDINALITY, TITLE VARCHAR2(4000) PATH 'title', DESCRIPTION CLOB PATH 'description'))");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicWrapAsInlineViewCommandWithFunctionInvokationWithSingleIdentifierParameter()
		{
			_editor.Text = @"SELECT COUNT(DISTINCT SELECTION_ID) OVER (), RESPONDENTBUCKET_ID FROM SELECTION";

			ExecuteCommand(OracleCommands.WrapAsInlineView, new TestCommandSettings(new CommandSettingsModel { Value = "IV" }));

			_editor.Text.ShouldBe(@"SELECT IV.""COUNT(DISTINCTSELECTION_ID)OVER()"", IV.RESPONDENTBUCKET_ID FROM (SELECT COUNT(DISTINCT SELECTION_ID) OVER (), RESPONDENTBUCKET_ID FROM SELECTION) IV");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicWrapAsCommonTableExpressionCommand()
		{
			_editor.Text = "SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";

			ExecuteCommand(OracleCommands.WrapAsCommonTableExpression, new TestCommandSettings(new CommandSettingsModel { Value = "MYQUERY" } ));

			_editor.Text.ShouldBe(@"WITH MYQUERY AS (SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT MYQUERY.""1"", MYQUERY.MYCOLUMN, MYQUERY.COLUMN3 FROM MYQUERY");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicWrapAsCommonTableExpressionWithOrderByClause()
		{
			_editor.Text = "SELECT DUMMY FROM DUAL ORDER BY DUAL.DUMMY";

			ExecuteCommand(OracleCommands.WrapAsCommonTableExpression, new TestCommandSettings(new CommandSettingsModel { Value = "MYQUERY" }));

			_editor.Text.ShouldBe(@"WITH MYQUERY AS (SELECT DUMMY FROM DUAL ORDER BY DUAL.DUMMY) SELECT MYQUERY.DUMMY FROM MYQUERY");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicWrapAsCommonTableExpressionCommandWithExistingCommonTableExpressionAndWhiteSpace()
		{
			_editor.Text = "\t\t            WITH OLDQUERY AS (SELECT OLD FROM OLD) SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			_editor.CaretOffset = 55;

			ExecuteCommand(OracleCommands.WrapAsCommonTableExpression, new TestCommandSettings(new CommandSettingsModel { Value = "NEWQUERY" }));

			_editor.Text.ShouldBe("\t\t            WITH OLDQUERY AS (SELECT OLD FROM OLD), NEWQUERY AS (SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT NEWQUERY.\"1\", NEWQUERY.MYCOLUMN, NEWQUERY.COLUMN3 FROM NEWQUERY");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicToggleQuotedNotationCommandOn()
		{
			_editor.Text = "SELECT \"PUBLIC\".DUAL.DUMMY, S.PROJECT_ID FROM SELECTION S, \"PUBLIC\".DUAL";

			ExecuteCommand(OracleCommands.ToggleQuotedNotation);

			_editor.Text.ShouldBe("SELECT \"PUBLIC\".\"DUAL\".\"DUMMY\", \"S\".\"PROJECT_ID\" FROM \"SELECTION\" \"S\", \"PUBLIC\".\"DUAL\"");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicToggleQuotedNotationCommandOff()
		{
			_editor.Text = "SELECT \"PUBLIC\".\"DUAL\".\"DUMMY\", \"S\".\"PROJECT_ID\" FROM \"SELECTION\" \"S\", \"PUBLIC\".\"DUAL\"";

			ExecuteCommand(OracleCommands.ToggleQuotedNotation);

			_editor.Text.ShouldBe("SELECT \"PUBLIC\".DUAL.DUMMY, S.PROJECT_ID FROM SELECTION S, \"PUBLIC\".DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestBasicToggleQuotedNotationCommandWithSubqueryWithQuotedNotation()
		{
			_editor.Text = "SELECT DUMMY FROM (SELECT \"DUMMY\" FROM \"DUAL\")";

			ExecuteCommand(OracleCommands.ToggleQuotedNotation);

			_editor.Text.ShouldBe("SELECT \"DUMMY\" FROM (SELECT \"DUMMY\" FROM \"DUAL\")");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestWrapCommonTableExpressionIntoAnotherCommonTableExpression()
		{
			_editor.Text = "WITH CTE1 AS (SELECT NAME FROM SELECTION) SELECT NAME FROM CTE1";
			_editor.CaretOffset = 15;

			ExecuteCommand(OracleCommands.WrapAsCommonTableExpression, new TestCommandSettings(new CommandSettingsModel { Value = "CTE2" } ));

			_editor.Text.ShouldBe(@"WITH CTE2 AS (SELECT NAME FROM SELECTION), CTE1 AS (SELECT CTE2.NAME FROM CTE2) SELECT NAME FROM CTE1");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithObjectReference()
		{
			_editor.Text = "SELECT SELECTION.*, PROJECT.* FROM SELECTION, PROJECT";
			_editor.CaretOffset = 28;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT SELECTION.*, PROJECT.NAME, PROJECT.PROJECT_ID FROM SELECTION, PROJECT");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithObjectReferenceWithHiddenColumns()
		{
			_editor.Text = "SELECT T.* FROM \"CaseSensitiveTable\" T";
			_editor.CaretOffset = 9;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT T.\"CaseSensitiveColumn\", T.VIRTUAL_COLUMN FROM \"CaseSensitiveTable\" T");
		}
		
		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithoutSpaces()
		{
			_editor.Text = "SELECT*FROM dual";
			_editor.CaretOffset = 6;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT dual.DUMMY FROM dual");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithQuotedIdentifierAndLowerFormatOption()
		{
			OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier = FormatOption.Lower;

			_editor.Text = "SELECT * FROM \"CaseSensitiveTable\"";
			_editor.CaretOffset = 7;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT \"CaseSensitiveTable\".\"CaseSensitiveColumn\", \"CaseSensitiveTable\".virtual_column FROM \"CaseSensitiveTable\"");
		}


		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithConnectByPseudocolumn()
		{
			_editor.Text = "SELECT * FROM dual CONNECT BY LEVEL <= 2";
			_editor.CaretOffset = 7;

			var commandSettings = new CommandSettingsModel();
			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(commandSettings));

			commandSettings.BooleanOptions.Count.ShouldBe(4);
			var firstColumnName = commandSettings.BooleanOptions.Keys.First();
			firstColumnName.ShouldBe("CONNECT_BY_ISLEAF");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithAmbiguousColumnName()
		{
			_editor.Text = "SELECT * FROM (SELECT * FROM DUAL T1, DUAL T2)";
			_editor.CaretOffset = 7;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT DUMMY FROM (SELECT * FROM DUAL T1, DUAL T2)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithObjectReferenceOverDatabaseLink()
		{
			_editor.Text = "SELECT SELECTION.*, PROJECT.* FROM SELECTION, PROJECT@HQ_PDB_LOOPBACK";
			_editor.CaretOffset = 28;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT SELECTION.*, PROJECT.REMOTE_COLUMN1, PROJECT.\"RemoteColumn2\" FROM SELECTION, PROJECT@HQ_PDB_LOOPBACK");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithAllColumns()
		{
			_editor.Text = "SELECT * FROM PROJECT, PROJECT P";
			_editor.CaretOffset = 7;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT PROJECT.NAME, PROJECT.PROJECT_ID, P.NAME, P.PROJECT_ID FROM PROJECT, PROJECT P");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandAsteriskCommandWithAllColumnsOverDatabaseLink()
		{
			_editor.Text = "SELECT * FROM PROJECT@HQ_PDB_LOOPBACK, PROJECT@HQ_PDB_LOOPBACK P";
			_editor.CaretOffset = 7;

			ExecuteCommand(OracleCommands.ExpandAsterisk, new TestCommandSettings(new CommandSettingsModel()));

			_editor.Text.ShouldBe("SELECT PROJECT.REMOTE_COLUMN1, PROJECT.\"RemoteColumn2\", P.REMOTE_COLUMN1, P.\"RemoteColumn2\" FROM PROJECT@HQ_PDB_LOOPBACK, PROJECT@HQ_PDB_LOOPBACK P");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommand()
		{
			_editor.Text = @"SELECT IV.TEST_COLUMN || ' ADDED' FROM PROJECT, (SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' TEST_COLUMN FROM SELECTION) IV, RESPONDENTBUCKET";
			_editor.CaretOffset = 50;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' || ' ADDED' FROM PROJECT, SELECTION, RESPONDENTBUCKET");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithDirectColumnReference()
		{
			_editor.Text = @"SELECT EXPRESSION FROM (SELECT NVL(NULL, 0) - NVL(NULL, 0) EXPRESSION FROM dual)";
			_editor.CaretOffset = 24;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT NVL(NULL, 0) - NVL(NULL, 0) EXPRESSION FROM dual");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithWhereClause()
		{
			_editor.Text = @"SELECT IV.TEST_COLUMN || ' ADDED' FROM PROJECT, (SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' TEST_COLUMN FROM SELECTION WHERE SELECTION_ID = 123) IV, RESPONDENTBUCKET";
			_editor.CaretOffset = 50;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' || ' ADDED' FROM PROJECT, SELECTION, RESPONDENTBUCKET WHERE SELECTION_ID = 123");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithCombinedWhereClause()
		{
			_editor.Text = @"SELECT * FROM (SELECT * FROM SELECTION WHERE SELECTION_ID = 123) IV, RESPONDENTBUCKET RB WHERE RB.RESPONDENTBUCKET_ID = 456";
			_editor.CaretOffset = 17;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION, RESPONDENTBUCKET RB WHERE RB.RESPONDENTBUCKET_ID = 456 AND SELECTION_ID = 123");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithAsterisk()
		{
			_editor.Text = @"SELECT IV.* FROM (SELECT * FROM SELECTION, RESPONDENTBUCKET) IV";
			_editor.CaretOffset = 18;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION, RESPONDENTBUCKET");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithObjectAsteriskCombinedWithOtherColumn()
		{
			_editor.Text = @"SELECT IV.*, TARGETGROUP_ID FROM (SELECT 1 C1, SELECTION.*, 3 C3 FROM SELECTION) IV, RESPONDENTBUCKET";
			_editor.CaretOffset = 40;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT 1 C1, SELECTION.*, 3 C3, TARGETGROUP_ID FROM SELECTION, RESPONDENTBUCKET");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithQueryBlocksContainingConflictingAnalyticsFunctions()
		{
			_editor.Text = @"SELECT COUNT(VALUE) OVER () FROM (SELECT COUNT(DUMMY) OVER () VALUE FROM DUAL)";
			_editor.CaretOffset = 34;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithQueryBlocksContainingNonConflictingAnalyticsFunctions()
		{
			_editor.Text =
@"SELECT
	COUNT(C1) OVER (),
	C2
FROM
	(SELECT 1 C1, COUNT(*) OVER () C2 FROM DUAL) DUAL";

			_editor.CaretOffset = 48;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithWithInlineViewWithoutSpace()
		{
			_editor.Text = @"SELECT * FROM SELECTION JOIN(SELECT NAME FROM PROJECT) S ON SELECTION.NAME = S.NAME";
			_editor.CaretOffset = 30;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION JOIN PROJECT ON SELECTION.NAME = PROJECT.NAME");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithWithInlineViewWithObjectNamePrefix()
		{
			_editor.Text = @"SELECT * FROM SELECTION JOIN(SELECT PROJECT.NAME FROM PROJECT) S ON SELECTION.NAME = S.NAME";
			_editor.CaretOffset = 30;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION JOIN PROJECT ON SELECTION.NAME = PROJECT.NAME");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnnestCommandWithColumnExpressions()
		{
			_editor.Text = @"SELECT 'OuterPrefix' || IV.VAL || 'OuterPostfix' FROM (SELECT 'InnerPrefix' || (DUMMY || 'InnerPostfix') VAL FROM DUAL) IV";
			_editor.CaretOffset = 60;

			CanExecuteCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT 'OuterPrefix' || 'InnerPrefix' || (DUAL.DUMMY || 'InnerPostfix') || 'OuterPostfix' FROM DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSafeDeleteCommandAtObjectAlias()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, S.PROJECT_ID, S.NAME FROM SELECTION S";
			_editor.CaretOffset = 82;

			ExecuteCommand(SafeDeleteCommand.SafeDelete);

			_editor.Text.ShouldBe("SELECT SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, SELECTION.PROJECT_ID, SELECTION.NAME FROM SELECTION ");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSafeDeleteCommandAtColumnAlias()
		{
			_editor.Text = @"SELECT XXX FROM (SELECT XXX FROM (SELECT XXX FROM (SELECT DUMMY XXX FROM DUAL) T1) T2) T3";
			_editor.CaretOffset = 64;

			ExecuteCommand(SafeDeleteCommand.SafeDelete);

			_editor.Text.ShouldBe("SELECT DUMMY FROM (SELECT DUMMY FROM (SELECT DUMMY FROM (SELECT DUMMY  FROM DUAL) T1) T2) T3");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestModifyCaseCommandWithMultipleTerminalsSelected()
		{
			_editor.Text = @"select null, 'null' from selection";
			_editor.CaretOffset = 3;
			_editor.SelectionLength = 28;

			ExecuteCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("selECT NULL, 'null' FROM SELECTion");

			ExecuteCommand(ModifyCaseCommand.MakeLowerCase);

			_editor.Text.ShouldBe("select null, 'null' from selection");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestModifyCaseCommandWithMultipleSelectionSegments()
		{
			_editor.Text =
@"select null, 'null' from selection;
select null, 'null' from selection";
			_editor.TextArea.Selection = new RectangleSelection(_editor.TextArea, new TextViewPosition(1, 4), new TextViewPosition(2, 23));
			_editor.TextArea.Selection.Segments.Count().ShouldBe(2);

			ExecuteCommand(ModifyCaseCommand.MakeUpperCase);

			const string expectedResult =
@"selECT NULL, 'null' FRom selection;
selECT NULL, 'null' FRom selection";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestModifyCaseCommandWithUnrecognizedGrammar()
		{
			_editor.Text = @"lot of invalid tokens preceding; select 'null' as ""null"" from dual and lot of invalid tokens following";
			_editor.SelectionLength = _editor.Text.Length;

			ExecuteCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("LOT OF INVALID TOKENS PRECEDING; SELECT 'null' AS \"null\" FROM DUAL AND LOT OF INVALID TOKENS FOLLOWING");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestModifyCaseCommandWithSingleCaseUnsafeToken()
		{
			_editor.Text = @"SELECT 'null' FROM DUAL";
			_editor.CaretOffset = 7;
			_editor.SelectionLength = 6;

			ExecuteCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("SELECT 'NULL' FROM DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestModifyCaseCommandWithCaseUnsafeTokenAsLastToken()
		{
			_editor.Text = @"select * from ""Accounts""";
			_editor.SelectAll();

			ExecuteCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("SELECT * FROM \"Accounts\"");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveContentCommandUp()
		{
			_editor.Text = @"SELECT 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix' FROM PROJECT";
			_editor.CaretOffset = 66;

			ExecuteCommand(MoveContentCommand.MoveContentUp);

			_editor.Text.ShouldBe("SELECT 'IdPrefix' || PROJECT_ID || 'IdPostfix', 'NamePrefix' || NAME || 'NamePostfix' FROM PROJECT");
			_editor.CaretOffset.ShouldBe(27);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveOrderByExpressionCommandUp()
		{
			_editor.Text = @"SELECT * FROM SELECTION ORDER BY 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || SELECTION_ID || 'IdPostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix'";
			_editor.CaretOffset = 86;

			ExecuteCommand(MoveContentCommand.MoveContentUp);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION ORDER BY 'IdPrefix' || SELECTION_ID || 'IdPostfix', 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix'");
			_editor.CaretOffset.ShouldBe(47);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveOrderByExpressionCommandDown()
		{
			_editor.Text = @"SELECT * FROM SELECTION ORDER BY 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || SELECTION_ID || 'IdPostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix'";
			_editor.CaretOffset = 33;

			ExecuteCommand(MoveContentCommand.MoveContentDown);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION ORDER BY 'IdPrefix' || SELECTION_ID || 'IdPostfix', 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix'");
			_editor.CaretOffset.ShouldBe(76);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveContentCommandDown()
		{
			_editor.Text = @"SELECT 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix' FROM PROJECT";
			_editor.CaretOffset = 24;

			ExecuteCommand(MoveContentCommand.MoveContentDown);

			_editor.Text.ShouldBe("SELECT 'IdPrefix' || PROJECT_ID || 'IdPostfix', 'NamePrefix' || NAME || 'NamePostfix' FROM PROJECT");
			_editor.CaretOffset.ShouldBe(65);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveContentCommandDownAtLastColumn()
		{
			_editor.Text = @"SELECT 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix' FROM PROJECT";
			_editor.CaretOffset = 66;

			ExecuteCommand(MoveContentCommand.MoveContentDown);

			_editor.Text.ShouldBe("SELECT 'NamePrefix' || NAME || 'NamePostfix', 'IdPrefix' || PROJECT_ID || 'IdPostfix' FROM PROJECT");
			_editor.CaretOffset.ShouldBe(66);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveFromClauseDown()
		{
			_editor.Text =
@"SELECT
	*
FROM
	DUAL D1
	JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY,
	DUAL D3";
			_editor.CaretOffset = 64;

			ExecuteCommand(MoveContentCommand.MoveContentDown);

const string expectedResult =
@"SELECT
	*
FROM
	DUAL D3,
	DUAL D1
	JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY";
			
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(75);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestMoveFromClauseUp()
		{
			_editor.Text =
@"SELECT
	*
FROM
	DUAL D3,
	DUAL D1
	JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY";

			_editor.CaretOffset = 45;

			ExecuteCommand(MoveContentCommand.MoveContentUp);

			const string expectedResult =
@"SELECT
	*
FROM
	DUAL D1
	JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY,
	DUAL D3";
			
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(34);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithoutExistingGroupByClause()
		{
			_editor.Text = @"SELECT SELECTION.PROJECT_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION";
			_editor.CaretOffset = 18;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT SELECTION.PROJECT_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION GROUP BY SELECTION.PROJECT_ID");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithMultipleSelectColumns()
		{
			_editor.Text = @"SELECT PROJECT_ID, RESPONDENTBUCKET_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION";
			_editor.SelectionStart = 7;
			_editor.SelectionLength = 30;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT PROJECT_ID, RESPONDENTBUCKET_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION GROUP BY PROJECT_ID, RESPONDENTBUCKET_ID");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithSameExpressionWithinExistingGroupByClause()
		{
			_editor.Text = @"SELECT PROJECT_ID, COUNT(*) SELECTION_COUNT FROM SELECTION GROUP BY PROJECT_ID";
			_editor.CaretOffset = 7;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandNotAvailableAtAsteriskTerminal()
		{
			_editor.Text = @"SELECT * FROM SELECTION";
			_editor.CaretOffset = 7;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithWhereClause()
		{
			_editor.Text = @"SELECT PROJECT_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION WHERE NAME LIKE '%1%'";
			_editor.SelectionStart = 7;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT PROJECT_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION WHERE NAME LIKE '%1%' GROUP BY PROJECT_ID");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandAtColumnTableQualifier()
		{
			_editor.Text = @"SELECT SELECTION.NAME, COUNT(*) FROM SELECTION JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID";
			_editor.SelectionStart = 7;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithOrderByClause()
		{
			_editor.Text = @"SELECT PROJECT_ID, RESPONDENTBUCKET_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION GROUP BY PROJECT_ID ORDER BY PROJECT_SELECTIONS";
			_editor.SelectionStart = 25;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT PROJECT_ID, RESPONDENTBUCKET_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION GROUP BY PROJECT_ID, RESPONDENTBUCKET_ID ORDER BY PROJECT_SELECTIONS");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithExpressionFollowedByOtherColumn()
		{
			_editor.Text = @"SELECT SELECTIONNAME || 'X', PROJECT_ID + 3 FROM SELECTION";
			_editor.SelectionStart = 7;
			_editor.SelectionLength = 20;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT SELECTIONNAME || 'X', PROJECT_ID + 3 FROM SELECTION GROUP BY SELECTIONNAME || 'X'");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandWithExistingGroupByClause()
		{
			_editor.Text = @"SELECT SELECTION.PROJECT_ID, SELECTION.RESPONDENTBUCKET_ID, COUNT(*) SELECTION_COUNT FROM SELECTION GROUP BY SELECTION.PROJECT_ID";
			_editor.CaretOffset = 40;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT SELECTION.PROJECT_ID, SELECTION.RESPONDENTBUCKET_ID, COUNT(*) SELECTION_COUNT FROM SELECTION GROUP BY SELECTION.PROJECT_ID, SELECTION.RESPONDENTBUCKET_ID");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandNotAvailableAtInvalidExpression()
		{
			_editor.Text = @"SELECT 1 + SELECTION_ID FROM SELECTION";
			_editor.SelectionStart = 7;
			_editor.SelectionLength = 3;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandNotAvailableAtBindVariable()
		{
			_editor.Text = @"SELECT :X FROM SELECTION";
			_editor.CaretOffset = 8;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandNotAvailableWhenSequencePseudocolumnWithinSelection()
		{
			_editor.Text = @"SELECT TEST_SEQ.NEXTVAL FROM SELECTION";
			_editor.CaretOffset = 18;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAddToGroupByCommandNotAvailableWhenSequenceWithinSelection()
		{
			_editor.Text = @"SELECT TEST_SEQ.NEXTVAL FROM SELECTION";
			_editor.CaretOffset = 10;

			CanExecuteCommand(OracleCommands.AddToGroupByClause).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferences()
		{
			_editor.Text = @"SELECT SQLPAD_FUNCTION, RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME, SQLPAD.SQLPAD_FUNCTION(0), TO_CHAR('') FROM SELECTION";

			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT HUSQVIK.SQLPAD_FUNCTION, HUSQVIK.SELECTION.RESPONDENTBUCKET_ID, HUSQVIK.SELECTION.SELECTION_ID, HUSQVIK.SELECTION.PROJECT_ID, HUSQVIK.SELECTION.NAME, HUSQVIK.SQLPAD.SQLPAD_FUNCTION(0), TO_CHAR('') FROM HUSQVIK.SELECTION");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesWithAsteriskClause()
		{
			_editor.Text = @"SELECT * FROM SELECTION";

			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT * FROM HUSQVIK.SELECTION");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesWithPartiallyQualifiedAsteriskClause()
		{
			_editor.Text = @"SELECT SELECTION.*, PROJECT.* FROM SELECTION, PROJECT";

			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT HUSQVIK.SELECTION.*, HUSQVIK.PROJECT.* FROM HUSQVIK.SELECTION, HUSQVIK.PROJECT");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesWithNotExistingTable()
		{
			_editor.Text = @"SELECT NOT_EXISTING_TABLE.* FROM NOT_EXISTING_TABLE";

			CanExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesWithAliasedTable()
		{
			_editor.Text = @"SELECT DUMMY, NAME FROM DUAL D, SELECTION S";
			_editor.SelectionLength = 0;

			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT D.DUMMY, S.NAME FROM DUAL D, HUSQVIK.SELECTION S");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesWithRowIdPseudocolumn()
		{
			_editor.Text = @"SELECT ROWID FROM DUAL";
			_editor.SelectionLength = 0;

			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT DUAL.ROWID FROM DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesOnFullyQualifiedSchemaFunction()
		{
			_editor.Text = @"SELECT HUSQVIK.SQLPAD_FUNCTION FROM SYS.DUAL";
			_editor.SelectionLength = 0;

			// TODO: Update when toogle off is implemented
			CanExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesOnNonAliasedTableReference()
		{
			_editor.Text = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL)";
			_editor.SelectionLength = 0;

			CanExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT DUMMY FROM (SELECT DUAL.DUMMY FROM DUAL)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToggleFullyQualifiedReferencesWithinInlineView()
		{
			_editor.Text = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) T";
			_editor.CaretOffset = 19;

			CanExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT DUMMY FROM (SELECT DUAL.DUMMY FROM DUAL) T");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestResolveAmbiguousColumnCommand()
		{
			_editor.Text = @"SELECT DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";
			_editor.CaretOffset = 12;

			var actions =
				new OracleContextActionProvider(_commandSettingsProviderFactory)
					.GetContextActions(TestFixture.DatabaseModel, _editor.Text, _editor.CaretOffset)
					.Where(a => a.Name.StartsWith("Resolve as"))
					.ToArray();

			actions.Length.ShouldBe(2);
			CanExecuteCommand(actions[0].ExecutionHandler).ShouldBe(true);
			ExecuteCommand(actions[0].ExecutionHandler);
			CanExecuteCommand(actions[1].ExecutionHandler).ShouldBe(true);

			_editor.Text.ShouldBe(@"SELECT SYS.DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestResolveAmbiguousColumnCommandWhenAtClosingParenthesisOutsidePrefixedColumnReference()
		{
			_editor.Text = @"SELECT COUNT(DISTINCT DUMMY) FROM DUAL D1, DUAL D2";
			_editor.CaretOffset = 27;

			var actions =
				new OracleContextActionProvider(_commandSettingsProviderFactory)
					.GetContextActions(TestFixture.DatabaseModel, _editor.Text, _editor.CaretOffset)
					.Where(a => a.Name.StartsWith("Resolve as"))
					.ToArray();

			actions.Length.ShouldBe(2);
			CanExecuteCommand(actions[0].ExecutionHandler).ShouldBe(true);
			ExecuteCommand(actions[0].ExecutionHandler);

			_editor.Text.ShouldBe(@"SELECT COUNT(DISTINCT D1.DUMMY) FROM DUAL D1, DUAL D2");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestResolveAmbiguousColumnCommandWhenAtComma()
		{
			_editor.Text = @"SELECT DUMMY, 1 FROM DUAL T1, DUAL T2";
			_editor.CaretOffset = 12;

			var actions =
				new OracleContextActionProvider(_commandSettingsProviderFactory)
					.GetContextActions(TestFixture.DatabaseModel, _editor.Text, _editor.CaretOffset)
					.Where(a => a.Name.StartsWith("Resolve as"))
					.ToArray();

			actions.Length.ShouldBe(2);
			CanExecuteCommand(actions[0].ExecutionHandler).ShouldBe(true);
			ExecuteCommand(actions[0].ExecutionHandler);

			_editor.Text.ShouldBe(@"SELECT T1.DUMMY, 1 FROM DUAL T1, DUAL T2");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestGenerateMissingColumnsCommand()
		{
			_editor.Text = @"SELECT NOT_EXISTING_COLUMN FROM SELECTION";
			_editor.CaretOffset = 7;

			CanExecuteCommand(OracleCommands.AddMissingColumn).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddMissingColumn);

			_editor.Text.ShouldBe("SELECT NOT_EXISTING_COLUMN FROM SELECTION;\r\n\r\nALTER TABLE HUSQVIK.SELECTION ADD\r\n(\r\n\tNOT_EXISTING_COLUMN VARCHAR2(100) NULL\r\n);\r\n");
			_editor.CaretOffset.ShouldBe(105);
			_editor.SelectionLength.ShouldBe(18);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCreateScriptCommand()
		{
			const string statementText = @"SELECT * FROM SELECTION";
			_editor.Text = statementText;
			_editor.CaretOffset = 17;

			CanExecuteCommand(OracleCommands.CreateScript).ShouldBe(true);
			ExecuteCommand(OracleCommands.CreateScript, new TestCommandSettings(new CommandSettingsModel()));

			var expectedResult = statementText + ";" + Environment.NewLine + Environment.NewLine + OracleTestObjectScriptExtractor.SelectionTableCreateScript + ";";
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(17);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCreateScriptCommandAtPackageSynonym()
		{
			const string statementText = @"SELECT DBMS_RANDOM.VALUE FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 7;

			CanExecuteCommand(OracleCommands.CreateScript).ShouldBe(true);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCreateScriptCommandAtPipelinedSchemaFunction()
		{
			const string statementText = @"SELECT * FROM TABLE(SQLPAD_FUNCTION())";
			_editor.Text = statementText;
			_editor.CaretOffset = 20;

			CanExecuteCommand(OracleCommands.CreateScript).ShouldBe(true);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void AddInsertIntoColumnListCommand()
		{
			const string statementText = @"INSERT INTO SELECTION SELECT * FROM SELECTION";
			_editor.Text = statementText;
			_editor.CaretOffset = 8;

			CanExecuteCommand(OracleCommands.AddInsertIntoColumnList).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddInsertIntoColumnList, new TestCommandSettings(new CommandSettingsModel { UseDefaultSettings = () => true } ));

			const string expectedResult = "INSERT INTO SELECTION (RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME) SELECT * FROM SELECTION";
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(8);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ReconfigureInsertIntoColumnList()
		{
			const string statementText = @"INSERT INTO SELECTION (RESPONDENTBUCKET_ID) SELECT * FROM SELECTION";
			_editor.Text = statementText;
			_editor.CaretOffset = 8;

			CanExecuteCommand(OracleCommands.AddInsertIntoColumnList).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddInsertIntoColumnList, new TestCommandSettings(new CommandSettingsModel { UseDefaultSettings = () => true } ));

			const string expectedResult = "INSERT INTO SELECTION (RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME) SELECT * FROM SELECTION";
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(8);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCleanRedundantSymbolCommand()
		{
			const string statementText = @"SELECT SELECTION.SELECTION_ID, HUSQVIK.RESPONDENTBUCKET.TARGETGROUP_ID, HUSQVIK.SELECTION.RESPONDENTBUCKET_ID FROM HUSQVIK.SELECTION, HUSQVIK.RESPONDENTBUCKET";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.CleanRedundantSymbol).ShouldBe(true);
			ExecuteCommand(OracleCommands.CleanRedundantSymbol);

			_editor.Text.ShouldBe("SELECT SELECTION_ID, TARGETGROUP_ID, SELECTION.RESPONDENTBUCKET_ID FROM SELECTION, RESPONDENTBUCKET");
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCleanRedundantProgramQualifier()
		{
			const string statementText = @"SELECT HUSQVIK.INVALID_OBJECT_TYPE(), SYS.XMLTYPE('<root/>'), HUSQVIK.SQLPAD.SQLPAD_FUNCTION(), SYS.DBMS_RANDOM.VALUE, HUSQVIK.TEST_SEQ.NEXTVAL FROM SYS.DUAL";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.CleanRedundantSymbol).ShouldBe(true);
			ExecuteCommand(OracleCommands.CleanRedundantSymbol);

			_editor.Text.ShouldBe("SELECT INVALID_OBJECT_TYPE(), XMLTYPE('<root/>'), SQLPAD.SQLPAD_FUNCTION(), DBMS_RANDOM.VALUE, TEST_SEQ.NEXTVAL FROM SYS.DUAL");
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCleanRedundantTerminalsAtQueryBlockLevelInOrderByClause()
		{
			const string statementText = @"SELECT * FROM SELECTION ORDER BY SELECTION.PROJECT_ID, SELECTION.NAME";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.CleanRedundantSymbol).ShouldBe(true);
			ExecuteCommand(OracleCommands.CleanRedundantSymbol);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION ORDER BY PROJECT_ID, NAME");
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCleanSingleRedundantQualifier()
		{
			const string statementText = @"SELECT SYS.XMLTYPE('<root/>'), SYS.DBMS_RANDOM.VALUE FROM HUSQVIK.SELECTION";
			_editor.Text = statementText;
			_editor.CaretOffset = 8;

			CanExecuteCommand(OracleCommands.CleanRedundantSymbol).ShouldBe(true);
			ExecuteCommand(OracleCommands.CleanRedundantSymbol);

			_editor.Text.ShouldBe("SELECT XMLTYPE('<root/>'), SYS.DBMS_RANDOM.VALUE FROM HUSQVIK.SELECTION");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestGenerateCreateTableScriptFromQueryCommand()
		{
			const string statementText = @"SELECT * FROM DUAL";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.AddCreateTableAs).ShouldBe(true);
			var commandSettings = new TestCommandSettings(new CommandSettingsModel { Value = "NEW_TABLE" });
			commandSettings.GetSettingsCalled += (sender, args) => commandSettings.Settings.BooleanOptions[AddCreateTableAsCommand.CreateSeparateStatement].Value = true;

			ExecuteCommand(OracleCommands.AddCreateTableAs, commandSettings);

			const string expectedResult =
@"SELECT * FROM DUAL;

CREATE TABLE NEW_TABLE (
	DUMMY VARCHAR2(1 BYTE)
);
";
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestGenerateCreateTableScriptFromInlineView()
		{
			const string statementText = @"SELECT * FROM (SELECT * FROM DUAL)";
			_editor.Text = statementText;
			_editor.CaretOffset = 18;

			CanExecuteCommand(OracleCommands.AddCreateTableAs).ShouldBe(true);
			var commandSettings = new TestCommandSettings(new CommandSettingsModel { Value = "NEW_TABLE" });
			commandSettings.GetSettingsCalled += (sender, args) => commandSettings.Settings.BooleanOptions[AddCreateTableAsCommand.CreateSeparateStatement].IsEnabled.ShouldBe(false);

			ExecuteCommand(OracleCommands.AddCreateTableAs, commandSettings);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestGenerateCreateTableAsSelectFromQueryCommand()
		{
			const string statementText = @"SELECT * FROM DUAL";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.AddCreateTableAs).ShouldBe(true);
			ExecuteCommand(OracleCommands.AddCreateTableAs, new TestCommandSettings(new CommandSettingsModel { Value = "NEW_TABLE" }));

			const string expectedResult =
@"CREATE TABLE NEW_TABLE (
	DUMMY
)
AS
SELECT * FROM DUAL";
			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(41);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommand()
		{
			const string statementText = @"SELECT ""CaseSensitiveColumn"", ""CaseSensitiveColumn"" FROM INVOICELINES";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT ""CaseSensitiveColumn"" CaseSensitiveColumn, ""CaseSensitiveColumn"" CaseSensitiveColumn FROM INVOICELINES";

			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithFullyQualifiedColumn()
		{
			const string statementText = @"SELECT ""CaseSensitiveTable"".""CaseSensitiveColumn"" FROM ""CaseSensitiveTable""";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT CaseSensitiveTable.""CaseSensitiveColumn"" CaseSensitiveColumn FROM ""CaseSensitiveTable"" CaseSensitiveTable";

			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithAliasedExpression()
		{
			const string statementText = @"SELECT 1 + 1 ""CaseSensitiveColumn"" FROM DUAL";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT 1 + 1 CaseSensitiveColumn FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithExistingQuotedAlias()
		{
			const string statementText = @"SELECT ""CaseSensitiveColumn"" ""Alias"", ""CaseSensitiveColumn"" FROM INVOICELINES";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT ""CaseSensitiveColumn"" Alias, ""CaseSensitiveColumn"" CaseSensitiveColumn FROM INVOICELINES";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithExistingObjectQuotedAlias()
		{
			const string statementText = @"SELECT ""ObjectAlias"".* FROM ""CaseSensitiveTable"" ""ObjectAlias""";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT ObjectAlias.* FROM ""CaseSensitiveTable"" ObjectAlias";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithObjectOverDatabaseLink()
		{
			const string statementText = @"SELECT ""CaseSensitiveTable"".* FROM ""CaseSensitiveTable""@HQ_PDB_LOOPBACK";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT CaseSensitiveTable.* FROM ""CaseSensitiveTable""@HQ_PDB_LOOPBACK CaseSensitiveTable";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithRedundantQuotes()
		{
			const string statementText = @"SELECT ""DUAL"".* FROM ""DUAL""";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult = @"SELECT DUAL.* FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithRedundantColumnQuotes()
		{
			const string statementText =
@"SELECT
	""DUMMY"",
	""DUMMY""
FROM
	DUAL";

			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(true);
			ExecuteCommand(OracleCommands.Unquote);

			const string expectedResult =
@"SELECT
	DUMMY,
	DUMMY
FROM
	DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithReservedWordCollision()
		{
			const string statementText = @"SELECT ""Level"" FROM DUAL";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestUnquoteCommandWithAsteriskAndexistingObjectAlias()
		{
			const string statementText = @"SELECT * FROM ""RemoteTable""@HQ_PDB_LOOPBACK REMOTE_TABLE";
			_editor.Text = statementText;

			CanExecuteCommand(OracleCommands.Unquote).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertSingleBindVariableToLiteralCommand()
		{
			const string statementText = @"SELECT :1, :1 FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 12;

			SetBindVariableAndExecute(0, "VALUE");

			const string expectedResult = @"SELECT :1, 'VALUE' FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertAllBindVariableOccurencesToLiteralCommand()
		{
			const string statementText = @"SELECT :1, :1 FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 12;

			SetBindVariableAndExecute(1, "2014-10-04", TerminalValues.Date);

			const string expectedResult = @"SELECT DATE'2014-10-04', DATE'2014-10-04' FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertAllBindVariableOccurencesToTimestampLiteralCommand()
		{
			const string statementText = @"SELECT :1, :1 FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 12;

			SetBindVariableAndExecute(1, "2014-11-28 14:16:18", TerminalValues.Timestamp);

			const string expectedResult = @"SELECT TIMESTAMP'2014-11-28 14:16:18', TIMESTAMP'2014-11-28 14:16:18' FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		private void SetBindVariableAndExecute(int actionIndex, string value, string dataType = TerminalValues.Varchar2)
		{
			var actions = new OracleContextActionProvider(_commandSettingsProviderFactory)
					.GetContextActions(TestFixture.DatabaseModel, _editor.Text, _editor.CaretOffset)
					.Where(a => a.Name.StartsWith("Convert"))
					.ToArray();

			actions.Length.ShouldBe(2);

			var action = actions[actionIndex];
			action.ExecutionContext.DocumentRepository.Statements.Count.ShouldBe(1);
			var bindVariable = action.ExecutionContext.DocumentRepository.Statements.Single().BindVariables.Single();
			bindVariable.DataType = dataType;
			bindVariable.Value = value;
			action.ExecutionHandler.CanExecuteHandler(action.ExecutionContext).CanExecute.ShouldBe(true);
			ExecuteCommand(action.ExecutionHandler, action.ExecutionContext);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertSingleLiteralToBindVariableCommand()
		{
			const string statementText = @"SELECT 'VALUE', 'VALUE' FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 20;

			ExecuteConvertLiteralToBindVariableCommand(0);

			const string expectedResult = @"SELECT 'VALUE', :BIND_VARIABLE FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertAllLiteralOccurencesToBindVariableCommand()
		{
			const string statementText = @"SELECT DATE'2014-10-04', DATE'2014-10-04', TIMESTAMP'2014-10-04', '2014-10-04' FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 15;

			ExecuteConvertLiteralToBindVariableCommand(1);

			const string expectedResult = @"SELECT :BIND_VARIABLE, :BIND_VARIABLE, TIMESTAMP'2014-10-04', '2014-10-04' FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertNumberLiteralToBindVariable()
		{
			const string statementText = @"SELECT 123, 123 FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 10;

			ExecuteConvertLiteralToBindVariableCommand(0);

			const string expectedResult = @"SELECT :BIND_VARIABLE, 123 FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);

			var configuration = WorkDocumentCollection.GetProviderConfiguration(TestFixture.DatabaseModel.ConnectionString.ProviderName);
			var bindVariable = configuration.GetBindVariable("BIND_VARIABLE");
			bindVariable.ShouldNotBe(null);
			bindVariable.DataType.ShouldBe("NUMBER");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertTimestampLiteralToBindVariable()
		{
			const string statementText = @"SELECT TIMESTAMP'2014-11-24 14:14:14', TIMESTAMP'2014-11-24 14:14:14' FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 18;

			ExecuteConvertLiteralToBindVariableCommand(0);

			const string expectedResult = @"SELECT :BIND_VARIABLE, TIMESTAMP'2014-11-24 14:14:14' FROM DUAL";

			_editor.Text.ShouldBe(expectedResult);

			var configuration = WorkDocumentCollection.GetProviderConfiguration(TestFixture.DatabaseModel.ConnectionString.ProviderName);
			var bindVariable = configuration.GetBindVariable("BIND_VARIABLE");
			bindVariable.ShouldNotBe(null);
			bindVariable.DataType.ShouldBe("TIMESTAMP");
		}

		private void ExecuteConvertLiteralToBindVariableCommand(int actionIndex)
		{
			var actions = new OracleContextActionProvider(_commandSettingsProviderFactory)
				.GetContextActions(TestFixture.DatabaseModel, _editor.Text, _editor.CaretOffset)
				.Where(a => a.Name.StartsWith("Convert"))
				.ToArray();

			actions.Length.ShouldBe(2);
			var action = actions[actionIndex];
			action.ExecutionContext.SettingsProvider = new TestCommandSettings(new CommandSettingsModel { Value = "BIND_VARIABLE" });
			action.ExecutionHandler.CanExecuteHandler(action.ExecutionContext).CanExecute.ShouldBe(true);
			ExecuteCommand(action.ExecutionHandler, action.ExecutionContext);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPropagateCommand()
		{
			const string statementText = @"SELECT 1 C1 FROM (SELECT 2 C2 FROM DUAL)";
			_editor.Text = statementText;
			_editor.CaretOffset = 28;

			CanExecuteCommand(OracleCommands.PropagateColumn).ShouldBe(true);
			ExecuteCommand(OracleCommands.PropagateColumn);

			const string expectedResult = @"SELECT 1 C1, C2 FROM (SELECT 2 C2 FROM DUAL)";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPropagateCommandWithGrandParentWithAsterisk()
		{
			const string statementText = @"SELECT * FROM (SELECT 1 FROM (SELECT 1 C FROM DUAL))";
			_editor.Text = statementText;
			_editor.CaretOffset = 37;

			CanExecuteCommand(OracleCommands.PropagateColumn).ShouldBe(true);
			ExecuteCommand(OracleCommands.PropagateColumn);

			const string expectedResult = @"SELECT * FROM (SELECT 1, C FROM (SELECT 1 C FROM DUAL))";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPropagateCommandNotAvailable()
		{
			const string statementText = @"SELECT SELECTION_ID FROM SELECTION";
			_editor.Text = statementText;
			_editor.CaretOffset = 8;

			CanExecuteCommand(OracleCommands.PropagateColumn).ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPropagateCommandWithWithoutAlias()
		{
			const string statementText = @"SELECT 1 FROM (SELECT 1 FROM DUAL)";
			_editor.Text = statementText;
			_editor.CaretOffset = 22;

			CanExecuteCommand(OracleCommands.PropagateColumn).ShouldBe(true);
			ExecuteCommand(OracleCommands.PropagateColumn, new TestCommandSettings(new CommandSettingsModel { Value = "COLUMN1" }));

			const string expectedResult = @"SELECT 1, COLUMN1 FROM (SELECT 1 COLUMN1 FROM DUAL)";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPropagateCommandCanBeExecutedAtCommaTerminal()
		{
			const string statementText = @"SELECT 1 FROM (SELECT DUMMY, DUMMY FROM DUAL)";
			_editor.Text = statementText;
			_editor.CaretOffset = 27;

			CanExecuteCommand(OracleCommands.PropagateColumn).ShouldBe(true);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPropagateCommandWithQueryBlockWithModelClause()
		{
			const string statementText =
@"SELECT
	C1, M1
FROM (SELECT 1 C1, 2 C2 FROM DUAL)
MODEL
	DIMENSION BY (C1)
	MEASURES (0 M1)
	RULES (
		M1[ANY] = DBMS_RANDOM.VALUE
	)";
			_editor.Text = statementText;
			_editor.CaretOffset = 36;

			CanExecuteCommand(OracleCommands.PropagateColumn).ShouldBe(true);
			ExecuteCommand(OracleCommands.PropagateColumn);

			const string expectedResult =
@"SELECT
	C1, M1, C2
FROM (SELECT 1 C1, 2 C2 FROM DUAL)
MODEL
	DIMENSION BY (C1)
	MEASURES (0 M1, C2)
	RULES (
		M1[ANY] = DBMS_RANDOM.VALUE
	)";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertOrderByColumnReferences()
		{
			const string statementText = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 1, 2";
			_editor.Text = statementText;
			_editor.CaretOffset = 67;

			CanExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences);

			const string expectedResult = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY T.NAME, '[' || NAME || ']'";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertOrderByColumnReferencesWithInvalidColumnNumber()
		{
			const string statementText = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 1, 3";
			_editor.Text = statementText;
			_editor.CaretOffset = 67;

			CanExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences);

			const string expectedResult = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY T.NAME, 3";

			_editor.Text.ShouldBe(expectedResult);
		}
		
		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertOrderByColumnReferencesWithAliasedDirectColumnReference()
		{
			const string statementText = @"SELECT DUMMY NOT_DUMMY FROM DUAL ORDER BY 1";
			_editor.Text = statementText;
			_editor.CaretOffset = 42;

			CanExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences);

			const string expectedResult = @"SELECT DUMMY NOT_DUMMY FROM DUAL ORDER BY NOT_DUMMY";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertOrderByColumnReferencesAtSpecificColumn()
		{
			const string statementText = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 1, 2";
			_editor.Text = statementText;
			_editor.CaretOffset = 79;

			CanExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences);

			const string expectedResult = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 1, '[' || NAME || ']'";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestConvertOrderByColumnReferencesWithColumnAliasAndDescendingOrder()
		{
			const string statementText = @"SELECT COUNT(*) TOTALS FROM DUAL GROUP BY DUMMY ORDER BY 1 DESC";
			_editor.Text = statementText;
			_editor.CaretOffset = 57;

			CanExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences).ShouldBe(true);
			ExecuteCommand(OracleCommands.ConvertOrderByNumberColumnReferences);

			const string expectedResult = @"SELECT COUNT(*) TOTALS FROM DUAL GROUP BY DUMMY ORDER BY TOTALS DESC";

			_editor.Text.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSplitStringCommand()
		{
			const string statementText = @"SELECT q'|sometext|' FROM dual";
			_editor.Text = statementText;
			_editor.CaretOffset = 14;

			ExecuteCommand(OracleCommands.SplitString);

			const string expectedResult = @"SELECT q'|some|' ||  || q'|text|' FROM dual";

			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(20);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSplitStringCommandAfterOffApostrophe()
		{
			const string statementText = @"SELECT 'some''text' FROM dual";
			_editor.Text = statementText;
			_editor.CaretOffset = 14;

			ExecuteCommand(OracleCommands.SplitString);

			const string expectedResult = @"SELECT 'some''' ||  || 'text' FROM dual";

			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(19);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExpandViewCommand()
		{
			const string statementText = @"SELECT * FROM v$session";
			_editor.Text = statementText;
			_editor.CaretOffset = 14;

			ExecuteCommand(OracleCommands.ExpandView);

			const string expectedResult = @"SELECT * FROM (SELECT dummy FROM dual) v$session";

			_editor.Text.ShouldBe(expectedResult);
			_editor.CaretOffset.ShouldBe(39);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestGenerateCustomTypeCSharpWrapperClassCommand()
		{
			const string statementText = @"SELECT SYS.ODCIARGDESC() FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 11;

			CanExecuteCommand(OracleCommands.GenerateCustomTypeCSharpWrapperClass).ShouldBe(true);
			ExecuteCommand(OracleCommands.GenerateCustomTypeCSharpWrapperClass);

			const string expectedResult =
@"using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

[OracleCustomTypeMapping(""SYS.ODCIARGDESC"")]
public class SYS_ODCIARGDESC : CustomTypeBase<SYS_ODCIARGDESC>
{
	[OracleObjectMapping(""ARGTYPE"")]
	public Oracle.DataAccess.Types.OracleDecimal ARGTYPE;
	[OracleObjectMapping(""TABLENAME"")]
	public System.String TABLENAME;
	[OracleObjectMapping(""TABLESCHEMA"")]
	public System.String TABLESCHEMA;
	[OracleObjectMapping(""COLNAME"")]
	public System.String COLNAME;
	[OracleObjectMapping(""TABLEPARTITIONLOWER"")]
	public System.String TABLEPARTITIONLOWER;
	[OracleObjectMapping(""TABLEPARTITIONUPPER"")]
	public System.String TABLEPARTITIONUPPER;
	[OracleObjectMapping(""CARDINALITY"")]
	public Oracle.DataAccess.Types.OracleDecimal CARDINALITY;

	public override void FromCustomObject(OracleConnection connection, IntPtr pointerUdt)
	{
		OracleUdt.SetValue(connection, pointerUdt, ""ARGTYPE"", ARGTYPE);
		OracleUdt.SetValue(connection, pointerUdt, ""TABLENAME"", TABLENAME);
		OracleUdt.SetValue(connection, pointerUdt, ""TABLESCHEMA"", TABLESCHEMA);
		OracleUdt.SetValue(connection, pointerUdt, ""COLNAME"", COLNAME);
		OracleUdt.SetValue(connection, pointerUdt, ""TABLEPARTITIONLOWER"", TABLEPARTITIONLOWER);
		OracleUdt.SetValue(connection, pointerUdt, ""TABLEPARTITIONUPPER"", TABLEPARTITIONUPPER);
		OracleUdt.SetValue(connection, pointerUdt, ""CARDINALITY"", CARDINALITY);
	}

	public override void ToCustomObject(OracleConnection connection, IntPtr pointerUdt)
	{
		ARGTYPE = (Oracle.DataAccess.Types.OracleDecimal)OracleUdt.GetValue(connection, pointerUdt, ""ARGTYPE"");
		TABLENAME = (System.String)OracleUdt.GetValue(connection, pointerUdt, ""TABLENAME"");
		TABLESCHEMA = (System.String)OracleUdt.GetValue(connection, pointerUdt, ""TABLESCHEMA"");
		COLNAME = (System.String)OracleUdt.GetValue(connection, pointerUdt, ""COLNAME"");
		TABLEPARTITIONLOWER = (System.String)OracleUdt.GetValue(connection, pointerUdt, ""TABLEPARTITIONLOWER"");
		TABLEPARTITIONUPPER = (System.String)OracleUdt.GetValue(connection, pointerUdt, ""TABLEPARTITIONUPPER"");
		CARDINALITY = (Oracle.DataAccess.Types.OracleDecimal)OracleUdt.GetValue(connection, pointerUdt, ""CARDINALITY"");
	}
}

public abstract class CustomTypeBase<T> : IOracleCustomType, IOracleCustomTypeFactory, INullable where T : CustomTypeBase<T>, new()
{
	private bool _isNull;
	
	public IOracleCustomType CreateObject()
	{
		return new T();
	}

	public abstract void FromCustomObject(OracleConnection connection, IntPtr pointerUdt);

	public abstract void ToCustomObject(OracleConnection connection, IntPtr pointerUdt);

	public bool IsNull
	{
		get { return this._isNull; }
	}

	public static T Null
	{
		get { return new T { _isNull = true }; }
	}
}
";

			var result = Clipboard.GetText();
			result.ShouldBe(expectedResult);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestGenerateCustomCollectionTypeCSharpWrapperClassCommand()
		{
			const string statementText = @"SELECT SYS.ODCIARGDESCLIST() FROM DUAL";
			_editor.Text = statementText;
			_editor.CaretOffset = 11;

			CanExecuteCommand(OracleCommands.GenerateCustomTypeCSharpWrapperClass).ShouldBe(true);
			ExecuteCommand(OracleCommands.GenerateCustomTypeCSharpWrapperClass);

			const string expectedResult =
@"using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

[OracleCustomTypeMapping(""SYS.ODCIARGDESCLIST"")]
public class SYS_ODCIARGDESCLIST : CustomCollectionTypeBase<SYS_ODCIARGDESCLIST, SYS_ODCIARGDESC>
{
}

public abstract class CustomCollectionTypeBase<TType, TValue> : CustomTypeBase<TType>, IOracleArrayTypeFactory where TType : CustomTypeBase<TType>, new()
{
	[OracleArrayMapping()]
	public TValue[] Values;

	public override void FromCustomObject(OracleConnection connection, IntPtr pointerUdt)
	{
		OracleUdt.SetValue(connection, pointerUdt, 0, Values);
	}

	public override void ToCustomObject(OracleConnection connection, IntPtr pointerUdt)
	{
		Values = (TValue[])OracleUdt.GetValue(connection, pointerUdt, 0);
	}

	public Array CreateArray(int numElems)
	{
		return new TValue[numElems];
	}

	public Array CreateStatusArray(int numElems)
	{
		return null;
	}
}

public abstract class CustomTypeBase<T> : IOracleCustomType, IOracleCustomTypeFactory, INullable where T : CustomTypeBase<T>, new()
{
	private bool _isNull;
	
	public IOracleCustomType CreateObject()
	{
		return new T();
	}

	public abstract void FromCustomObject(OracleConnection connection, IntPtr pointerUdt);

	public abstract void ToCustomObject(OracleConnection connection, IntPtr pointerUdt);

	public bool IsNull
	{
		get { return this._isNull; }
	}

	public static T Null
	{
		get { return new T { _isNull = true }; }
	}
}
";

			var result = Clipboard.GetText();
			result.ShouldBe(expectedResult);
		}
	}
}
