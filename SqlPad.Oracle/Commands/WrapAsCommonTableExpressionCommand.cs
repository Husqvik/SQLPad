using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class WrapAsCommonTableExpressionCommand : OracleConfigurableCommandBase
	{
		public WrapAsCommonTableExpressionCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode, ICommandSettingsProvider settingsProvider = null)
			: base(semanticModel, currentNode, settingsProvider)
		{
		}

		public override bool CanExecute(object parameter)
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
			       CurrentNode.Id == Terminals.Select &&
			       CurrentQueryBlock.Columns.Any(c => !String.IsNullOrEmpty(c.NormalizedName));
		}

		public override string Title
		{
			get { return "Wrap as common table expression"; }
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			SettingsModel.Title = Title;
			SettingsModel.Heading = SettingsModel.Title;
			SettingsModel.Description = "Enter an alias for the common table expression";

			if (!SettingsProvider.GetSettings())
				return;

			var tableAlias = SettingsProvider.Settings.Value;

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
			builder.Append(queryBlock.RootNode.GetStatementSubstring(statementText));
			builder.Append(")");

			if (CurrentQueryBlock.Type == QueryBlockType.CommonTableExpression)
			{
				builder.Append(", ");
			}

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
			var columnList = String.Join(", ", queryBlock.Columns
				.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
				.Select(c => String.Format("{0}.{1}", tableAlias, c.NormalizedName.ToSimpleIdentifier()))
				);
			
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
