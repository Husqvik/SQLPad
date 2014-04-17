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
			if (CurrentTerminal.Id != Terminals.Select)
				return false;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentTerminal);
			return queryBlock != null;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			var queryBlock = SemanticModel.GetQueryBlock(CurrentTerminal);

			bool? enableQuotes = null;
			foreach (var identifier in queryBlock.RootNode.Terminals.Where(t => (Terminals.Identifiers.Contains(t.Id) || t.Id == Terminals.Alias || t.Id == Terminals.ObjectAlias) && !t.Token.Value.CollidesWithKeyword()))
			{
				if (!enableQuotes.HasValue)
				{
					enableQuotes = !identifier.Token.Value.IsQuoted();
				}

				var replacedLength = enableQuotes.Value ? 0 : 1;
				segmentsToReplace.Add(new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexStart,
					Length = replacedLength,
					Text = enableQuotes.Value ? "\"" : String.Empty
				});

				segmentsToReplace.Add(new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexEnd + 1 - replacedLength,
					Length = replacedLength,
					Text = enableQuotes.Value ? "\"" : String.Empty
				});
			}
		}
	}
}
