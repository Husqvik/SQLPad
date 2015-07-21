using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleContextActionProvider : IContextActionProvider
	{
		private static readonly ContextAction[] EmptyCollection = new ContextAction[0];

		internal ICollection<ContextAction> GetContextActions(OracleDatabaseModelBase databaseModel, string statementText, int cursorPosition)
		{
			var documentStore = new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), databaseModel, statementText);
			var executionContext = new CommandExecutionContext(statementText, cursorPosition, cursorPosition, 0, documentStore);
			return GetContextActions(documentStore, executionContext);
		}

		public ICollection<ContextAction> GetAvailableRefactorings(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext)
		{
			throw new NotImplementedException();
		}

		public ICollection<ContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext)
		{
			if (sqlDocumentRepository?.Statements == null || executionContext.StatementText != sqlDocumentRepository.StatementText)
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
			
			var actionList = new List<ContextAction>();

			if (OracleCommands.AddAlias.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(AddAliasCommand.Title, OracleCommands.AddAlias, executionContext));
			}

			if (OracleCommands.WrapAsInlineView.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(WrapAsInlineViewCommand.Title, OracleCommands.WrapAsInlineView, executionContext));
			}

			if (OracleCommands.WrapAsCommonTableExpression.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(WrapAsCommonTableExpressionCommand.Title, OracleCommands.WrapAsCommonTableExpression, executionContext));
			}

			if (OracleCommands.ToggleQuotedNotation.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(ToggleQuotedNotationCommand.Title, OracleCommands.ToggleQuotedNotation, executionContext));
			}

			if (OracleCommands.AddToGroupByClause.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(AddToGroupByCommand.Title, OracleCommands.AddToGroupByClause, executionContext));
			}

			var canExecuteResult = OracleCommands.ExpandAsterisk.CanExecuteHandler(executionContext);
			if (canExecuteResult)
			{
				actionList.Add(new ContextAction(ExpandAsteriskCommand.Title, OracleCommands.ExpandAsterisk, CloneContextWithUseDefaultSettingsOption(executionContext), canExecuteResult.IsLongOperation));
			}

			if (OracleCommands.UnnestInlineView.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(UnnestInlineViewCommand.Title, OracleCommands.UnnestInlineView, executionContext));
			}

			if (OracleCommands.ToggleFullyQualifiedReferences.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(ToggleFullyQualifiedReferencesCommand.Title, OracleCommands.ToggleFullyQualifiedReferences, executionContext));
			}

			if (OracleCommands.GenerateMissingColumns.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(AddMissingColumnCommand.Title, OracleCommands.GenerateMissingColumns, executionContext));
			}

			canExecuteResult = OracleCommands.CreateScript.CanExecuteHandler(executionContext);
			if (canExecuteResult)
			{
				actionList.Add(new ContextAction(CreateScriptCommand.Title, OracleCommands.CreateScript, CloneContextWithUseDefaultSettingsOption(executionContext), canExecuteResult.IsLongOperation));
			}

			if (OracleCommands.AddInsertIntoColumnList.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(AddInsertIntoColumnListCommand.Title, OracleCommands.AddInsertIntoColumnList, CloneContextWithUseDefaultSettingsOption(executionContext)));
			}

			if (OracleCommands.CleanRedundantSymbol.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(CleanRedundantSymbolCommand.Title, OracleCommands.CleanRedundantSymbol, executionContext));
			}

			if (OracleCommands.AddCreateTableAs.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(AddCreateTableAsCommand.Title, OracleCommands.AddCreateTableAs, executionContext));
			}

			if (OracleCommands.Unquote.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(UnquoteCommand.Title, OracleCommands.Unquote, executionContext));
			}

			if (OracleCommands.PropagateColumn.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(PropagateColumnCommand.Title, OracleCommands.PropagateColumn, executionContext));
			}

			if (OracleCommands.ConvertOrderByNumberColumnReferences.CanExecuteHandler(executionContext))
			{
				actionList.Add(new ContextAction(ConvertOrderByNumberColumnReferencesCommand.Title, OracleCommands.ConvertOrderByNumberColumnReferences, executionContext));
			}

			var actions = ResolveAmbiguousColumnCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new ContextAction("Resolve as " + c.Name, c, executionContext));

			actionList.AddRange(actions);

			actions = BindVariableLiteralConversionCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new ContextAction(c.Name, c, executionContext));

			actionList.AddRange(actions);

			actions = LiteralBindVariableConversionCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new ContextAction(c.Name, c, executionContext));

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
}
