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

		private WrapAsCommonTableExpressionCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
			       CurrentNode.Id == Terminals.Select &&
			       CurrentQueryBlock.Columns.Any(c => !String.IsNullOrEmpty(c.NormalizedName));
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;
			settingsModel.ValidationRule = new OracleIdentifierValidationRule();

			settingsModel.Title = Title;
			settingsModel.Heading = settingsModel.Title;
			settingsModel.Description = "Enter an alias for the common table expression";

			return settingsModel;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

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
				startIndex = CurrentQueryBlock.RootNode.GetAncestor(NonTerminals.CommonTableExpression).SourcePosition.IndexStart;
			}
			else
			{
				builder.Append(", ");
				startIndex = lastCte.RootNode.GetAncestor(NonTerminals.CommonTableExpression).SourcePosition.IndexEnd + 1;
			}

			builder.Append(tableAlias + " AS (");
			builder.Append(queryBlock.RootNode.GetText(ExecutionContext.StatementText));
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
