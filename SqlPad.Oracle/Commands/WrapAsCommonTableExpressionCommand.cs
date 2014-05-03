using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class WrapAsCommonTableExpressionCommand : OracleConfigurableCommandBase
	{
		public WrapAsCommonTableExpressionCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal, ICommandSettingsProvider settingsProvider = null)
			: base(semanticModel, currentTerminal, settingsProvider)
		{
		}

		public override bool CanExecute(object parameter)
		{
			return CurrentTerminal != null && CurrentQueryBlock != null &&
			       CurrentTerminal.Id == Terminals.Select &&
			       CurrentQueryBlock.Columns.Any(c => !String.IsNullOrEmpty(c.NormalizedName));
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			SettingsModel.Title = "Wrap as common table expression";
			SettingsModel.Heading = SettingsModel.Title;
			SettingsModel.Description = "Enter an alias for the common table expression";

			if (!SettingsProvider.GetSettings())
				return;

			var tableAlias = SettingsProvider.Settings.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentTerminal);

			var lastCte = SemanticModel.MainQueryBlock
				.AccessibleQueryBlocks
				.OrderByDescending(qb => qb.RootNode.SourcePosition.IndexStart)
				.FirstOrDefault();

			var builder = new StringBuilder();
			var startIndex = 0;
			if (lastCte == null)
			{
				builder.Append("WITH ");
			}
			else
			{
				builder.Append(", ");
				startIndex = lastCte.RootNode.GetAncestor(NonTerminals.SubqueryComponent).SourcePosition.IndexEnd + 1;
			}

			builder.Append(tableAlias + " AS (");
			builder.Append(statementText.Substring(queryBlock.RootNode.SourcePosition.IndexStart, queryBlock.RootNode.SourcePosition.Length));
			builder.Append(")");

			if (lastCte == null)
			{
				builder.Append(" ");	
			}

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = startIndex,
				Length = 0,
				Text = builder.ToString()
			});
			
			builder = new StringBuilder("SELECT ");
			var columnList = String.Join(", ", queryBlock.Columns.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName)).Select(c => c.NormalizedName.ToSimpleIdentifier()));
			builder.Append(columnList);
			builder.Append(" FROM ");
			builder.Append(tableAlias);

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = queryBlock.RootNode.SourcePosition.IndexStart,
				Length = queryBlock.RootNode.SourcePosition.Length,
				Text = builder.ToString()
			});
		}
	}
}
