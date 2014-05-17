using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	public class WrapAsInlineViewCommand : OracleConfigurableCommandBase
	{
		public WrapAsInlineViewCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode, ICommandSettingsProvider settingsProvider = null)
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
			get { return "Wrap as inline view"; }
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			SettingsModel.Title = Title;
			SettingsModel.Heading = SettingsModel.Title;
			SettingsModel.Description = "Enter an alias for the inline view";

			if (!SettingsProvider.GetSettings())
				return;

			var tableAlias = SettingsProvider.Settings.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentNode);

			var builder = new StringBuilder("SELECT ");
			var columnList = String.Join(", ", queryBlock.Columns
				.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
				.Select(c => String.Format("{0}.{1}", tableAlias, c.NormalizedName.ToSimpleIdentifier()))
				);
			
			builder.Append(columnList);
			builder.Append(" FROM (");

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = queryBlock.RootNode.SourcePosition.IndexStart,
				Length = 0,
				Text = builder.ToString()
			});

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = queryBlock.RootNode.SourcePosition.IndexEnd + 1,
				Length = 0,
				Text = ") " + tableAlias
			});
		}
	}
}
