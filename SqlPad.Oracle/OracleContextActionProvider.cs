using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

namespace SqlPad.Oracle
{
	public class OracleContextActionProvider : IContextActionProvider
	{
		private static readonly IContextAction[] EmptyCollection = new IContextAction[0];
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();

		internal ICollection<IContextAction> GetContextActions(IDatabaseModel databaseModel, string statementText, int cursorPosition, int selectionLength = 0)
		{
			return GetContextActions(databaseModel, SqlDocument.FromStatementCollection(_oracleParser.Parse(statementText), statementText), cursorPosition, selectionLength);
		}

		public ICollection<IContextAction> GetContextActions(IDatabaseModel databaseModel, SqlDocument sqlDocument, int cursorPosition, int selectionLength = 0)
		{
			if (sqlDocument == null || sqlDocument.StatementCollection == null)
				return EmptyCollection;

			var currentTerminal = sqlDocument.StatementCollection.GetTerminalAtPosition(cursorPosition);
			if (currentTerminal == null)
				return EmptyCollection;

			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)currentTerminal.Statement, (OracleDatabaseModelBase)databaseModel);
			var executionContext = OracleCommandExecutionContext.Create(sqlDocument.StatementText, cursorPosition, semanticModel);
			var enterIdentifierModel = new CommandSettingsModel { Value = "Enter value" };
			executionContext.SettingsProvider = new EditDialog(enterIdentifierModel);
			
			var actionList = new List<IContextAction>();

			if (OracleCommands.AddAlias.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddAliasCommand.Title, OracleCommands.AddAlias, executionContext));
			}

			if (OracleCommands.WrapAsInlineView.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(WrapAsInlineViewCommand.Title, OracleCommands.WrapAsInlineView, executionContext));
			}

			if (OracleCommands.WrapAsCommonTableExpression.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(WrapAsCommonTableExpressionCommand.Title, OracleCommands.WrapAsCommonTableExpression, executionContext));
			}

			if (OracleCommands.ToggleQuotedNotation.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(ToggleQuotedNotationCommand.Title, OracleCommands.ToggleQuotedNotation, executionContext));
			}

			if (OracleCommands.AddToGroupByClause.CanExecuteHandler(executionContext))
			{
				// TODO
				//actionList.Add(new OracleContextAction(AddToGroupByCommand.Title, OracleCommands.AddToGroupByClause, executionContext));
			}

			if (OracleCommands.ExpandAsterisk.CanExecuteHandler(executionContext))
			{
				var expandAsteriskExecutionContext = executionContext.Clone();
				expandAsteriskExecutionContext.SettingsProvider = new EditDialog(new CommandSettingsModel { UseDefaultSettings = () => !Keyboard.IsKeyDown(Key.LeftShift) } );

				actionList.Add(new OracleContextAction(ExpandAsteriskCommand.Title, OracleCommands.ExpandAsterisk, expandAsteriskExecutionContext));
			}

			if (OracleCommands.UnnestInlineView.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(UnnestInlineViewCommand.Title, OracleCommands.UnnestInlineView, executionContext));
			}

			if (OracleCommands.ToggleFullyQualifiedReferences.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(ToggleFullyQualifiedReferencesCommand.Title, OracleCommands.ToggleFullyQualifiedReferences, executionContext));
			}

			if (OracleCommands.GenerateMissingColumns.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(GenerateMissingColumnsCommand.Title, OracleCommands.GenerateMissingColumns, executionContext));
			}

			var actions = ResolveAmbiguousColumnCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction("Resolve as " + c.Name, c, executionContext));

			actionList.AddRange(actions);

			// TODO: Resolve command order
			return actionList.AsReadOnly();
		}
	}

	[DebuggerDisplay("OracleContextAction (Name={Name})")]
	internal class OracleContextAction : IContextAction
	{
		public OracleContextAction(string name, CommandExecutionHandler executionHandler, CommandExecutionContext executionContext)
		{
			Name = name;
			ExecutionHandler = executionHandler;
			ExecutionContext = executionContext;
		}

		public string Name { get; private set; }

		public CommandExecutionHandler ExecutionHandler { get; private set; }

		public CommandExecutionContext ExecutionContext { get; private set; }
	}
}
