using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddToGroupByCommand : OracleCommandBase
	{
		public const string Title = "Add to GROUP BY clause";

		private static readonly OracleSqlParser Parser = new OracleSqlParser();

		private string _groupingExpressionText;
		private IList<StatementGrammarNode> _selectedTerminals;
		private TextSegment _addedTextSegment = TextSegment.Empty;

		private AddToGroupByCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null || CurrentQueryBlock.FromClause == null || !CurrentQueryBlock.FromClause.IsGrammarValid)
			{
				return false;
			}

			ResolveGroupingExpressionText();

			ResolveAddedTextSegment();

			return _groupingExpressionText != null && !_addedTextSegment.Equals(TextSegment.Empty) && Parser.IsRuleValid(NonTerminals.ExpressionList, _groupingExpressionText);
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.Add(_addedTextSegment);
		}

		private bool IsTerminalSelected(StatementGrammarNode terminal)
		{
			var isWithinSelection = terminal.SourcePosition.IndexEnd + 1 >= ExecutionContext.SelectionStart && terminal.SourcePosition.IndexStart <= ExecutionContext.SelectionEnd;
			return isWithinSelection && ExecutionContext.SelectionLength == 0
				? terminal.Id.IsIdentifier() && terminal.Id != Terminals.BindVariableIdentifier
				: terminal.SourcePosition.IndexEnd + 1 > ExecutionContext.SelectionStart && terminal.SourcePosition.IndexStart < ExecutionContext.SelectionEnd;
		}

		private void ResolveGroupingExpressionText()
		{
			_selectedTerminals = CurrentQueryBlock.RootNode.Terminals
				.Where(IsTerminalSelected)
				.ToArray();

			if (_selectedTerminals.Count == 0)
			{
				return;
			}

			var isSelectionWithinExpression = _selectedTerminals.All(t => t.IsWithinSelectClauseOrExpression());
			if (!isSelectionWithinExpression)
			{
				return;
			}

			var isSequenceWithinSelection = CurrentQueryBlock.AllSequenceReferences.Any(s => s.RootNode.SourcePosition.IndexStart <= ExecutionContext.SelectionEnd && s.RootNode.SourcePosition.IndexEnd + 1 >= ExecutionContext.SelectionStart);
			if (isSequenceWithinSelection)
			{
				return;
			}

			var columnReferences = CurrentQueryBlock.AllColumnReferences
				.Where(c => !c.ReferencesAllColumns && c.HasExplicitDefinition && _selectedTerminals.Contains(c.ColumnNode))
				.ToArray();
			if (columnReferences.Length == 0)
			{
				return;
			}

			var functionReferences = CurrentQueryBlock.AllProgramReferences
				.Where(c => c.HasExplicitDefinition && _selectedTerminals.Contains(c.FunctionIdentifierNode));

			var references = ((IEnumerable<OracleReference>)columnReferences).Concat(functionReferences).OrderBy(r => r.RootNode.SourcePosition.IndexStart).ToArray();

			var firstTerminalStartIndex = _selectedTerminals[0].SourcePosition.IndexStart;
			var lastTerminalEndIndex = _selectedTerminals[_selectedTerminals.Count - 1].SourcePosition.IndexEnd;

			if (references.Length > 0)
			{
				var firstColumnReference = references[0];
				if (firstColumnReference.RootNode.SourcePosition.IndexStart < firstTerminalStartIndex)
				{
					firstTerminalStartIndex = firstColumnReference.RootNode.SourcePosition.IndexStart;
				}

				var lastColumnReference = references[references.Length - 1];
				if (lastColumnReference.RootNode.SourcePosition.IndexEnd > lastTerminalEndIndex)
				{
					lastTerminalEndIndex = firstColumnReference.RootNode.SourcePosition.IndexEnd;
				}
			}

			_groupingExpressionText = ExecutionContext.StatementText.Substring(firstTerminalStartIndex, lastTerminalEndIndex - firstTerminalStartIndex + 1);
		}

		private void ResolveAddedTextSegment()
		{
			var groupByClause = CurrentQueryBlock.RootNode[NonTerminals.GroupByClause];
			if (groupByClause == null)
			{
				var targetNode = CurrentQueryBlock.RootNode[NonTerminals.HierarchicalQueryClause]
				                 ?? CurrentQueryBlock.RootNode[NonTerminals.WhereClause]
				                 ?? CurrentQueryBlock.FromClause;

				if (targetNode != null && targetNode.LastTerminalNode != null)
				{
					_addedTextSegment =
						new TextSegment
						{
							IndextStart = targetNode.LastTerminalNode.SourcePosition.IndexEnd + 1,
							Length = 0,
							Text = " GROUP BY " + _groupingExpressionText
						};
				}
			}
			else
			{
				var groupingExpressions = groupByClause.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.GroupingSetsClause, NonTerminals.RollupCubeClause, NonTerminals.NestedQuery, NonTerminals.HavingClause), NonTerminals.GroupingClause)
					.Where(n => n.ChildNodes.Count > 0 && n.ChildNodes[0].Id == NonTerminals.Expression);

				StatementGrammarNode lastGroupingExpression = null;
				foreach (var groupingExpression in groupingExpressions)
				{
					if (TerminalCollectionEqual(groupingExpression.Terminals, _selectedTerminals))
					{
						return;
					}

					lastGroupingExpression = groupingExpression;
				}

				_addedTextSegment =
					new TextSegment
					{
						IndextStart = (lastGroupingExpression == null ? groupByClause.SourcePosition.IndexEnd : lastGroupingExpression.SourcePosition.IndexEnd) + 1,
						Length = 0,
						Text = (lastGroupingExpression == null ? String.Empty : ", ") + _groupingExpressionText
					};
			}
		}

		private static bool TerminalCollectionEqual(IEnumerable<StatementGrammarNode> terminalCollection1, IList<StatementGrammarNode> terminalCollection2)
		{
			var index = 0;
			return terminalCollection1.All(i => terminalCollection2.Count > index && GroupingExpressionTerminalEquals(terminalCollection2[index++], i)) &&
			       index == terminalCollection2.Count;
		}

		private static bool GroupingExpressionTerminalEquals(StatementGrammarNode terminal1, StatementGrammarNode terminal2)
		{
			if (terminal1.Id.IsIdentifierOrAlias() || terminal2.Id.IsIdentifierOrAlias())
			{
				return terminal1.Id == terminal2.Id && terminal1.Token.Value.ToQuotedIdentifier() == terminal2.Token.Value.ToQuotedIdentifier();
			}

			if (terminal1.Id.IsLiteral() || terminal2.Id.IsLiteral())
			{
				return terminal1.Id == terminal2.Id && terminal1.Token.Value == terminal2.Token.Value;
			}

			return terminal1.Id == terminal2.Id;
		}
	}
}
