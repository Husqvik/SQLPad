using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class ConvertOrderByNumberColumnReferencesCommand : OracleCommandBase
	{
		public const string Title = "Convert to expressions";

		private ConvertOrderByNumberColumnReferencesCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null && GetNodesToReplace().Any();
		}

		private IEnumerable<StatementGrammarNode> GetNodesToReplace()
		{
			if (!CurrentNode.HasAncestor(CurrentQueryBlock.OrderByClause))
			{
				return StatementGrammarNode.EmptyArray;
			}

			if (CurrentNode.ParentNode == CurrentQueryBlock.OrderByClause)
			{
				return CurrentQueryBlock.OrderByClause.GetDescendantsWithinSameQuery(NonTerminals.OrderExpression)
					.Where(IsColumnIndex);
			}

			return CurrentNode.Id == Terminals.NumberLiteral && CurrentNode.ParentNode.ParentNode.Id == NonTerminals.OrderExpression && IsColumnIndex(CurrentNode.ParentNode.ParentNode)
				? Enumerable.Repeat(CurrentNode.ParentNode.ParentNode, 1)
				: StatementGrammarNode.EmptyArray;
		}

		private bool IsColumnIndex(StatementGrammarNode orderExpression)
		{
			return orderExpression.TerminalCount == 1 && orderExpression.FirstTerminalNode.Id == Terminals.NumberLiteral &&
			       orderExpression.FirstTerminalNode.Token.Value.IndexOf('.') == -1 &&
				   Convert.ToInt32(orderExpression.FirstTerminalNode.Token.Value) <= CurrentQueryBlock.Columns.Count(c => !c.IsAsterisk);
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.AddRange(GetReplacedSegments());
		}

		private IEnumerable<TextSegment> GetReplacedSegments()
		{
			var columns = CurrentQueryBlock.Columns.Where(c => !c.IsAsterisk).ToArray();
			foreach (var node in GetNodesToReplace())
			{
				var columnIndex = Convert.ToInt32(node.FirstTerminalNode.Token.Value) - 1;
				var column = columns[columnIndex];
				var columnReference = column.ColumnReferences.FirstOrDefault();
				var prefix = columnReference == null || columnReference.ValidObjectReference == null
					? null
					: String.Format("{0}.", columnReference.ValidObjectReference.FullyQualifiedObjectName);
				
				var expressionText = column.HasExplicitDefinition
					? column.RootNode.GetText(ExecutionContext.StatementText)
					: String.Format("{0}{1}", prefix, column.NormalizedName.ToSimpleIdentifier());

				yield return new TextSegment
				{
					IndextStart = node.SourcePosition.IndexStart,
					Length = node.SourcePosition.Length,
					Text = expressionText
				};
			}
		}
	}
}
