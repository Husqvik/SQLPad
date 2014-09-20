using System;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class CleanRedundantQualifierCommand : OracleCommandBase
	{
		public const string Title = "Clean redundant qualifiers";

		private CleanRedundantQualifierCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			return ExecutionContext.SelectionLength == 0 && CurrentNode != null &&
			       CurrentQueryBlock != null &&
			       CurrentNode.Id.In(Terminals.Select, Terminals.Update, Terminals.Insert, Terminals.Delete) &&
			       SemanticModel.RedundantNodes.Any();
		}

		protected override void Execute()
		{
			var removedSegments = SemanticModel.RedundantNodes
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
