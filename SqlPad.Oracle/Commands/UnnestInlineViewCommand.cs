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
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 && (c.SelectListColumn == null || (!c.SelectListColumn.IsAsterisk && c.SelectListColumn.ExplicitDefinition))))
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
				.Select(c => c.ExplicitDefinition
					? c.RootNode.GetDescendantsWithinSameQuery(NonTerminals.Expression).First().GetStatementSubstring(statementText)
					: c.NormalizedName.ToSimpleIdentifier())
				.FirstOrDefault();
		}
	}
}
