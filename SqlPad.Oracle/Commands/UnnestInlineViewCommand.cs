using System;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class UnnestInlineViewCommand : OracleCommandBase
	{
		private readonly OracleQueryBlock _parentQueryBlock;

		public const string Title = "Unnest";

		private UnnestInlineViewCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
			_parentQueryBlock = SemanticModel?.QueryBlocks
				.Select(qb => qb.ObjectReferences.FirstOrDefault(o => o.Type == ReferenceType.InlineView && o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == CurrentQueryBlock))
				.Where(o => o != null)
				.Select(o => o.Owner)
				.FirstOrDefault();
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			var canExecute = CurrentNode != null && String.Equals(CurrentNode.Id, Terminals.Select) && _parentQueryBlock != null &&
				!CurrentQueryBlock.HasDistinctResultSet && CurrentQueryBlock.GroupByClause == null && !ContainConflictingAnalyticFunctions();

			// TODO: Add other rules preventing unnesting

			return canExecute;
		}

		private bool ContainConflictingAnalyticFunctions()
		{
			var parentAnalyticFunctionRootNodes = _parentQueryBlock.Columns
				.Where(c => c.HasExplicitDefinition)
				.SelectMany(c => c.ProgramReferences)
				.Where(p => p.AnalyticClauseNode != null)
				.Select(p => p.RootNode[0])
				.ToHashSet();

			var namedParentColumnReferences = _parentQueryBlock.Columns
				.Where(c => c.HasExplicitDefinition)
				.SelectMany(c => c.ColumnReferences)
				.Where(c => c.ValidObjectReference?.QueryBlocks.FirstOrDefault() == CurrentQueryBlock)
				.ToLookup(c => c.NormalizedName);

			foreach (var columnWithAnalyticFunction in CurrentQueryBlock.Columns.Where(OracleQueryBlock.PredicateContainsAnalyticFuction))
			{
				foreach (var parentColumnReference in namedParentColumnReferences[columnWithAnalyticFunction.NormalizedName])
				{
					var parentAnalyticFunctionRootNode = OracleStatementValidator.GetParentAggregateOrAnalyticFunctionRootNode(parentColumnReference.RootNode);
					if (parentAnalyticFunctionRootNodes.Contains(parentAnalyticFunctionRootNode))
					{
						return true;
					}
				}
			}

			return false;
		}

		protected override void Execute()
		{
			foreach (var columnReference in _parentQueryBlock.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.First().QueryBlocks.Count == 1 && c.ColumnNodeObjectReferences.First().QueryBlocks.First() == CurrentQueryBlock &&
				            (c.SelectListColumn == null || (!c.SelectListColumn.IsAsterisk && c.SelectListColumn.HasExplicitDefinition))))
			{
				var indextStart = (columnReference.OwnerNode ?? columnReference.ObjectNode ?? columnReference.ColumnNode).SourcePosition.IndexStart;
				var columnExpression = GetUnnestedColumnExpression(columnReference);
				if (String.IsNullOrEmpty(columnExpression))
				{
					continue;
				}

				var segmentToReplace =
					new TextSegment
					{
						IndextStart = indextStart,
						Length = columnReference.ColumnNode.SourcePosition.IndexEnd - indextStart + 1,
						Text = columnExpression
					};

				ExecutionContext.SegmentsToReplace.Add(segmentToReplace);
			}

			var nodeToRemove = CurrentQueryBlock.RootNode.GetAncestor(NonTerminals.TableReference);

			var segmentToRemove = new TextSegment
			{
				IndextStart = nodeToRemove.SourcePosition.IndexStart,
				Length = nodeToRemove.SourcePosition.Length,
				Text = String.Empty
			};

			var sourceFromClause = CurrentQueryBlock.RootNode[NonTerminals.FromClause];
			if (sourceFromClause != null)
			{
				segmentToRemove.Text = sourceFromClause.GetText(ExecutionContext.StatementText);
			}

			if (nodeToRemove.SourcePosition.IndexStart > 0 &&
				!ExecutionContext.StatementText[nodeToRemove.SourcePosition.IndexStart - 1].In(' ', '\t', '\n', '\u00A0'))
			{
				segmentToRemove.Text = $" {segmentToRemove.Text}";
			}

			ExecutionContext.SegmentsToReplace.Add(segmentToRemove);

			var objectPrefixAsteriskColumns = _parentQueryBlock.AsteriskColumns
				.Where(c =>
					c.ColumnReferences.Count == 1 && c.ColumnReferences[0].ObjectNode != null &&
					c.ColumnReferences[0].ObjectNodeObjectReferences.Count == 1 && c.ColumnReferences[0].ObjectNodeObjectReferences.First().QueryBlocks.Count == 1 &&
					c.ColumnReferences[0].ObjectNodeObjectReferences.First().QueryBlocks.First() == CurrentQueryBlock);

			foreach (var objectPrefixAsteriskColumn in objectPrefixAsteriskColumns)
			{
				var asteriskToReplace =
					new TextSegment
					{
						IndextStart = objectPrefixAsteriskColumn.RootNode.SourcePosition.IndexStart,
						Length = objectPrefixAsteriskColumn.RootNode.SourcePosition.Length,
						Text = CurrentQueryBlock.SelectList.GetText(ExecutionContext.StatementText)
					};

				ExecutionContext.SegmentsToReplace.Add(asteriskToReplace);
			}

			var whereCondition = String.Empty;
			var whereConditionNode = CurrentQueryBlock.WhereClause?.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.Condition));
			if (whereConditionNode != null)
			{
				whereCondition = whereConditionNode.GetText(ExecutionContext.StatementText);
			}

			if (String.IsNullOrEmpty(whereCondition))
			{
				return;
			}
			
			var whereConditionSegment = new TextSegment();

			if (_parentQueryBlock.WhereClause != null)
			{
				// TODO: Make proper condition resolution, if it's needed to encapsulate existing condition into parentheses to keep the logic
				whereCondition = $" AND {whereCondition}";
				whereConditionSegment.IndextStart = _parentQueryBlock.WhereClause.SourcePosition.IndexEnd + 1;
			}
			else
			{
				var targetFromClause = _parentQueryBlock.RootNode[NonTerminals.FromClause];
				whereCondition = $" WHERE {whereCondition}";
				whereConditionSegment.IndextStart = targetFromClause.SourcePosition.IndexEnd + 1;
			}

			whereConditionSegment.Text = whereCondition;
			ExecutionContext.SegmentsToReplace.Add(whereConditionSegment);
		}

		private string GetUnnestedColumnExpression(OracleColumnReference columnReference)
		{
			return CurrentQueryBlock.Columns
				.Where(c => !c.IsAsterisk && String.Equals(c.NormalizedName, columnReference.NormalizedName))
				.Select(c => GetUnnestedColumnExpression(columnReference, c))
				.FirstOrDefault();
		}

		private string GetUnnestedColumnExpression(OracleColumnReference sourceColumnReference, OracleSelectListColumn column)
		{
			if (column.HasExplicitDefinition)
			{
				var isNonAliasedDirectReference = sourceColumnReference.SelectListColumn?.IsDirectReference == true && !sourceColumnReference.SelectListColumn.HasExplicitAlias;
				var rootExpressionNode = column.RootNode.GetDescendantsWithinSameQueryBlock(isNonAliasedDirectReference ? NonTerminals.AliasedExpression : NonTerminals.Expression).First();
				var columnExpression = rootExpressionNode.GetText(ExecutionContext.StatementText);
				var offset = column.RootNode.SourcePosition.IndexStart;

				foreach (var columnReference in column.ColumnReferences
					.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ObjectNode == null)
					.OrderByDescending(c => c.ColumnNode.SourcePosition.IndexStart))
				{
					var prefix = columnReference.ColumnNodeObjectReferences.First().FullyQualifiedObjectName.ToString();
					if (!String.IsNullOrEmpty(prefix))
					{
						prefix = $"{prefix}.";
					}

					columnExpression = columnExpression.Remove(columnReference.ColumnNode.SourcePosition.IndexStart - offset, columnReference.ColumnNode.SourcePosition.Length).Insert(columnReference.ColumnNode.SourcePosition.IndexStart - offset, prefix + columnReference.Name);
				}

				return columnExpression;
			}

			var objectName = column.ColumnReferences.Count == 1 && column.ColumnReferences[0].ColumnNodeObjectReferences.Count == 1
				? $"{column.ColumnReferences[0].ColumnNodeObjectReferences.First().FullyQualifiedObjectName}."
				: null;

			return $"{objectName}{column.NormalizedName.ToSimpleIdentifier()}";
		}
	}
}
