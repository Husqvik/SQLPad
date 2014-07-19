using System.Linq;
using SqlPad.Commands;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddToGroupByCommand : OracleCommandBase
	{
		private readonly StatementGrammarNode _fromClause;

		public const string Title = "Add to GROUP BY clause";

		private AddToGroupByCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
			_fromClause = CurrentQueryBlock == null
				? null
				: CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
		}

		protected override bool CanExecute()
		{
			// TODO: Check ambiguous references, span over clauses, etc.
			return CurrentNode != null && _fromClause != null && _fromClause.IsGrammarValid;
		}

		protected override void Execute()
		{
			var selectedTerminals = CurrentQueryBlock.RootNode.Terminals.Where(t => t.SourcePosition.IndexEnd >= ExecutionContext.SelectionStart && t.SourcePosition.IndexStart <= ExecutionContext.SelectionStart + ExecutionContext.SelectionLength).ToArray();
			var expressions = selectedTerminals.Select(t => t.GetTopAncestor(NonTerminals.Expression)).Distinct().ToArray();

			var columnReferences = CurrentQueryBlock.AllColumnReferences
				.Where(c => selectedTerminals.Contains(c.ColumnNode))
				.ToDictionary(c => c.ColumnNode, c => c);

			var functionReferences = CurrentQueryBlock.AllFunctionReferences
				.Where(c => selectedTerminals.Contains(c.FunctionIdentifierNode))
				.ToDictionary(c => c.FunctionIdentifierNode, c => c);

			var firstTerminalStartIndex = selectedTerminals[0].SourcePosition.IndexStart;
			
			var groupByClause = CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.GroupByClause).SingleOrDefault();
			if (groupByClause == null)
			{
				ExecutionContext.SegmentsToReplace.Add(new TextSegment
				                      {
					                      IndextStart = _fromClause.LastTerminalNode.SourcePosition.IndexEnd + 1,
					                      Length = 0,
										  Text = " GROUP BY " + ExecutionContext.StatementText.Substring(firstTerminalStartIndex, selectedTerminals[selectedTerminals.Length - 1].SourcePosition.IndexEnd - firstTerminalStartIndex + 1)
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
