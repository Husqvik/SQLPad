using System;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class CleanRedundantSymbolCommand : OracleCommandBase
	{
		public const string Title = "Clean redundant symbols";

		private RedundantTerminalGroup _terminalGroup;

		private CleanRedundantSymbolCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			var prerequisitesMet = ExecutionContext.SelectionLength == 0 && CurrentNode != null &&
			                       CurrentQueryBlock != null &&
			                       SemanticModel.RedundantSymbolGroups.Any();
			if (!prerequisitesMet)
			{
				return false;
			}

			var doGlobalClean = CurrentNode.Id.In(Terminals.Select, Terminals.Update, Terminals.Insert, Terminals.Delete);
			_terminalGroup = doGlobalClean
				? null
				: SemanticModel.RedundantSymbolGroups.SingleOrDefault(g => g.Contains(CurrentNode));

			return doGlobalClean || _terminalGroup != null;
		}

		protected override void Execute()
		{
			var removedTerminals = _terminalGroup ?? SemanticModel.RedundantSymbolGroups.SelectMany(g => g);
			var removedSegments = removedTerminals
				.Select(n =>
					new TextSegment
					{
						IndextStart = n.SourcePosition.IndexStart,
						Length = n.SourcePosition.Length,
						Text = String.Empty
					});

			ExecutionContext.SegmentsToReplace.AddRange(removedSegments);
		}
	}
}
