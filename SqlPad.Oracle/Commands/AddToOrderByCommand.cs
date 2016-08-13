using System;
using System.Linq;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal class AddToOrderByCommand : AddExpressionToClauseCommandBase
	{
		public const string Title = "Add to ORDER BY clause";

		private AddToOrderByCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override TextSegment ResolveAddedTextSegment()
		{
			var subqueryNode = CurrentQueryBlock.RootNode.GetAncestor(OracleGrammarDescription.NonTerminals.Subquery);
			var orderByClause = subqueryNode[OracleGrammarDescription.NonTerminals.OrderByClause];
			if (orderByClause == null)
			{
				var targetNode = subqueryNode[OracleGrammarDescription.NonTerminals.OptionalParenthesisEnclosedConcatenatedQueryBlock];
				return
					new TextSegment
					{
						IndextStart = targetNode.LastTerminalNode.SourcePosition.IndexEnd + 1,
						Length = 0,
						Text = $" ORDER BY {ExpressionText}"
					};
			}

			var orderExpressions =
				orderByClause.GetPathFilterDescendants(n => !n.Id.In(OracleGrammarDescription.NonTerminals.NestedQuery), OracleGrammarDescription.NonTerminals.OrderExpression)
					.Where(n => n.ChildNodes.Count > 0);

			StatementGrammarNode lastOrderExpression = null;
			foreach (var orderExpression in orderExpressions)
			{
				if (TerminalCollectionEqual(orderExpression.Terminals, SelectedTerminals))
				{
					return TextSegment.Empty;
				}

				lastOrderExpression = orderExpression;
			}

			var commaPrefix = lastOrderExpression == null ? String.Empty : ", ";
			return
				new TextSegment
				{
					IndextStart = (lastOrderExpression?.SourcePosition.IndexEnd ?? orderByClause.SourcePosition.IndexEnd) + 1,
					Length = 0,
					Text = $"{commaPrefix}{ExpressionText}"
				};
		}
	}
}