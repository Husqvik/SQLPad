using System;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class UnnestInlineViewCommand : OracleCommandBase
	{
		private readonly OracleQueryBlock _parentQueryBlock;

		public const string Title = "Unnest";

		private UnnestInlineViewCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
			_parentQueryBlock = SemanticModel == null
				? null
				: SemanticModel.QueryBlocks.Select(qb => qb.ObjectReferences.FirstOrDefault(o => o.Type == ReferenceType.InlineView && o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == CurrentQueryBlock))
					.Where(o => o != null)
					.Select(o => o.Owner)
					.FirstOrDefault();
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			var canExecute = CurrentNode != null && CurrentNode.Id == Terminals.Select && _parentQueryBlock != null &&
				!CurrentQueryBlock.HasDistinctResultSet && CurrentQueryBlock.GroupByClause == null;

			// TODO: Add other rules preventing unnesting, e. g., nested analytic clause

			return canExecute;
		}

		protected override void Execute()
		{
			foreach (var columnReference in _parentQueryBlock.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.First().QueryBlocks.Count == 1 && c.ColumnNodeObjectReferences.First().QueryBlocks.First() == CurrentQueryBlock &&
				            (c.SelectListColumn == null || (!c.SelectListColumn.IsAsterisk && c.SelectListColumn.HasExplicitDefinition))))
			{
				var indextStart = (columnReference.OwnerNode ?? columnReference.ObjectNode ?? columnReference.ColumnNode).SourcePosition.IndexStart;
				var columnExpression = GetUnnestedColumnExpression(columnReference, ExecutionContext.StatementText);
				if (String.IsNullOrEmpty(columnExpression))
					continue;

				var segmentToReplace = new TextSegment
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

			var sourceFromClause = CurrentQueryBlock.RootNode.GetDescendantByPath(NonTerminals.FromClause);
			if (sourceFromClause != null)
			{
				segmentToRemove.Text = sourceFromClause.GetText(ExecutionContext.StatementText);
			}

			if (nodeToRemove.SourcePosition.IndexStart > 0 &&
				!ExecutionContext.StatementText[nodeToRemove.SourcePosition.IndexStart - 1].In(' ', '\t', '\n'))
			{
				segmentToRemove.Text = " " + segmentToRemove.Text;
			}

			ExecutionContext.SegmentsToReplace.Add(segmentToRemove);

			var objectPrefixAsteriskColumns = _parentQueryBlock.AsteriskColumns.Where(c => c.ColumnReferences.Count == 1 && c.ColumnReferences[0].ObjectNode != null &&
																				   c.ColumnReferences[0].ObjectNodeObjectReferences.Count == 1 && c.ColumnReferences[0].ObjectNodeObjectReferences.First().QueryBlocks.Count == 1 &&
																				   c.ColumnReferences[0].ObjectNodeObjectReferences.First().QueryBlocks.First() == CurrentQueryBlock);

			foreach (var objectPrefixAsteriskColumn in objectPrefixAsteriskColumns)
			{
				var asteriskToReplace = new TextSegment
				                        {
					                        IndextStart = objectPrefixAsteriskColumn.RootNode.SourcePosition.IndexStart,
											Length = objectPrefixAsteriskColumn.RootNode.SourcePosition.Length,
											Text = CurrentQueryBlock.SelectList.GetText(ExecutionContext.StatementText)
				                        };

				ExecutionContext.SegmentsToReplace.Add(asteriskToReplace);
			}

			var whereCondition = String.Empty;
			if (CurrentQueryBlock.WhereClause != null)
			{
				var whereConditionNode = CurrentQueryBlock.WhereClause.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Condition);
				if (whereConditionNode != null)
				{
					whereCondition = whereConditionNode.GetText(ExecutionContext.StatementText);
				}
			}

			if (String.IsNullOrEmpty(whereCondition))
				return;
			
			var whereConditionSegment = new TextSegment();

			if (_parentQueryBlock.WhereClause != null)
			{
				// TODO: Make proper condition resolution, if it's needed to encapsulate existing condition into parentheses to keep the logic
				whereCondition = " AND " + whereCondition;
				whereConditionSegment.IndextStart = _parentQueryBlock.WhereClause.SourcePosition.IndexEnd + 1;
			}
			else
			{
				var targetFromClause = _parentQueryBlock.RootNode.GetDescendantByPath(NonTerminals.FromClause);
				whereCondition = " WHERE " + whereCondition;
				whereConditionSegment.IndextStart = targetFromClause.SourcePosition.IndexEnd + 1;
			}

			whereConditionSegment.Text = whereCondition;
			ExecutionContext.SegmentsToReplace.Add(whereConditionSegment);
		}

		private string GetUnnestedColumnExpression(OracleColumnReference columnReference, string statementText)
		{
			return CurrentQueryBlock.Columns
				.Where(c => !c.IsAsterisk && c.NormalizedName == columnReference.NormalizedName)
				.Select(c => GetUnnestedColumnExpression(c, statementText))
				.FirstOrDefault();
		}

		private string GetUnnestedColumnExpression(OracleSelectListColumn column, string statementText)
		{
			if (column.HasExplicitDefinition)
			{
				var columnExpression = column.RootNode.GetDescendantsWithinSameQuery(NonTerminals.Expression).First().GetText(statementText);
				var offset = column.RootNode.SourcePosition.IndexStart;

				foreach (var columnReference in column.ColumnReferences
					.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ObjectNode == null)
					.OrderByDescending(c => c.ColumnNode.SourcePosition.IndexStart))
				{
					var prefix = columnReference.ColumnNodeObjectReferences.First().FullyQualifiedObjectName.ToString();
					if (!String.IsNullOrEmpty(prefix))
					{
						prefix += ".";
					}

					columnExpression = columnExpression.Remove(columnReference.ColumnNode.SourcePosition.IndexStart - offset, columnReference.ColumnNode.SourcePosition.Length).Insert(columnReference.ColumnNode.SourcePosition.IndexStart - offset, prefix + columnReference.Name);
				}

				return columnExpression;
			}

			var objectName = column.ColumnReferences.Count == 1 && column.ColumnReferences[0].ColumnNodeObjectReferences.Count == 1
				? column.ColumnReferences[0].ColumnNodeObjectReferences.First().FullyQualifiedObjectName + "."
				: null;

			return objectName + column.NormalizedName.ToSimpleIdentifier();
		}
	}
}
