using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)currentTerminal.Statement, (OracleDatabaseModel)databaseModel);
			var executionContext = OracleCommandExecutionContext.Create(sqlDocument.StatementText, cursorPosition, semanticModel);
			var model = new CommandSettingsModel { Value = "Enter value", ValidationRule = new OracleIdentifierValidationRule() };
			executionContext.SettingsProvider = new EditDialog(model);
			
			var actionList = new List<IContextAction>();

			if (AddAliasCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddAliasCommand.Title, AddAliasCommand.ExecutionHandler, executionContext));
			}

			if (WrapAsInlineViewCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(WrapAsInlineViewCommand.Title, WrapAsInlineViewCommand.ExecutionHandler, executionContext));
			}

			if (WrapAsCommonTableExpressionCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(WrapAsCommonTableExpressionCommand.Title, WrapAsCommonTableExpressionCommand.ExecutionHandler, executionContext));
			}

			if (ToggleQuotedNotationCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(ToggleQuotedNotationCommand.Title, ToggleQuotedNotationCommand.ExecutionHandler, executionContext));
			}

			if (AddToGroupByCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				// TODO
				//actionList.Add(new OracleContextAction(AddToGroupByCommand.Title, AddToGroupByCommand.ExecutionHandler, executionContext));
			}

			if (ExpandAsteriskCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(ExpandAsteriskCommand.Title, ExpandAsteriskCommand.ExecutionHandler, executionContext));
			}

			if (UnnestInlineViewCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(UnnestInlineViewCommand.Title, UnnestInlineViewCommand.ExecutionHandler, executionContext));
			}

			if (ToggleFullyQualifiedReferencesCommand.ExecutionHandler.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(ToggleFullyQualifiedReferencesCommand.Title, ToggleFullyQualifiedReferencesCommand.ExecutionHandler, executionContext));
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
