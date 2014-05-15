using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SqlPad.Oracle.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleContextActionProvider : IContextActionProvider
	{
		private static readonly IContextAction[] EmptyCollection = new IContextAction[0];
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();

		public ICollection<IContextAction> GetContextActions(IDatabaseModel databaseModel, string statementText, int cursorPosition, int selectionLength = 0)
		{
			return GetContextActions(databaseModel, SqlDocument.FromStatementCollection(_oracleParser.Parse(statementText)), cursorPosition, selectionLength);
		}

		public ICollection<IContextAction> GetContextActions(IDatabaseModel databaseModel, SqlDocument sqlDocument, int cursorPosition, int selectionLength = 0)
		{
			if (sqlDocument == null || sqlDocument.StatementCollection == null)
				return EmptyCollection;

			var currentTerminal = sqlDocument.StatementCollection.GetTerminalAtPosition(cursorPosition, n => Terminals.AllTerminals.Contains(n.Id));
			if (currentTerminal == null)
				return EmptyCollection;

			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)currentTerminal.Statement, (OracleDatabaseModel)databaseModel);
			var actionList = new List<IContextAction>();

			var addAliasCommand = new AddAliasCommand(semanticModel, currentTerminal);
			if (addAliasCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction(addAliasCommand.Title, addAliasCommand));
			}

			var wrapAsInlineViewCommand = new WrapAsInlineViewCommand(semanticModel, currentTerminal);
			if (wrapAsInlineViewCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction(wrapAsInlineViewCommand.Title, wrapAsInlineViewCommand));
			}

			var wrapAsCommonTableExpressionCommand = new WrapAsCommonTableExpressionCommand(semanticModel, currentTerminal);
			if (wrapAsCommonTableExpressionCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction(wrapAsCommonTableExpressionCommand.Title, wrapAsCommonTableExpressionCommand));
			}

			var toggleQuotedNotationCommand = new ToggleQuotedNotationCommand(semanticModel, currentTerminal);
			if (toggleQuotedNotationCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction(toggleQuotedNotationCommand.Title, toggleQuotedNotationCommand));
			}

			var addToGroupByCommand = new AddToGroupByCommand(semanticModel, cursorPosition, selectionLength);
			if (addToGroupByCommand.CanExecute(null))
			{
				//actionList.Add(new OracleContextAction(addToGroupByCommand.Title, addToGroupByCommand));
			}

			var unnestCommonTableExpressionCommand = new UnnestCommonTableExpressionCommand(semanticModel, currentTerminal);
			if (unnestCommonTableExpressionCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction(unnestCommonTableExpressionCommand.Title, unnestCommonTableExpressionCommand));
			}

			var actions = ResolveAmbiguousColumnCommand.ResolveCommands(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction("Resolve as " + c.Title, c));

			actionList.AddRange(actions);

			// TODO: Resolve command order
			return actionList.AsReadOnly();
		}
	}

	[DebuggerDisplay("OracleContextAction (Name={Name})")]
	public class OracleContextAction : IContextAction
	{
		public OracleContextAction(string name, ICommand command)
		{
			Name = name;
			Command = command;
		}

		public string Name { get; private set; }

		public ICommand Command { get; private set; }
	}
}
