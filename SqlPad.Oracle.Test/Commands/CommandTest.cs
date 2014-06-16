using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

namespace SqlPad.Oracle.Test.Commands
{
	[TestFixture]
	public class CommandTest
	{
		private static readonly OracleSqlParser Parser = new OracleSqlParser();

		private const string FindUsagesStatementText =
@"SELECT
	NAME,
	TARGETGROUP_NAME,
	PNAME,
	RESPONDENTBUCKET_NAME,
	CONSTANT_COLUMN
FROM
	(SELECT
		NAME,
		TARGETGROUP_NAME || TARGETGROUP_NAME TARGETGROUP_NAME,
		PROJECT_NAME PNAME,
		RESPONDENTBUCKET_NAME,
		CONSTANT_COLUMN
	FROM
		(SELECT
			S.NAME,
			TG.NAME TARGETGROUP_NAME,
			P.NAME PROJECT_NAME,
			RB.NAME RESPONDENTBUCKET_NAME,
			'My column1' CONSTANT_COLUMN,
			'My column2'
		FROM
			HUSQVIK.SELECTION S
			LEFT JOIN RESPONDENTBUCKET RB ON S.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID
			LEFT JOIN TARGETGROUP TG ON RB.TARGETGROUP_ID = TG.TARGETGROUP_ID AND RB.NAME = TG.NAME
			JOIN PROJECT P ON S.PROJECT_ID = P.PROJECT_ID
		WHERE
			TG.NAME IN ('X1', 'X2') OR S.NAME IS NOT NULL OR P.NAME <> ''
		)
	) TMP
ORDER BY
	TMP.RESPONDENTBUCKET_NAME";

		private const string FindFunctionUsagesStatementText =
@"SELECT
	COUNT(*) OVER () CNT1,
	COUNT(1) OVER () CNT2,
	FIRST_VALUE(DUMMY) IGNORE NULLS OVER () FIRST_VAL1,
	FIRST_VALUE(DUMMY) OVER () FIRST_VAL2,
	COALESCE(DUMMY, 1) COALESCE1,
	COALESCE(DUMMY, 1) COALESCE2,
	TO_CHAR(0) C1,
	TO_CHAR(0) C2
FROM
	DUAL";

		private const string FindLiteralUsagesStatementText =
@"SELECT
	:BV,
	'123',
	'456',
	'123',
	'456',
	123,
	456,
	123,
	456,
	:BV
FROM
	SELECTION
WHERE
	'456' != '123'
	AND 1 = :BV";

		private TextEditor _editor;

		[SetUp]
		public void SetUp()
		{
			_editor = new TextEditor();
		}

		private class TestCommandSettings : ICommandSettingsProvider
		{
			private readonly bool _isValueValid;

			public TestCommandSettings(string value, bool isValueValid = true)
			{
				Settings = new CommandSettingsModel { Value = value };
				_isValueValid = isValueValid;
			}

			public bool GetSettings()
			{
				return _isValueValid;
			}

			public CommandSettingsModel Settings { get; private set; }
		}

		private CommandExecutionContext CreateGenericExecutionContext()
		{
			var statements = Parser.Parse(_editor.Text);
			return CommandExecutionContext.Create(_editor, statements, TestFixture.DatabaseModel);
		}

		private OracleCommandExecutionContext CreateOracleExecutionContext()
		{
			var childContext = CreateGenericExecutionContext();
			var semanticModel = new OracleStatementSemanticModel(childContext.StatementText, (OracleStatement)childContext.Statements.Single(), (OracleDatabaseModelBase)childContext.DatabaseModel);
			return OracleCommandExecutionContext.Create(childContext, semanticModel);
		}

		private bool CanExecuteOracleCommand(CommandExecutionHandler executionHandler)
		{
			var executionContext = CreateOracleExecutionContext();
			return executionHandler.CanExecuteHandler(executionContext);
		}

		private void ExecuteOracleCommand(CommandExecutionHandler executionHandler, string commandParameter = null, bool isValidParameter = true)
		{
			var executionContext = CreateOracleExecutionContext();
			AddSettingsProvider(executionContext, commandParameter, isValidParameter);

			ExecuteCommand(executionHandler, executionContext);
		}

		private void ExecuteGenericCommand(CommandExecutionHandler executionHandler, string commandParameter = null, bool isValidParameter = true)
		{
			var executionContext = CreateGenericExecutionContext();
			AddSettingsProvider(executionContext, commandParameter, isValidParameter);

			ExecuteCommand(executionHandler, executionContext);
		}

		private void AddSettingsProvider(CommandExecutionContext executionContext, string commandParameter, bool isValidParameter)
		{
			if (commandParameter != null)
			{
				executionContext.SettingsProvider = new TestCommandSettings(commandParameter, isValidParameter);
			}
		}

		private void ExecuteCommand(CommandExecutionHandler executionHandler, CommandExecutionContext executionContext)
		{
			executionHandler.ExecutionHandler(executionContext);
			_editor.ReplaceTextSegments(executionContext.SegmentsToReplace);
		}

		[Test(Description = @""), STAThread]
		public void TestBasicAddAliasCommand()
		{
			_editor.Text = @"SELECT SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION";
			_editor.CaretOffset = 87;

			CanExecuteOracleCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.AddAlias, "S");

			_editor.Text.ShouldBe(@"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION S");
		}

		[Test(Description = @""), STAThread]
		public void TestAddAliasCommandAtTableWithAlias()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION S";
			_editor.CaretOffset = 70;

			CanExecuteOracleCommand(OracleCommands.AddAlias).ShouldBe(false);
		}

		[Test(Description = @""), STAThread]
		public void TestAddAliasCommandWithWhereGroupByAndHavingClauses()
		{
			_editor.Text = "SELECT SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION WHERE SELECTION.NAME = NAME GROUP BY SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(SELECTION.SELECTION_ID) = COUNT(SELECTION_ID)";
			_editor.CaretOffset = 60;

			CanExecuteOracleCommand(OracleCommands.AddAlias).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.AddAlias, "S");

			_editor.Text.ShouldBe("SELECT S.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION S WHERE S.NAME = NAME GROUP BY S.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(S.SELECTION_ID) = COUNT(SELECTION_ID)");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicWrapAsInlineViewCommand()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S";
			_editor.CaretOffset = 0;

			ExecuteOracleCommand(OracleCommands.WrapAsInlineView, "IV");

			_editor.Text.ShouldBe(@"SELECT IV.RESPONDENTBUCKET_ID, IV.SELECTION_ID, IV.PROJECT_ID, IV.NAME FROM (SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S) IV");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicWrapAsCommonTableExpressionCommand()
		{
			_editor.Text = "SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			_editor.CaretOffset = 0;

			ExecuteOracleCommand(OracleCommands.WrapAsCommonTableExpression, "MYQUERY");

			_editor.Text.ShouldBe(@"WITH MYQUERY AS (SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT MYQUERY.MYCOLUMN, MYQUERY.COLUMN3 FROM MYQUERY");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicWrapAsCommonTableExpressionCommandWithExistingCommonTableExpressionAndWhiteSpace()
		{
			_editor.Text = "\t\t            WITH OLDQUERY AS (SELECT OLD FROM OLD) SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			_editor.CaretOffset = 55;

			ExecuteOracleCommand(OracleCommands.WrapAsCommonTableExpression, "NEWQUERY");

			_editor.Text.ShouldBe("\t\t            WITH OLDQUERY AS (SELECT OLD FROM OLD), NEWQUERY AS (SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT NEWQUERY.MYCOLUMN, NEWQUERY.COLUMN3 FROM NEWQUERY");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicToggleQuotedNotationCommandOn()
		{
			_editor.Text = "SELECT \"PUBLIC\".DUAL.DUMMY, S.PROJECT_ID FROM SELECTION S, \"PUBLIC\".DUAL";
			_editor.CaretOffset = 0;

			ExecuteOracleCommand(OracleCommands.ToggleQuotedNotation);

			_editor.Text.ShouldBe("SELECT \"PUBLIC\".\"DUAL\".\"DUMMY\", \"S\".\"PROJECT_ID\" FROM \"SELECTION\" \"S\", \"PUBLIC\".\"DUAL\"");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicToggleQuotedNotationCommandOff()
		{
			_editor.Text = "SELECT \"PUBLIC\".\"DUAL\".\"DUMMY\", \"S\".\"PROJECT_ID\" FROM \"SELECTION\" \"S\", \"PUBLIC\".\"DUAL\"";
			_editor.CaretOffset = 0;

			ExecuteOracleCommand(OracleCommands.ToggleQuotedNotation);

			_editor.Text.ShouldBe("SELECT \"PUBLIC\".DUAL.DUMMY, S.PROJECT_ID FROM SELECTION S, \"PUBLIC\".DUAL");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicToggleQuotedNotationCommandWithSubqueryWithQuotedNotation()
		{
			_editor.Text = "SELECT DUMMY FROM (SELECT \"DUMMY\" FROM \"DUAL\")";
			_editor.CaretOffset = 0;

			ExecuteOracleCommand(OracleCommands.ToggleQuotedNotation);

			_editor.Text.ShouldBe("SELECT \"DUMMY\" FROM (SELECT \"DUMMY\" FROM \"DUAL\")");
		}

		private List<TextSegment> FindUsagesOrdered(string statementText, int currentPosition)
		{
			var executionContext = new CommandExecutionContext(statementText, currentPosition, Parser.Parse(statementText), TestFixture.DatabaseModel);
			FindUsagesCommand.FindUsages.ExecutionHandler(executionContext);
			return executionContext.SegmentsToReplace.OrderBy(s => s.IndextStart).ToList();
		}
			
		[Test(Description = @""), STAThread]
		public void TestFindObjectUsages()
		{
			const string statementText = "SELECT \"SELECTION\".RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION WHERE SELECTION.NAME = NAME GROUP BY SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(SELECTION.SELECTION_ID) = COUNT(SELECTION_ID)";

			var foundSegments = FindUsagesOrdered(statementText, 8);
			foundSegments.Count.ShouldBe(5);
			foundSegments[0].Length.ShouldBe("\"SELECTION\"".Length);
			foundSegments[1].Length.ShouldBe("SELECTION".Length);
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectWithAliasUsages()
		{
			const string statementText = "SELECT S.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION \"S\" WHERE S.NAME = NAME GROUP BY S.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(S.SELECTION_ID) = COUNT(SELECTION_ID)";

			var foundSegments = FindUsagesOrdered(statementText, 56);
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].Length.ShouldBe("S".Length);
			foundSegments[1].Length.ShouldBe("SELECTION".Length);
			foundSegments[2].Length.ShouldBe("\"S\"".Length);
			foundSegments[3].Length.ShouldBe("S".Length);
		}

		[Test(Description = @""), STAThread]
		public void TestFindSchemaUsages()
		{
			const string statementText = "SELECT HUSQVIK.SELECTION.PROJECT_ID FROM (SELECT HUSQVIK.SELECTION.NAME FROM HUSQVIK.SELECTION), HUSQVIK.SELECTION";

			var foundSegments = FindUsagesOrdered(statementText, 9);
			foundSegments.Count.ShouldBe(4);
			foundSegments.ForEach(s => s.Length.ShouldBe("HUSQVIK".Length));
		}

		[Test(Description = @""), STAThread]
		public void TestBasicFindColumnUsages()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 11);
			foundSegments.Count.ShouldBe(4);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(106);
			foundSegments[2].IndextStart.ShouldBe(262);
			foundSegments[3].IndextStart.ShouldBe(709);
			foundSegments.ForEach(s => s.Length.ShouldBe(4));
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfColumnAliases()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 40);
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].IndextStart.ShouldBe(37);
			foundSegments[0].Length.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(173);
			foundSegments[1].Length.ShouldBe(12);
			foundSegments[2].IndextStart.ShouldBe(186);
			foundSegments[2].Length.ShouldBe(5);
			foundSegments[3].IndextStart.ShouldBe(304);
			foundSegments[3].Length.ShouldBe(4);
			foundSegments[4].IndextStart.ShouldBe(309);
			foundSegments[4].Length.ShouldBe(12);
			foundSegments[5].IndextStart.ShouldBe(731);
			foundSegments[5].Length.ShouldBe(4);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfIndirectColumnReferenceAtAliasNode()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 25);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(17);
			foundSegments[0].Length.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(152);
			foundSegments[1].Length.ShouldBe(16);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfIndirectColumnReferenceAtColumnNode()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 121);
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].IndextStart.ShouldBe(115);
			foundSegments[0].Length.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(135);
			foundSegments[1].Length.ShouldBe(16);
			foundSegments[2].IndextStart.ShouldBe(275);
			foundSegments[2].Length.ShouldBe(4);
			foundSegments[3].IndextStart.ShouldBe(280);
			foundSegments[3].Length.ShouldBe(16);
			foundSegments[4].IndextStart.ShouldBe(612);
			foundSegments[4].Length.ShouldBe(4);
			foundSegments[5].IndextStart.ShouldBe(683);
			foundSegments[5].Length.ShouldBe(4);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfComputedColumnAtUsage()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 80);
			ValidateCommonResults3(foundSegments);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfComputedColumnAtDefinition()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 382);
			ValidateCommonResults3(foundSegments);
		}

		private void ValidateCommonResults3(List<TextSegment> foundSegments)
		{
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(71);
			foundSegments[1].IndextStart.ShouldBe(222);
			foundSegments[2].IndextStart.ShouldBe(375);
			foundSegments.ForEach(s => s.Length.ShouldBe(15));
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectUsagesAtCommonTableExpressionDefinition()
		{
			const string statement = "WITH CTE AS (SELECT SELECTION.NAME FROM SELECTION) SELECT CTE.NAME FROM CTE";

			var foundSegments = FindUsagesOrdered(statement, 6);
			ValidateCommonResults2(foundSegments);
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectUsagesAtCommonTableExpressionUsage()
		{
			const string statement = "WITH CTE AS (SELECT SELECTION.NAME FROM SELECTION) SELECT CTE.NAME FROM CTE";
			
			var foundSegments = FindUsagesOrdered(statement, 72);
			ValidateCommonResults2(foundSegments);
		}

		private void ValidateCommonResults2(List<TextSegment> foundSegments)
		{
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(58);
			foundSegments[2].IndextStart.ShouldBe(72);
			foundSegments.ForEach(s => s.Length.ShouldBe(3));
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesAtJoinCondition()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 602);
			ValidateCommonResults1(foundSegments);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesInOrderByClause()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 771);
			ValidateCommonResults1(foundSegments);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesAtAliasDefinition()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 350);
			ValidateCommonResults1(foundSegments);
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectUsageInOrderByClause()
		{
			var foundSegments = FindUsagesOrdered(FindUsagesStatementText, 767);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(751);
			foundSegments[0].Length.ShouldBe(3);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[1].IndextStart.ShouldBe(767);
			foundSegments[1].Length.ShouldBe(3);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		private void ValidateCommonResults1(IList<TextSegment> foundSegments)
		{
			foundSegments.Count.ShouldBe(6);
			foundSegments[0].IndextStart.ShouldBe(46);
			foundSegments[0].Length.ShouldBe(21);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[1].IndextStart.ShouldBe(196);
			foundSegments[1].Length.ShouldBe(21);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[2].IndextStart.ShouldBe(330);
			foundSegments[2].Length.ShouldBe(4);
			foundSegments[2].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[3].IndextStart.ShouldBe(335);
			foundSegments[3].Length.ShouldBe(21);
			foundSegments[3].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[4].IndextStart.ShouldBe(602);
			foundSegments[4].Length.ShouldBe(4);
			foundSegments[4].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[5].IndextStart.ShouldBe(771);
			foundSegments[5].Length.ShouldBe(21);
			foundSegments[5].DisplayOptions.ShouldBe(DisplayOptions.Usage);
		}

		[Test(Description = @""), STAThread]
		public void TestAsteriskNotHighlightedWhenFindUsages()
		{
			const string statement = "SELECT NAME FROM (SELECT * FROM (SELECT NAME FROM SELECTION))";

			var foundSegments = FindUsagesOrdered(statement, 7);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(7);
			foundSegments[1].IndextStart.ShouldBe(40);
		}

		[Test(Description = @""), STAThread]
		public void TestWrapCommonTableExpressionIntoAnotherCommonTableExpression()
		{
			_editor.Text = "WITH CTE1 AS (SELECT NAME FROM SELECTION) SELECT NAME FROM CTE1";
			_editor.CaretOffset = 15;

			ExecuteOracleCommand(OracleCommands.WrapAsCommonTableExpression, "CTE2");

			_editor.Text.ShouldBe(@"WITH CTE2 AS (SELECT NAME FROM SELECTION), CTE1 AS (SELECT CTE2.NAME FROM CTE2) SELECT NAME FROM CTE1");
		}

		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandWithObjectReference()
		{
			_editor.Text = "SELECT SELECTION.*, PROJECT.* FROM SELECTION, PROJECT";
			_editor.CaretOffset = 28;
			
			ExecuteOracleCommand(OracleCommands.ExpandAsterisk, String.Empty);

			_editor.Text.ShouldBe("SELECT SELECTION.*, PROJECT.NAME, PROJECT.PROJECT_ID FROM SELECTION, PROJECT");
		}

		[Test(Description = @""), STAThread]
		public void TestExpandAsteriskCommandWithAllColumns()
		{
			_editor.Text = "SELECT * FROM PROJECT, PROJECT P";
			_editor.CaretOffset = 7;

			ExecuteOracleCommand(OracleCommands.ExpandAsterisk, String.Empty);

			_editor.Text.ShouldBe("SELECT PROJECT.NAME, PROJECT.PROJECT_ID, P.NAME, P.PROJECT_ID FROM PROJECT, PROJECT P");
		}

		[Test(Description = @""), STAThread]
		public void TestFindGrammarSpecificFunctionUsages()
		{
			var foundSegments = FindUsagesOrdered(FindFunctionUsagesStatementText, 9);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(9);
			foundSegments[1].IndextStart.ShouldBe(34);
			foundSegments.ForEach(s => s.Length.ShouldBe(5));
		}

		[Test(Description = @""), STAThread]
		public void TestFindGenericSqlFunctionUsages()
		{
			var foundSegments = FindUsagesOrdered(FindFunctionUsagesStatementText, 154);
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(154);
			foundSegments[1].IndextStart.ShouldBe(186);
			foundSegments.ForEach(s => s.Length.ShouldBe(8));
		}

		[Test(Description = @""), STAThread]
		public void TestFindLiteralUsages()
		{
			var foundSegments = FindUsagesOrdered(FindLiteralUsagesStatementText, 17);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(34);
			foundSegments[2].IndextStart.ShouldBe(120);
			foundSegments.ForEach(s => s.Length.ShouldBe(5));
		}

		[Test(Description = @""), STAThread]
		public void TestFindBindVariableUsages()
		{
			var foundSegments = FindUsagesOrdered(FindLiteralUsagesStatementText, 10);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(10);
			foundSegments[1].IndextStart.ShouldBe(81);
			foundSegments[2].IndextStart.ShouldBe(137);
			foundSegments.ForEach(s => s.Length.ShouldBe(2));
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesWithAliasedColumnWithInvalidReference()
		{
			const string statement = "SELECT CPU_SECONDS X FROM (SELECT CPU_TIME / 1000000 CPU_SECONDS FROM V$SESSION)";

			var foundSegments = FindUsagesOrdered(statement, 10);
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(7);
			foundSegments[0].Length.ShouldBe(11);
			foundSegments[0].DisplayOptions.ShouldBe(DisplayOptions.Usage);
			foundSegments[1].IndextStart.ShouldBe(19);
			foundSegments[1].Length.ShouldBe(1);
			foundSegments[1].DisplayOptions.ShouldBe(DisplayOptions.Definition);
			foundSegments[2].IndextStart.ShouldBe(53);
			foundSegments[2].DisplayOptions.ShouldBe(DisplayOptions.Definition);
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommand()
		{
			_editor.Text = @"SELECT IV.TEST_COLUMN || ' ADDED' FROM PROJECT, (SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' TEST_COLUMN FROM SELECTION) IV, RESPONDENTBUCKET";
			_editor.CaretOffset = 50;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' || ' ADDED' FROM PROJECT, SELECTION, RESPONDENTBUCKET");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithWhereClause()
		{
			_editor.Text = @"SELECT IV.TEST_COLUMN || ' ADDED' FROM PROJECT, (SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' TEST_COLUMN FROM SELECTION WHERE SELECTION_ID = 123) IV, RESPONDENTBUCKET";
			_editor.CaretOffset = 50;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT SELECTION.NAME || ' FROM INLINE_VIEW ' || ' ADDED' FROM PROJECT, SELECTION, RESPONDENTBUCKET WHERE SELECTION_ID = 123");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithCombinedWhereClause()
		{
			_editor.Text = @"SELECT * FROM (SELECT * FROM SELECTION WHERE SELECTION_ID = 123) IV, RESPONDENTBUCKET RB WHERE RB.RESPONDENTBUCKET_ID = 456";
			_editor.CaretOffset = 17;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION, RESPONDENTBUCKET RB WHERE RB.RESPONDENTBUCKET_ID = 456 AND SELECTION_ID = 123");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithAsterisk()
		{
			_editor.Text = @"SELECT IV.* FROM (SELECT * FROM SELECTION, RESPONDENTBUCKET) IV";
			_editor.CaretOffset = 18;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION, RESPONDENTBUCKET");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithObjectAsteriskCombinedWithOtherColumn()
		{
			_editor.Text = @"SELECT IV.*, TARGETGROUP_ID FROM (SELECT 1 C1, SELECTION.*, 3 C3 FROM SELECTION) IV, RESPONDENTBUCKET";
			_editor.CaretOffset = 40;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT 1 C1, SELECTION.*, 3 C3, TARGETGROUP_ID FROM SELECTION, RESPONDENTBUCKET");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithWithInlineViewWithoutSpace()
		{
			_editor.Text = @"SELECT * FROM SELECTION JOIN(SELECT NAME FROM PROJECT) S ON SELECTION.NAME = S.NAME";
			_editor.CaretOffset = 30;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION JOIN PROJECT ON SELECTION.NAME = PROJECT.NAME");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithWithInlineViewWithObjectNamePrefix()
		{
			_editor.Text = @"SELECT * FROM SELECTION JOIN(SELECT PROJECT.NAME FROM PROJECT) S ON SELECTION.NAME = S.NAME";
			_editor.CaretOffset = 30;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION JOIN PROJECT ON SELECTION.NAME = PROJECT.NAME");
		}

		[Test(Description = @""), STAThread]
		public void TestUnnestCommandWithColumnExpressions()
		{
			_editor.Text = @"SELECT 'OuterPrefix' || IV.VAL || 'OuterPostfix' FROM (SELECT 'InnerPrefix' || (DUMMY || 'InnerPostfix') VAL FROM DUAL) IV";
			_editor.CaretOffset = 60;

			CanExecuteOracleCommand(OracleCommands.UnnestInlineView).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.UnnestInlineView);

			_editor.Text.ShouldBe("SELECT 'OuterPrefix' || 'InnerPrefix' || (DUAL.DUMMY || 'InnerPostfix') || 'OuterPostfix' FROM DUAL");
		}

		[Test(Description = @""), STAThread]
		public void TestSafeDeleteCommandAtObjectAlias()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, S.PROJECT_ID, S.NAME FROM SELECTION S";
			_editor.CaretOffset = 82;

			ExecuteGenericCommand(SafeDeleteCommand.SafeDelete);

			_editor.Text.ShouldBe("SELECT SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, SELECTION.PROJECT_ID, SELECTION.NAME FROM SELECTION ");
		}

		[Test(Description = @""), STAThread]
		public void TestModifyCaseCommandWithMultipleTerminalsSelected()
		{
			_editor.Text = @"select null, 'null' from selection";
			_editor.CaretOffset = 3;
			_editor.SelectionLength = 28;

			ExecuteGenericCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("selECT NULL, 'null' FROM SELECTion");

			ExecuteGenericCommand(ModifyCaseCommand.MakeLowerCase);

			_editor.Text.ShouldBe("select null, 'null' from selection");
		}

		[Test(Description = @""), STAThread]
		public void TestMakeUpperCaseCommandWithUnrecognizedGrammar()
		{
			_editor.Text = @"lot of invalid tokens preceding; select 'null' as ""null"" from dual and lot of invalid tokens following";
			_editor.CaretOffset = 0;
			_editor.SelectionLength = _editor.Text.Length;

			ExecuteGenericCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("LOT OF INVALID TOKENS PRECEDING; SELECT 'null' AS \"null\" FROM DUAL AND LOT OF INVALID TOKENS FOLLOWING");
		}

		[Test(Description = @""), STAThread]
		public void TestMakeUpperCaseCommandWithSingleCaseUnsafeToken()
		{
			_editor.Text = @"SELECT 'null' FROM DUAL";
			_editor.CaretOffset = 7;
			_editor.SelectionLength = 6;

			ExecuteGenericCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("SELECT 'NULL' FROM DUAL");
		}

		[Test(Description = @""), STAThread]
		public void TestMakeUpperCaseCommandWithCaseUnsafeTokenAsLastToken()
		{
			_editor.Text = @"select * from ""Accounts""";
			_editor.SelectAll();

			ExecuteGenericCommand(ModifyCaseCommand.MakeUpperCase);

			_editor.Text.ShouldBe("SELECT * FROM \"Accounts\"");
		}

		[Test(Description = @""), STAThread]
		public void TestAddToGroupByCommandWithoutExistingGroupByClause()
		{
			_editor.Text = @"SELECT SELECTION.PROJECT_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION";
			_editor.SelectionLength = 18;

			ExecuteOracleCommand(OracleCommands.AddToGroupByClause);

			_editor.Text.ShouldBe("SELECT SELECTION.PROJECT_ID, COUNT(*) PROJECT_SELECTIONS FROM SELECTION GROUP BY PROJECT_ID");
		}

		[Test(Description = @""), STAThread]
		public void TestToggleFullyQualifiedReferencesOn()
		{
			_editor.Text = @"SELECT SQLPAD_FUNCTION, RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME, SQLPAD.SQLPAD_FUNCTION(0), TO_CHAR('') FROM SELECTION";
			_editor.SelectionLength = 0;

			ExecuteOracleCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT HUSQVIK.SQLPAD_FUNCTION, HUSQVIK.SELECTION.RESPONDENTBUCKET_ID, HUSQVIK.SELECTION.SELECTION_ID, HUSQVIK.SELECTION.PROJECT_ID, HUSQVIK.SELECTION.NAME, HUSQVIK.SQLPAD.SQLPAD_FUNCTION(0), TO_CHAR('') FROM HUSQVIK.SELECTION");
		}

		[Test(Description = @""), STAThread]
		public void TestToggleFullyQualifiedReferencesWithAliasedTable()
		{
			_editor.Text = @"SELECT DUMMY, NAME FROM DUAL D, SELECTION S";
			_editor.SelectionLength = 0;

			ExecuteOracleCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT D.DUMMY, S.NAME FROM DUAL D, HUSQVIK.SELECTION S");
		}

		[Test(Description = @""), STAThread]
		public void TestToggleFullyQualifiedReferencesWithRowIdPseudoColumn()
		{
			_editor.Text = @"SELECT ROWID FROM DUAL";
			_editor.SelectionLength = 0;

			ExecuteOracleCommand(OracleCommands.ToggleFullyQualifiedReferences);

			_editor.Text.ShouldBe("SELECT DUAL.ROWID FROM DUAL");
		}

		[Test(Description = @""), STAThread]
		public void TestToggleFullyQualifiedReferencesOnFullyQualifiedSchemaFunction()
		{
			_editor.Text = @"SELECT HUSQVIK.SQLPAD_FUNCTION FROM SYS.DUAL";
			_editor.SelectionLength = 0;

			// TODO: Update when toogle off is implemented
			CanExecuteOracleCommand(OracleCommands.ToggleFullyQualifiedReferences).ShouldBe(false);
		}

		[Test(Description = @""), STAThread]
		public void TestToggleFullyQualifiedReferencesOnNonAliasedTableReference()
		{
			_editor.Text = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL)";
			_editor.SelectionLength = 0;

			// TODO: Update when toogle off is implemented
			CanExecuteOracleCommand(OracleCommands.ToggleFullyQualifiedReferences).ShouldBe(false);
		}

		[Test(Description = @""), STAThread]
		public void TestResolveAmbiguousColumnCommand()
		{
			_editor.Text = @"SELECT DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";
			_editor.CaretOffset = 12;

			var actions = new OracleContextActionProvider().GetContextActions(TestFixture.DatabaseModel, _editor.Text, _editor.CaretOffset).ToArray();

			actions.Length.ShouldBe(2);
			CanExecuteOracleCommand(actions[0].ExecutionHandler).ShouldBe(true);
			ExecuteOracleCommand(actions[0].ExecutionHandler);
			CanExecuteOracleCommand(actions[1].ExecutionHandler).ShouldBe(true);

			_editor.Text.ShouldBe(@"SELECT SYS.DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL");
		}

		[Test(Description = @""), STAThread]
		public void TestGenerateMissingColumnsCommand()
		{
			_editor.Text = @"SELECT C1, C2, NAME, C3 FROM SELECTION";
			_editor.CaretOffset = 0;

			CanExecuteOracleCommand(OracleCommands.GenerateMissingColumns).ShouldBe(true);
			ExecuteOracleCommand(OracleCommands.GenerateMissingColumns);

			_editor.Text.ShouldBe("SELECT C1, C2, NAME, C3 FROM SELECTION\r\n\r\nALTER TABLE HUSQVIK.SELECTION ADD\r\n(\r\n\tC1 VARCHAR2(100) NULL,\r\n\tC2 VARCHAR2(100) NULL,\r\n\tC3 VARCHAR2(100) NULL);\r\n");
		}
	}
}
