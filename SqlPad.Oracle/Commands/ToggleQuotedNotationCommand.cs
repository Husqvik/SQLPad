using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class ToggleQuotedNotationCommand : OracleCommandBase
	{
		public ToggleQuotedNotationCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
			: base(semanticModel, currentTerminal)
		{
		}

		public override bool CanExecute(object parameter)
		{
			return CurrentTerminal != null && CurrentQueryBlock != null && CurrentTerminal.Id == Terminals.Select;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			bool? enableQuotes = null;
			foreach (var identifier in CurrentQueryBlock.RootNode.Terminals.Where(t => (OracleGrammarDescription.Identifiers.Contains(t.Id) || t.Id == Terminals.ColumnAlias || t.Id == Terminals.ObjectAlias) && !t.Token.Value.CollidesWithKeyword()))
			{
				if (!enableQuotes.HasValue)
				{
					enableQuotes = !identifier.Token.Value.IsQuoted();
				}

				if ((enableQuotes.Value && identifier.Token.Value.IsQuoted()) ||
				    !enableQuotes.Value && !identifier.Token.Value.IsQuoted())
					continue;

				var replacedLength = enableQuotes.Value ? 0 : 1;
				var newText = enableQuotes.Value ? "\"" : String.Empty;

				segmentsToReplace.Add(new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexStart,
					Length = replacedLength,
					Text = newText
				});

				segmentsToReplace.Add(new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexEnd + 1 - replacedLength,
					Length = replacedLength,
					Text = newText
				});
			}
		}
	}
}
