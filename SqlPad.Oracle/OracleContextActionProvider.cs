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

		internal ICollection<IContextAction> GetContextActions(OracleDatabaseModelBase databaseModel, string statementText, int cursorPosition)
		{
			var documentStore = new SqlDocumentRepository(_oracleParser, new OracleStatementValidator(), databaseModel, statementText);
			var executionContext = new CommandExecutionContext(statementText, cursorPosition, cursorPosition, 0, documentStore);
			return GetContextActions(documentStore, executionContext);
		}

		public ICollection<IContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext)
		{
			if (sqlDocumentRepository == null || sqlDocumentRepository.Statements == null || executionContext.StatementText != sqlDocumentRepository.StatementText)
				return EmptyCollection;

			var currentTerminal = sqlDocumentRepository.Statements.GetTerminalAtPosition(executionContext.CaretOffset);
			if (currentTerminal == null)
				return EmptyCollection;

			var semanticModel = (OracleStatementSemanticModel)sqlDocumentRepository.ValidationModels[currentTerminal.Statement].SemanticModel;
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

			// TODO
			/*if (OracleCommands.AddToGroupByClause.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddToGroupByCommand.Title, OracleCommands.AddToGroupByClause, executionContext));
			}*/

			if (OracleCommands.ExpandAsterisk.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(ExpandAsteriskCommand.Title, OracleCommands.ExpandAsterisk, CloneContextWithUseDefaultSettingsOption(executionContext)));
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
				actionList.Add(new OracleContextAction(AddMissingColumnCommand.Title, OracleCommands.GenerateMissingColumns, executionContext));
			}

			if (OracleCommands.CreateScript.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(CreateScriptCommand.Title, OracleCommands.CreateScript, executionContext, true));
			}

			if (OracleCommands.AddInsertIntoColumnList.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddInsertIntoColumnListCommand.Title, OracleCommands.AddInsertIntoColumnList, CloneContextWithUseDefaultSettingsOption(executionContext)));
			}

			if (OracleCommands.CleanRedundantQualifier.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(CleanRedundantQualifierCommand.Title, OracleCommands.CleanRedundantQualifier, executionContext));
			}

			if (OracleCommands.AddCreateTableAs.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddCreateTableAsCommand.Title, OracleCommands.AddCreateTableAs, executionContext));
			}

			if (OracleCommands.Unquote.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(UnquoteCommand.Title, OracleCommands.Unquote, executionContext));
			}

			var actions = ResolveAmbiguousColumnCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction("Resolve as " + c.Name, c, executionContext));

			actionList.AddRange(actions);

			actions = BindVariableLiteralConversionCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction(c.Name, c, executionContext));

			actionList.AddRange(actions);

			// TODO: Resolve command order
			return actionList.AsReadOnly();
		}

		private static CommandExecutionContext CloneContextWithUseDefaultSettingsOption(CommandExecutionContext executionContext)
		{
			var contextWithUseDefaultSettingsOption = executionContext.Clone();
			contextWithUseDefaultSettingsOption.SettingsProvider = new EditDialog(new CommandSettingsModel { UseDefaultSettings = () => !Keyboard.IsKeyDown(Key.LeftShift) });

			return contextWithUseDefaultSettingsOption;
		}
	}

	[DebuggerDisplay("OracleContextAction (Name={Name})")]
	internal class OracleContextAction : IContextAction
	{
		public OracleContextAction(string name, CommandExecutionHandler executionHandler, CommandExecutionContext executionContext, bool isLongOperation = false)
		{
			Name = name;
			ExecutionHandler = executionHandler;
			ExecutionContext = executionContext;
			IsLongOperation = isLongOperation;
		}

		public string Name { get; private set; }

		public bool IsLongOperation { get; private set; }

		public CommandExecutionHandler ExecutionHandler { get; private set; }

		public CommandExecutionContext ExecutionContext { get; private set; }
	}
}
