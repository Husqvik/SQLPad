using System;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class WrapAsCommonTableExpressionCommand : OracleCommandBase
	{
		public const string Title = "Wrap as common table expression";

		public static CommandExecutionHandler ExecutionHandler = new CommandExecutionHandler
		{
			Name = "WrapAsCommonTableExpression",
			ExecutionHandler = ExecutionHandlerImplementation,
			CanExecuteHandler = CanExecuteHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			var commandInstance = new WrapAsCommonTableExpressionCommand((OracleCommandExecutionContext)executionContext);
			if (commandInstance.CanExecute())
			{
				commandInstance.Execute();
			}
		}

		private static bool CanExecuteHandlerImplementation(CommandExecutionContext executionContext)
		{
			return new WrapAsCommonTableExpressionCommand((OracleCommandExecutionContext)executionContext).CanExecute();
		}

		private WrapAsCommonTableExpressionCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		private bool CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
			       CurrentNode.Id == Terminals.Select &&
			       CurrentQueryBlock.Columns.Any(c => !String.IsNullOrEmpty(c.NormalizedName));
		}

		private void Execute()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;

			settingsModel.Title = Title;
			settingsModel.Heading = settingsModel.Title;
			settingsModel.Description = "Enter an alias for the common table expression";

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var tableAlias = settingsModel.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentNode);

			var lastCte = SemanticModel.MainQueryBlock
				.AccessibleQueryBlocks
				.OrderByDescending(qb => qb.RootNode.SourcePosition.IndexStart)
				.FirstOrDefault();

			var builder = new StringBuilder();
			var startIndex = SemanticModel.Statement.RootNode.SourcePosition.IndexStart;
			if (lastCte == null)
			{
				builder.Append("WITH ");
			}
			else if (CurrentQueryBlock.Type == QueryBlockType.CommonTableExpression)
			{
				startIndex = CurrentQueryBlock.RootNode.GetAncestor(NonTerminals.SubqueryComponent).SourcePosition.IndexStart;
			}
			else
			{
				builder.Append(", ");
				startIndex = lastCte.RootNode.GetAncestor(NonTerminals.SubqueryComponent).SourcePosition.IndexEnd + 1;
			}

			builder.Append(tableAlias + " AS (");
			builder.Append(queryBlock.RootNode.GetStatementSubstring(ExecutionContext.StatementText));
			builder.Append(")");

			if (CurrentQueryBlock.Type == QueryBlockType.CommonTableExpression)
			{
				builder.Append(", ");
			}

			if (lastCte == null)
			{
				builder.Append(" ");	
			}

			ExecutionContext.SegmentsToReplace.Add(new TextSegment
			{
				IndextStart = startIndex,
				Length = 0,
				Text = builder.ToString()
			});
			
			builder = new StringBuilder("SELECT ");
			var columnList = String.Join(", ", queryBlock.Columns
				.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
				.Select(c => String.Format("{0}.{1}", tableAlias, c.NormalizedName.ToSimpleIdentifier()))
				);
			
			builder.Append(columnList);
			builder.Append(" FROM ");
			builder.Append(tableAlias);

			ExecutionContext.SegmentsToReplace.Add(new TextSegment
			{
				IndextStart = queryBlock.RootNode.SourcePosition.IndexStart,
				Length = queryBlock.RootNode.SourcePosition.Length,
				Text = builder.ToString()
			});
		}
	}
}
