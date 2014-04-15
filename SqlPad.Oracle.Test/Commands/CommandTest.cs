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
		public void TestBasicWrapAsSubqueryCommand()
		{
			_editor.Text = @"SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S";

			var command = InitializeCommand<WrapAsSubqueryCommand>(_editor.Text, 0, "SUB");
			command.Execute(_editor);

			_editor.Text.ShouldBe(@"SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME FROM (SELECT S.RESPONDENTBUCKET_ID, S.SELECTION_ID, PROJECT_ID, NAME, 1 FROM SELECTION S) SUB");
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