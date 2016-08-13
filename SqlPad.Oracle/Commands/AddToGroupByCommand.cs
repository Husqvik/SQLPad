using System;
using System.Linq;
using SqlPad.Commands;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddToGroupByCommand : AddExpressionToClauseCommandBase
	{
		public const string Title = "Add to GROUP BY clause";

		private AddToGroupByCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override TextSegment ResolveAddedTextSegment()
		{
			var groupByClause = CurrentQueryBlock.RootNode[NonTerminals.GroupByClause];
			if (groupByClause == null)
			{
				var targetNode =
					CurrentQueryBlock.RootNode[NonTerminals.HierarchicalQueryClause]
					?? CurrentQueryBlock.RootNode[NonTerminals.WhereClause]
					?? CurrentQueryBlock.FromClause;

				if (targetNode?.LastTerminalNode != null)
				{
					return
						new TextSegment
						{
							IndextStart = targetNode.LastTerminalNode.SourcePosition.IndexEnd + 1,
							Length = 0,
							Text = $" GROUP BY {ExpressionText}"
						};
				}

				return TextSegment.Empty;
			}

			var groupingExpressions =
				groupByClause.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.GroupingSetsClause, NonTerminals.RollupCubeClause, NonTerminals.NestedQuery, NonTerminals.HavingClause), NonTerminals.GroupingClause)
					.Where(n => n.ChildNodes.Count > 0 && String.Equals(n.ChildNodes[0].Id, NonTerminals.Expression));

			StatementGrammarNode lastGroupingExpression = null;
			foreach (var groupingExpression in groupingExpressions)
			{
				if (TerminalCollectionEqual(groupingExpression.Terminals, SelectedTerminals))
				{
					return TextSegment.Empty;
				}

				lastGroupingExpression = groupingExpression;
			}

			var commaPrefix = lastGroupingExpression == null ? String.Empty : ", ";
			return
				new TextSegment
				{
					IndextStart = (lastGroupingExpression?.SourcePosition.IndexEnd ?? groupByClause.SourcePosition.IndexEnd) + 1,
					Length = 0,
					Text = $"{commaPrefix}{ExpressionText}"
				};
		}
	}
}
