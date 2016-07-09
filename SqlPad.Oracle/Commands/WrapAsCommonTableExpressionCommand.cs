using System;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class WrapAsCommonTableExpressionCommand : OracleCommandBase
	{
		public const string Title = "Wrap as common table expression";

		private WrapAsCommonTableExpressionCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return
				CurrentQueryBlock != null &&
				String.Equals(CurrentNode?.Id, Terminals.Select) &&
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
			{
				return;
			}

			var tableAlias = settingsModel.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentNode);

			var lastCte = SemanticModel.MainQueryBlock
				.AccessibleQueryBlocks
				.OrderByDescending(qb => qb.RootNode.SourcePosition.IndexStart)
				.FirstOrDefault();

			var builder = new SqlTextBuilder();
			var startIndex = SemanticModel.Statement.RootNode.SourcePosition.IndexStart;
			if (lastCte == null)
			{
				builder.AppendReservedWord("WITH ");
			}
			else if (CurrentQueryBlock.Type == QueryBlockType.CommonTableExpression)
			{
				startIndex = CurrentQueryBlock.RootNode.GetAncestor(NonTerminals.CommonTableExpression).SourcePosition.IndexStart;
			}
			else
			{
				builder.AppendText(", ");
				startIndex = lastCte.RootNode.GetAncestor(NonTerminals.CommonTableExpression).SourcePosition.IndexEnd + 1;
			}

			builder.AppendText(tableAlias);
			builder.AppendKeyword(" AS (");
			var subqueryIndextStart = queryBlock.RootNode.SourcePosition.IndexStart;
			var subqueryIndexEnd = CurrentQueryBlock.OrderByClause == null
				? CurrentQueryBlock.RootNode.SourcePosition.IndexEnd
				: CurrentQueryBlock.OrderByClause.SourcePosition.IndexEnd;

			var subqueryLength = subqueryIndexEnd - subqueryIndextStart + 1;
			var subquery = ExecutionContext.StatementText.Substring(subqueryIndextStart, subqueryLength);
			builder.AppendText(subquery);
			builder.AppendText(")");

			if (CurrentQueryBlock.Type == QueryBlockType.CommonTableExpression)
			{
				builder.AppendText(", ");
			}

			if (lastCte == null)
			{
				builder.AppendText(" ");
			}

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = startIndex,
					Text = builder.ToString()
				});

			var formatOptions = OracleConfiguration.Configuration.Formatter.FormatOptions;

			builder = new SqlTextBuilder();
			builder.AppendReservedWord("SELECT ");
			var columnList = String.Join(", ", queryBlock.Columns
				.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
				.Select(c => $"{tableAlias}.{OracleStatementFormatter.FormatTerminalValue(c.NormalizedName.ToSimpleIdentifier(), formatOptions.Identifier)}"));
			
			builder.AppendText(columnList);
			builder.AppendReservedWord(" FROM ");
			builder.AppendText(tableAlias);

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = subqueryIndextStart,
					Length = subqueryLength,
					Text = builder.ToString()
				});
		}
	}
}
