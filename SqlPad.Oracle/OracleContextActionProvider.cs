using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

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

		public ICollection<IContextAction> GetAvailableRefactorings(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext)
		{
			throw new NotImplementedException();
		}

		public ICollection<IContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext)
		{
			if (sqlDocumentRepository == null || sqlDocumentRepository.Statements == null || executionContext.StatementText != sqlDocumentRepository.StatementText)
				return EmptyCollection;

			var currentTerminal = sqlDocumentRepository.Statements.GetTerminalAtPosition(executionContext.CaretOffset);
			if (currentTerminal == null)
				return EmptyCollection;

			var precedingTerminal = currentTerminal.PrecedingTerminal;
			if (currentTerminal.SourcePosition.IndexStart == executionContext.CaretOffset && precedingTerminal != null && precedingTerminal.SourcePosition.IndexEnd + 1 == executionContext.CaretOffset &&
				currentTerminal.Id.In(Terminals.Comma, Terminals.LeftParenthesis, Terminals.RightParenthesis))
			{
				currentTerminal = precedingTerminal;
			}

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

			if (OracleCommands.AddToGroupByClause.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddToGroupByCommand.Title, OracleCommands.AddToGroupByClause, executionContext));
			}

			var canExecuteResult = OracleCommands.ExpandAsterisk.CanExecuteHandler(executionContext);
			if (canExecuteResult)
			{
				actionList.Add(new OracleContextAction(ExpandAsteriskCommand.Title, OracleCommands.ExpandAsterisk, CloneContextWithUseDefaultSettingsOption(executionContext), canExecuteResult.IsLongOperation));
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

			canExecuteResult = OracleCommands.CreateScript.CanExecuteHandler(executionContext);
			if (canExecuteResult)
			{
				actionList.Add(new OracleContextAction(CreateScriptCommand.Title, OracleCommands.CreateScript, executionContext, canExecuteResult.IsLongOperation));
			}

			if (OracleCommands.AddInsertIntoColumnList.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddInsertIntoColumnListCommand.Title, OracleCommands.AddInsertIntoColumnList, CloneContextWithUseDefaultSettingsOption(executionContext)));
			}

			if (OracleCommands.CleanRedundantSymbol.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(CleanRedundantSymbolCommand.Title, OracleCommands.CleanRedundantSymbol, executionContext));
			}

			if (OracleCommands.AddCreateTableAs.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(AddCreateTableAsCommand.Title, OracleCommands.AddCreateTableAs, executionContext));
			}

			if (OracleCommands.Unquote.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(UnquoteCommand.Title, OracleCommands.Unquote, executionContext));
			}

			if (OracleCommands.PropagateColumn.CanExecuteHandler(executionContext))
			{
				actionList.Add(new OracleContextAction(PropagateColumnCommand.Title, OracleCommands.PropagateColumn, executionContext));
			}

			var actions = ResolveAmbiguousColumnCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction("Resolve as " + c.Name, c, executionContext));

			actionList.AddRange(actions);

			actions = BindVariableLiteralConversionCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction(c.Name, c, executionContext));

			actionList.AddRange(actions);

			actions = LiteralBindVariableConversionCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
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
