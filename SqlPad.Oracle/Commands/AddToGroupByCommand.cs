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

		private string _expressionText;
		private IList<StatementGrammarNode> _selectedTerminals;
		private TextSegment _addedTextSegment = TextSegment.Empty;

		private AddToGroupByCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock?.FromClause == null || !CurrentQueryBlock.FromClause.IsGrammarValid)
			{
				return false;
			}

			ResolveGroupingExpressionText();

			ResolveAddedTextSegment();

			return _expressionText != null && !_addedTextSegment.Equals(TextSegment.Empty) && OracleSqlParser.Instance.IsRuleValid(NonTerminals.ExpressionList, _expressionText);
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.Add(_addedTextSegment);
		}

		private bool IsTerminalSelected(StatementGrammarNode terminal)
		{
			var isWithinSelection = terminal.SourcePosition.IndexEnd + 1 >= ExecutionContext.SelectionStart && terminal.SourcePosition.IndexStart <= ExecutionContext.SelectionEnd;
			return isWithinSelection && ExecutionContext.SelectionLength == 0
				? terminal.Id.IsIdentifier() && !String.Equals(terminal.Id, Terminals.BindVariableIdentifier)
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
				.Where(c => c.HasExplicitDefinition && _selectedTerminals.Contains(c.ProgramIdentifierNode));

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

			_expressionText = ExecutionContext.StatementText.Substring(firstTerminalStartIndex, lastTerminalEndIndex - firstTerminalStartIndex + 1);
		}

		private void ResolveAddedTextSegment()
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
					_addedTextSegment =
						new TextSegment
						{
							IndextStart = targetNode.LastTerminalNode.SourcePosition.IndexEnd + 1,
							Length = 0,
							Text = " GROUP BY " + _expressionText
						};
				}
			}
			else
			{
				var groupingExpressions =
					groupByClause.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.GroupingSetsClause, NonTerminals.RollupCubeClause, NonTerminals.NestedQuery, NonTerminals.HavingClause), NonTerminals.GroupingClause)
						.Where(n => n.ChildNodes.Count > 0 && String.Equals(n.ChildNodes[0].Id, NonTerminals.Expression));

				StatementGrammarNode lastGroupingExpression = null;
				foreach (var groupingExpression in groupingExpressions)
				{
					if (TerminalCollectionEqual(groupingExpression.Terminals, _selectedTerminals))
					{
						return;
					}

					lastGroupingExpression = groupingExpression;
				}

				var commaPrefix = lastGroupingExpression == null ? String.Empty : ", ";
				_addedTextSegment =
					new TextSegment
					{
						IndextStart = (lastGroupingExpression?.SourcePosition.IndexEnd ?? groupByClause.SourcePosition.IndexEnd) + 1,
						Length = 0,
						Text = $"{commaPrefix}{_expressionText}"
					};
			}
		}

		private static bool TerminalCollectionEqual(IEnumerable<StatementGrammarNode> terminalCollection1, IList<StatementGrammarNode> terminalCollection2)
		{
			var index = 0;
			return terminalCollection1.All(i => terminalCollection2.Count > index && ExpressionTerminalEquals(terminalCollection2[index++], i)) &&
			       index == terminalCollection2.Count;
		}

		private static bool ExpressionTerminalEquals(StatementGrammarNode terminal1, StatementGrammarNode terminal2)
		{
			if (terminal1.Id.IsIdentifierOrAlias() || terminal2.Id.IsIdentifierOrAlias())
			{
				return String.Equals(terminal1.Id, terminal2.Id) && String.Equals(terminal1.Token.Value.ToQuotedIdentifier(), terminal2.Token.Value.ToQuotedIdentifier());
			}

			if (terminal1.Id.IsLiteral() || terminal2.Id.IsLiteral())
			{
				return String.Equals(terminal1.Id, terminal2.Id) && String.Equals(terminal1.Token.Value, terminal2.Token.Value);
			}

			return String.Equals(terminal1.Id, terminal2.Id);
		}
	}
}
