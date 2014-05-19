using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class UnnestInlineViewCommand : OracleCommandBase
	{
		private readonly OracleQueryBlock _parentQueryBlock;

		public UnnestInlineViewCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
			: base(semanticModel, currentTerminal)
		{
			_parentQueryBlock = SemanticModel == null
				? null
				: SemanticModel.QueryBlocks.Select(qb => qb.ObjectReferences.FirstOrDefault(o => o.Type == TableReferenceType.InlineView && o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == CurrentQueryBlock))
					.Where(o => o != null)
					.Select(o => o.Owner)
					.FirstOrDefault();
		}

		public override bool CanExecute(object parameter)
		{
			var canExecute = CurrentNode != null && CurrentNode.Id == Terminals.Select && _parentQueryBlock != null &&
				!CurrentQueryBlock.HasDistinctResultSet && CurrentQueryBlock.GroupByClause == null;

			// TODO: Add other rules preventing unnesting, e. g., nested analytic clause

			return canExecute;
		}

		public override string Title
		{
			get { return "Unnest"; }
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			foreach (var columnReference in _parentQueryBlock.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.First().QueryBlocks.Count == 1 && c.ColumnNodeObjectReferences.First().QueryBlocks.First() == CurrentQueryBlock &&
				            (c.SelectListColumn == null || (!c.SelectListColumn.IsAsterisk && c.SelectListColumn.ExplicitDefinition))))
			{
				var indextStart = (columnReference.OwnerNode ?? columnReference.ObjectNode ?? columnReference.ColumnNode).SourcePosition.IndexStart;
				var columnExpression = GetUnnestedColumnExpression(columnReference, statementText);
				if (String.IsNullOrEmpty(columnExpression))
					continue;

				var segmentToReplace = new TextSegment
				                       {
					                       IndextStart = indextStart,
					                       Length = columnReference.ColumnNode.SourcePosition.IndexEnd - indextStart + 1,
					                       Text = columnExpression
				                       };

				segmentsToReplace.Add(segmentToReplace);
			}

			var nodeToRemove = CurrentQueryBlock.RootNode.GetAncestor(NonTerminals.TableReference);

			var segmentToRemove = new TextSegment
			{
				IndextStart = nodeToRemove.SourcePosition.IndexStart,
				Length = nodeToRemove.SourcePosition.Length,
				Text = String.Empty
			};

			var sourceFromClause = CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
			if (sourceFromClause != null)
			{
				segmentToRemove.Text = sourceFromClause.GetStatementSubstring(statementText);
			}

			if (nodeToRemove.SourcePosition.IndexStart > 0 &&
				!statementText[nodeToRemove.SourcePosition.IndexStart - 1].In(' ', '\t', '\n'))
			{
				segmentToRemove.Text = " " + segmentToRemove.Text;
			}

			segmentsToReplace.Add(segmentToRemove);

			var objectPrefixAsteriskColumns = _parentQueryBlock.Columns.Where(c => c.IsAsterisk && c.ColumnReferences.Count == 1 && c.ColumnReferences.First().ObjectNode != null &&
			                                                                       c.ColumnReferences.First().ObjectNodeObjectReferences.Count == 1 && c.ColumnReferences.First().ObjectNodeObjectReferences.First().QueryBlocks.Count == 1 &&
			                                                                       c.ColumnReferences.First().ObjectNodeObjectReferences.First().QueryBlocks.First() == CurrentQueryBlock);

			foreach (var objectPrefixAsteriskColumn in objectPrefixAsteriskColumns)
			{
				var asteriskToReplace = new TextSegment
				                        {
					                        IndextStart = objectPrefixAsteriskColumn.RootNode.SourcePosition.IndexStart,
											Length = objectPrefixAsteriskColumn.RootNode.SourcePosition.Length,
											Text = CurrentQueryBlock.SelectList.GetStatementSubstring(statementText)
				                        };

				segmentsToReplace.Add(asteriskToReplace);
			}

			var whereCondition = String.Empty;
			if (CurrentQueryBlock.WhereClause != null)
			{
				var whereConditionNode = CurrentQueryBlock.WhereClause.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Condition);
				if (whereConditionNode != null)
				{
					whereCondition = whereConditionNode.GetStatementSubstring(statementText);
				}
			}

			if (String.IsNullOrEmpty(whereCondition))
				return;
			
			var whereConditionSegment = new TextSegment();

			if (_parentQueryBlock.WhereClause != null)
			{
				whereCondition = " AND " + whereCondition;
				whereConditionSegment.IndextStart = _parentQueryBlock.WhereClause.SourcePosition.IndexEnd + 1;
			}
			else
			{
				var targetFromClause = _parentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause).First();
				whereCondition = " WHERE " + whereCondition;
				whereConditionSegment.IndextStart = targetFromClause.SourcePosition.IndexEnd + 1;
			}

			whereConditionSegment.Text = whereCondition;
			segmentsToReplace.Add(whereConditionSegment);
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
			if (column.ExplicitDefinition)
			{
				var columnExpression = column.RootNode.GetDescendantsWithinSameQuery(NonTerminals.Expression).First().GetStatementSubstring(statementText);
				var offset = column.RootNode.SourcePosition.IndexStart;

				foreach (var columnReference in column.ColumnReferences
					.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ObjectNode == null)
					.OrderByDescending(c => c.ColumnNode.SourcePosition.IndexStart))
				{
					var prefix = columnReference.ColumnNodeObjectReferences.First().FullyQualifiedName.ToString();
					if (!String.IsNullOrEmpty(prefix))
					{
						prefix += ".";
					}

					columnExpression = columnExpression.Remove(columnReference.ColumnNode.SourcePosition.IndexStart - offset, columnReference.ColumnNode.SourcePosition.Length).Insert(columnReference.ColumnNode.SourcePosition.IndexStart - offset, prefix + columnReference.Name);
				}

				return columnExpression;
			}

			var objectName = column.ColumnReferences.Count == 1 && column.ColumnReferences.First().ColumnNodeObjectReferences.Count == 1
				? column.ColumnReferences.First().ColumnNodeObjectReferences.First().FullyQualifiedName + "."
				: null;

			return objectName + column.NormalizedName.ToSimpleIdentifier();
		}
	}
}
