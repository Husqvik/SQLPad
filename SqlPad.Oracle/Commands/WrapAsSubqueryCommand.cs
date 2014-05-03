using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	public class WrapAsSubqueryCommand : OracleConfigurableCommandBase
	{
		public WrapAsSubqueryCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal, ICommandSettingsProvider settingsProvider = null)
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
			SettingsModel.Title = "Wrap as sub-query";
			SettingsModel.Heading = SettingsModel.Title;
			SettingsModel.Description = "Enter an alias for the sub-query";

			if (!SettingsProvider.GetSettings())
				return;

			var tableAlias = SettingsProvider.Settings.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentTerminal);

			var builder = new StringBuilder("SELECT ");
			var columnList = String.Join(", ", queryBlock.Columns.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName)).Select(c => c.NormalizedName.ToSimpleIdentifier()));
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
