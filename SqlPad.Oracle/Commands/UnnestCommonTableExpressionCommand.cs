using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class UnnestCommonTableExpressionCommand : OracleCommandBase
	{
		private OracleQueryBlock _commonTableExpressionQueryBlock;
		private ICollection<OracleObjectReference> _commonTableExpressionReferences;

		public UnnestCommonTableExpressionCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
			: base(semanticModel, currentTerminal)
		{
		}

		public override bool CanExecute(object parameter)
		{
			if (SemanticModel == null)
				return false;

			_commonTableExpressionQueryBlock = SemanticModel.QueryBlocks.SingleOrDefault(qb => qb.AliasNode == CurrentNode);
			_commonTableExpressionReferences = SemanticModel.QueryBlocks
				.SelectMany(qb => qb.ObjectReferences)
				.Where(o => o.Type == TableReferenceType.CommonTableExpression && o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == _commonTableExpressionQueryBlock)
				.ToArray();

			// TODO: Add check for analytic clauses and other features preventing from unnesting.

			return CurrentNode != null && CurrentNode.Id == Terminals.ObjectAlias && _commonTableExpressionQueryBlock != null &&
			       _commonTableExpressionReferences.Count > 0;
		}

		public override string Title
		{
			get { return "Unnest"; }
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			//var cteText = _commonTableExpressionQueryBlock.RootNode.GetStatementSubstring(statementText);

			foreach (var cteReference in _commonTableExpressionReferences.Where(r => !r.Owner.HasAsteriskClause))
			{
				var columnExpressions = cteReference.QueryBlocks.First().Columns
					.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
					.ToDictionary(c => c.NormalizedName, c => c.RootNode.GetDescendantsWithinSameQuery(NonTerminals.Expression).First().GetStatementSubstring(statementText));

				foreach (var columnReference in cteReference.Owner.AllColumnReferences
					.Where(c => c.ColumnNodeObjectReferences.Count == 1 && (c.SelectListColumn == null || (!c.SelectListColumn.IsAsterisk && c.SelectListColumn.ExplicitDefinition)) && c.ColumnNodeObjectReferences.First() == cteReference))
				{
					var indextStart = (columnReference.OwnerNode ?? columnReference.ObjectNode ?? columnReference.ColumnNode).SourcePosition.IndexStart;
					var segmentToReplace = new TextSegment
					                  {
										  IndextStart = indextStart,
										  Length = columnReference.ColumnNode.SourcePosition.IndexEnd - indextStart + 1,
										  Text = columnExpressions[columnReference.NormalizedName]
					                  };

					segmentsToReplace.Add(segmentToReplace);
				}
				/*segmentsToReplace.Add(new TextSegment
				                      {
					                      IndextStart = cteReference.TableReferenceNode.SourcePosition.IndexStart,
										  Length = cteReference.TableReferenceNode.SourcePosition.Length,
										  Text = "(" + cteText + ")"
				                      });*/
			}

			var subqueryFactoringNode = _commonTableExpressionQueryBlock.RootNode.GetAncestor(NonTerminals.SubqueryFactoringClause);
			var isLastCte = subqueryFactoringNode.GetDescendantsWithinSameQuery(NonTerminals.SubqueryComponent).Count() == 1;
			var subqueryComponentNode = _commonTableExpressionQueryBlock.RootNode.GetAncestor(NonTerminals.SubqueryComponent);

			StatementDescriptionNode removedNode;
			int indexStart;
			int removedLength;
			if (isLastCte)
			{
				removedNode = subqueryFactoringNode;
				indexStart = removedNode.SourcePosition.IndexStart;
				removedLength = removedNode.SourcePosition.Length;
			}
			else
			{
				int indexEnd;
				removedNode = subqueryComponentNode;
				var followingCteNode = subqueryComponentNode.GetDescendants(NonTerminals.SubqueryComponent).FirstOrDefault();
				var precedingCteNode = subqueryComponentNode.GetAncestor(NonTerminals.SubqueryComponent);
				if (followingCteNode != null)
				{
					indexStart = removedNode.SourcePosition.IndexStart;
					indexEnd = followingCteNode.SourcePosition.IndexStart - 1;
				}
				else if (precedingCteNode != null)
				{
					indexStart = precedingCteNode.SourcePosition.IndexEnd + 1;
					indexEnd = subqueryComponentNode.SourcePosition.IndexEnd;
				}
				else
				{
					indexStart = removedNode.SourcePosition.IndexStart;
					indexEnd = subqueryComponentNode.SourcePosition.IndexEnd;
				}

				removedLength = indexEnd - removedNode.SourcePosition.IndexStart + 1;
			}

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = indexStart,
				Length = removedLength,
				Text = String.Empty
			});
		}
	}
}
