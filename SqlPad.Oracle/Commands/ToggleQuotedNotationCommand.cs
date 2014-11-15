using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class ToggleQuotedNotationCommand : OracleCommandBase
	{
		public const string Title = "Toggle quoted notation";

		private ToggleQuotedNotationCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null && CurrentNode.Id == Terminals.Select &&
			       GetReplacedSegments().Any();
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.AddRange(GetReplacedSegments());
		}

		private IEnumerable<TextSegment> GetReplacedSegments()
		{
			bool? enableQuotes = null;
			foreach (var identifier in CurrentQueryBlock.RootNode.Terminals.Where(t => t.Id.IsIdentifierOrAlias() && t.Token.Value.ToQuotedIdentifier() != t.Token.Value.ToSimpleIdentifier() && !t.Token.Value.CollidesWithReservedWord()))
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

				yield return new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexStart,
					Length = replacedLength,
					Text = newText
				};

				yield return new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexEnd + 1 - replacedLength,
					Length = replacedLength,
					Text = newText
				};
			}
		}
	}
}
