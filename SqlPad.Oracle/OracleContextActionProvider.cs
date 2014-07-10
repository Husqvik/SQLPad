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
			var statements = _oracleParser.Parse(statementText);
			var documentStore = new SqlDocumentRepository(_oracleParser, new OracleStatementValidator(), databaseModel, statementText);
			var executionContext = new CommandExecutionContext(statementText, 0, 0, cursorPosition, statements, databaseModel);
			return GetContextActions(documentStore, executionContext);
		}

		public ICollection<IContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext)
		{
			if (sqlDocumentRepository == null || sqlDocumentRepository.StatementCollection == null || executionContext.StatementText != sqlDocumentRepository.StatementText)
				return EmptyCollection;

			var currentTerminal = sqlDocumentRepository.StatementCollection.GetTerminalAtPosition(executionContext.CaretOffset);
			if (currentTerminal == null)
				return EmptyCollection;

			var semanticModel = (OracleStatementSemanticModel)sqlDocumentRepository.ValidationModels[currentTerminal.Statement].SemanticModel;
			var oracleExecutionContext = OracleCommandExecutionContext.Create(sqlDocumentRepository.StatementText, executionContext.Line, executionContext.Column, executionContext.CaretOffset, semanticModel);
			var enterIdentifierModel = new CommandSettingsModel { Value = "Enter value" };
			oracleExecutionContext.SettingsProvider = new EditDialog(enterIdentifierModel);
			
			var actionList = new List<IContextAction>();

			if (OracleCommands.AddAlias.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(AddAliasCommand.Title, OracleCommands.AddAlias, oracleExecutionContext));
			}

			if (OracleCommands.WrapAsInlineView.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(WrapAsInlineViewCommand.Title, OracleCommands.WrapAsInlineView, oracleExecutionContext));
			}

			if (OracleCommands.WrapAsCommonTableExpression.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(WrapAsCommonTableExpressionCommand.Title, OracleCommands.WrapAsCommonTableExpression, oracleExecutionContext));
			}

			if (OracleCommands.ToggleQuotedNotation.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(ToggleQuotedNotationCommand.Title, OracleCommands.ToggleQuotedNotation, oracleExecutionContext));
			}

			// TODO
			/*if (OracleCommands.AddToGroupByClause.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(AddToGroupByCommand.Title, OracleCommands.AddToGroupByClause, executionContext));
			}*/

			if (OracleCommands.ExpandAsterisk.CanExecuteHandler(oracleExecutionContext))
			{
				var expandAsteriskExecutionContext = oracleExecutionContext.Clone();
				expandAsteriskExecutionContext.SettingsProvider = new EditDialog(new CommandSettingsModel { UseDefaultSettings = () => !Keyboard.IsKeyDown(Key.LeftShift) } );

				actionList.Add(new OracleContextAction(ExpandAsteriskCommand.Title, OracleCommands.ExpandAsterisk, expandAsteriskExecutionContext));
			}

			if (OracleCommands.UnnestInlineView.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(UnnestInlineViewCommand.Title, OracleCommands.UnnestInlineView, oracleExecutionContext));
			}

			if (OracleCommands.ToggleFullyQualifiedReferences.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(ToggleFullyQualifiedReferencesCommand.Title, OracleCommands.ToggleFullyQualifiedReferences, oracleExecutionContext));
			}

			if (OracleCommands.GenerateMissingColumns.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(AddMissingColumnCommand.Title, OracleCommands.GenerateMissingColumns, oracleExecutionContext));
			}

			if (OracleCommands.CreateScript.CanExecuteHandler(oracleExecutionContext))
			{
				actionList.Add(new OracleContextAction(CreateScriptCommand.Title, OracleCommands.CreateScript, oracleExecutionContext));
			}

			var actions = ResolveAmbiguousColumnCommand.ResolveCommandHandlers(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction("Resolve as " + c.Name, c, oracleExecutionContext));

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
