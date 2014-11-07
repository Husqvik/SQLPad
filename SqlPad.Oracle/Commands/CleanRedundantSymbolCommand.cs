using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class CleanRedundantSymbolCommand : OracleCommandBase
	{
		public const string Title = "Clean redundant symbols";

		private IReadOnlyCollection<RedundantTerminalGroup> _terminalGroupsToRemove;

		private CleanRedundantSymbolCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			var prerequisitesMet = ExecutionContext.SelectionLength == 0 && CurrentNode != null &&
			                       SemanticModel.RedundantSymbolGroups.Any();
			if (!prerequisitesMet)
			{
				return false;
			}

			var terminalGroupsToRemove = (IEnumerable<RedundantTerminalGroup>)SemanticModel.RedundantSymbolGroups;

			var doGlobalClean = CurrentNode.Id.In(Terminals.Select, Terminals.Update, Terminals.Insert, Terminals.Delete);
			if (doGlobalClean)
			{
				if (CurrentQueryBlock != null)
				{
					terminalGroupsToRemove = terminalGroupsToRemove.Where(g => g.Any(n => n.HasAncestor(CurrentQueryBlock.RootNode) || (CurrentQueryBlock.OrderByClause != null && n.HasAncestor(CurrentQueryBlock.OrderByClause))));
				}
			}
			else
			{
				terminalGroupsToRemove = terminalGroupsToRemove.Where(g => g.Contains(CurrentNode));
			}

			_terminalGroupsToRemove = terminalGroupsToRemove.ToArray();

			return _terminalGroupsToRemove.Count > 0;
		}

		protected override void Execute()
		{
			var removedTerminals = _terminalGroupsToRemove.SelectMany(g => g);
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
