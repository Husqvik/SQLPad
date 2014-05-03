using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class AddToGroupByCommand : OracleCommandBase
	{
		private readonly StatementDescriptionNode _fromClause;
		private readonly int _selectionStart;
		private readonly int _selectionLength;

		public AddToGroupByCommand(OracleStatementSemanticModel semanticModel, int selectionStart, int selectionLength)
			: base(semanticModel, semanticModel.Statement.GetNodeAtPosition(selectionStart))
		{
			_selectionLength = selectionLength;
			_selectionStart = selectionStart;
			_fromClause = CurrentQueryBlock == null
				? null
				: CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
		}

		public override bool CanExecute(object parameter)
		{
			// TODO: Check ambiguous references
			return CurrentNode != null && _fromClause != null && _fromClause.IsGrammarValid;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			var selectedTerminals = CurrentQueryBlock.RootNode.Terminals.Where(t => t.SourcePosition.IndexEnd >= _selectionStart && t.SourcePosition.IndexStart <= _selectionStart + _selectionLength).ToArray();
			var expressions = selectedTerminals.Select(t => t.GetTopAncestor(NonTerminals.Expression)).Distinct().ToArray();

			var firstTerminalStartIndex = selectedTerminals[0].SourcePosition.IndexStart;
			
			var groupByClause = CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.GroupByClause).SingleOrDefault();
			if (groupByClause == null)
			{
				segmentsToReplace.Add(new TextSegment
				                      {
					                      IndextStart = _fromClause.LastTerminalNode.SourcePosition.IndexEnd + 1,
					                      Length = 0,
										  Text = " GROUP BY " + statementText.Substring(firstTerminalStartIndex, selectedTerminals[selectedTerminals.Length - 1].SourcePosition.IndexEnd - firstTerminalStartIndex + 1)
				                      });
			}
			else
			{
				var groupingExpressions = groupByClause.GetDescendantsWithinSameQuery(NonTerminals.GroupingClause).Where(n => n.ChildNodes.First().Id == NonTerminals.Expression);
				// TODO: Find existing elements
			}

			// TODO: Find handle multiple selected columns
		}
	}
}
