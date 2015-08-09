using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class ConvertOrderByNumberColumnReferencesCommand : OracleCommandBase
	{
		public const string Title = "Convert to expression";

		private ConvertOrderByNumberColumnReferencesCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction
		{
			get { return n => n.Id != Terminals.Comma; }
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

			var columnIndexReferences = CurrentQueryBlock.OrderByColumnIndexReferences.Where(r => r.Terminal == CurrentNode);
			if (CurrentNode.ParentNode == CurrentQueryBlock.OrderByClause)
			{
				columnIndexReferences = CurrentQueryBlock.OrderByColumnIndexReferences;
			}

			return columnIndexReferences.Where(r => r.IsValid)
				.Select(r => r.Terminal);
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
				var prefix = columnReference == null || columnReference.ValidObjectReference == null || column.HasExplicitAlias
					? null
					: $"{columnReference.ValidObjectReference.FullyQualifiedObjectName}.";
				
				var expressionText = column.HasExplicitDefinition && !column.HasExplicitAlias
					? column.RootNode.GetText(ExecutionContext.StatementText)
					: $"{prefix}{column.NormalizedName.ToSimpleIdentifier()}";

				yield return
					new TextSegment
					{
						IndextStart = node.SourcePosition.IndexStart,
						Length = node.SourcePosition.Length,
						Text = expressionText
					};
			}
		}
	}
}
