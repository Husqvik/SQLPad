using System;
using System.Collections.Generic;
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
			var semanticModel = new OracleStatementSemanticModel(statementText, statement, DatabaseModelFake.Instance);

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