using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using ICSharpCode.AvalonEdit;
using NUnit.Framework;
using Shouldly;
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
	)";

		private TextEditor _editor;

		[SetUp]
		public void SetUp()
		{
			_editor = new TextEditor();
		}

		private OracleCommandBase InitializeCommand<TCommand>(string statementText, int cursorPosition, string commandParameter, bool isValidParameter = true) where TCommand : OracleCommandBase
		{
			var statement = (OracleStatement)Parser.Parse(statementText).Single();
			var currentNode = statement.GetNodeAtPosition(cursorPosition);
			var semanticModel = new OracleStatementSemanticModel(statementText, statement, TestFixture.DatabaseModel);

			var settingsProvider = new TestCommandSettings(commandParameter, isValidParameter);
			var commandType = typeof(TCommand);
			var parameters = new List<object> { semanticModel, currentNode };
			if (typeof(OracleConfigurableCommandBase).IsAssignableFrom(commandType))
			{
				parameters.Add(settingsProvider);
			} 

			return (OracleCommandBase)Activator.CreateInstance(commandType, parameters.ToArray());
		}

		[Test(Description = @""), STAThread]
		public void TestBasicAddAliasCommand()
		{
			_editor.Text = @"SELECT SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION";

			var command = InitializeCommand<AddAliasCommand>(_editor.Text, 87, "S");
			var canExecute = command.CanExecute(null);
			canExecute.ShouldBe(true);

			command.Execute(_editor);

			_editor.Text.ShouldBe(@"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION S");
		}

		[Test(Description = @""), STAThread]
		public void TestAddAliasCommandAtTableWithAlias()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME FROM SELECTION S";

			var command = InitializeCommand<AddAliasCommand>(_editor.Text, 70, "S");
			var canExecute = command.CanExecute(null);

			canExecute.ShouldBe(false);
		}

		[Test(Description = @""), STAThread]
		public void TestAddAliasCommandWithWhereGroupByAndHavingClauses()
		{
			_editor.Text = "SELECT SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION WHERE SELECTION.NAME = NAME GROUP BY SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(SELECTION.SELECTION_ID) = COUNT(SELECTION_ID)";
			var command = InitializeCommand<AddAliasCommand>(_editor.Text, 60, "S");
			var canExecute = command.CanExecute(null);
			canExecute.ShouldBe(true);

			command.Execute(_editor);

			_editor.Text.ShouldBe("SELECT S.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION S WHERE S.NAME = NAME GROUP BY S.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(S.SELECTION_ID) = COUNT(SELECTION_ID)");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicWrapAsSubqueryCommand()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S";

			var command = InitializeCommand<WrapAsSubqueryCommand>(_editor.Text, 0, "SUB");
			command.Execute(_editor);

			_editor.Text.ShouldBe(@"SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME FROM (SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S) SUB");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicWrapAsCommonTableExpressionCommand()
		{
			_editor.Text = "SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			var command = InitializeCommand<WrapAsCommonTableExpressionCommand>(_editor.Text, 0, "MYQUERY");
			command.Execute(_editor);

			_editor.Text.ShouldBe(@"WITH MYQUERY AS (SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT MYCOLUMN, COLUMN3 FROM MYQUERY");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicWrapAsCommonTableExpressionCommandWithExistingCommonTableExpressionAndWhiteSpace()
		{
			_editor.Text = "\t\t            WITH OLDQUERY AS (SELECT OLD FROM OLD) SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			var command = InitializeCommand<WrapAsCommonTableExpressionCommand>(_editor.Text, 55, "NEWQUERY");
			command.Execute(_editor);

			_editor.Text.ShouldBe("\t\t            WITH OLDQUERY AS (SELECT OLD FROM OLD), NEWQUERY AS (SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT MYCOLUMN, COLUMN3 FROM NEWQUERY");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicToggleQuotedNotationCommandOn()
		{
			_editor.Text = "SELECT \"PUBLIC\".DUAL.DUMMY, S.PROJECT_ID FROM SELECTION S, \"PUBLIC\".DUAL";
			var command = InitializeCommand<ToggleQuotedNotationCommand>(_editor.Text, 0, null);
			command.Execute(_editor);

			_editor.Text.ShouldBe("SELECT \"PUBLIC\".\"DUAL\".\"DUMMY\", \"S\".\"PROJECT_ID\" FROM \"SELECTION\" \"S\", \"PUBLIC\".\"DUAL\"");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicToggleQuotedNotationCommandOff()
		{
			_editor.Text = "SELECT \"PUBLIC\".\"DUAL\".\"DUMMY\", \"S\".\"PROJECT_ID\" FROM \"SELECTION\" \"S\", \"PUBLIC\".\"DUAL\"";
			var command = InitializeCommand<ToggleQuotedNotationCommand>(_editor.Text, 0, null);
			command.Execute(_editor);

			_editor.Text.ShouldBe("SELECT \"PUBLIC\".DUAL.DUMMY, S.PROJECT_ID FROM SELECTION S, \"PUBLIC\".DUAL");
		}

		[Test(Description = @""), STAThread]
		public void TestBasicToggleQuotedNotationCommandWithSubqueryWithQuotedNotation()
		{
			_editor.Text = "SELECT DUMMY FROM (SELECT \"DUMMY\" FROM \"DUAL\")";
			var command = InitializeCommand<ToggleQuotedNotationCommand>(_editor.Text, 0, null);
			command.Execute(_editor);

			_editor.Text.ShouldBe("SELECT \"DUMMY\" FROM (SELECT \"DUMMY\" FROM \"DUAL\")");
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectUsages()
		{
			const string statementText = "SELECT \"SELECTION\".RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION WHERE SELECTION.NAME = NAME GROUP BY SELECTION.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(SELECTION.SELECTION_ID) = COUNT(SELECTION_ID)";
			var command = new FindUsagesCommand(statementText, 8, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
			foundSegments.Count.ShouldBe(5);
			foundSegments[0].Length.ShouldBe("\"SELECTION\"".Length);
			foundSegments[1].Length.ShouldBe("SELECTION".Length);
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectWithAliasUsages()
		{
			const string statementText = "SELECT S.RESPONDENTBUCKET_ID, PROJECT_ID FROM SELECTION \"S\" WHERE S.NAME = NAME GROUP BY S.RESPONDENTBUCKET_ID, PROJECT_ID HAVING COUNT(S.SELECTION_ID) = COUNT(SELECTION_ID)";
			var command = new FindUsagesCommand(statementText, 56, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
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
			var command = new FindUsagesCommand(statementText, 9, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
			foundSegments.Count.ShouldBe(4);
			foundSegments.ForEach(s => s.Length.ShouldBe("HUSQVIK".Length));
		}

		[Test(Description = @""), STAThread]
		public void TestBasicFindColumnUsages()
		{
			var command = new FindUsagesCommand(FindUsagesStatementText, 11, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
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
			var command = new FindUsagesCommand(FindUsagesStatementText, 40, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
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
			var command = new FindUsagesCommand(FindUsagesStatementText, 25, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
			foundSegments.Count.ShouldBe(2);
			foundSegments[0].IndextStart.ShouldBe(17);
			foundSegments[0].Length.ShouldBe(16);
			foundSegments[1].IndextStart.ShouldBe(152);
			foundSegments[1].Length.ShouldBe(16);
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfIndirectColumnReferenceAtColumnNode()
		{
			var command = new FindUsagesCommand(FindUsagesStatementText, 121, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
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
			var command = new FindUsagesCommand(FindUsagesStatementText, 80, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(71);
			foundSegments[1].IndextStart.ShouldBe(222);
			foundSegments[2].IndextStart.ShouldBe(375);
			foundSegments.ForEach(s => s.Length.ShouldBe(15));
		}

		[Test(Description = @""), STAThread]
		public void TestFindColumnUsagesOfComputedColumnAtDefinition()
		{
			var command = new FindUsagesCommand(FindUsagesStatementText, 382, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
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
			var command = new FindUsagesCommand(statement, 6, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(58);
			foundSegments[2].IndextStart.ShouldBe(72);
			foundSegments.ForEach(s => s.Length.ShouldBe(3));
		}

		[Test(Description = @""), STAThread]
		public void TestFindObjectUsagesAtCommonTableExpressionUsage()
		{
			const string statement = "WITH CTE AS (SELECT SELECTION.NAME FROM SELECTION) SELECT CTE.NAME FROM CTE";
			var command = new FindUsagesCommand(statement, 72, TestFixture.DatabaseModel);
			var foundSegments = new List<TextSegment>();
			command.Execute(foundSegments);

			foundSegments = foundSegments.OrderBy(s => s.IndextStart).ToList();
			foundSegments.Count.ShouldBe(3);
			foundSegments[0].IndextStart.ShouldBe(5);
			foundSegments[1].IndextStart.ShouldBe(58);
			foundSegments[2].IndextStart.ShouldBe(72);
			foundSegments.ForEach(s => s.Length.ShouldBe(3));
		}

		//WITH CTE AS (SELECT SELECTION.NAME FROM SELECTION) SELECT CTE.NAME FROM CTE

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
	}
}