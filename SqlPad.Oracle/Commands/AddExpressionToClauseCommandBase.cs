using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;

namespace SqlPad.Oracle.Commands
{
	internal abstract class AddExpressionToClauseCommandBase : OracleCommandBase
	{
		protected string ExpressionText { get; private set; }
		protected IList<StatementGrammarNode> SelectedTerminals { get; private set; }
		protected TextSegment AddedTextSegment { get; private set; }

		protected AddExpressionToClauseCommandBase(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock?.FromClause == null || !CurrentQueryBlock.FromClause.IsGrammarValid)
			{
				return false;
			}

			ResolveExpressionText();

			AddedTextSegment = ResolveAddedTextSegment();

			return ExpressionText != null && !AddedTextSegment.Equals(TextSegment.Empty) && OracleSqlParser.Instance.IsRuleValid(OracleGrammarDescription.NonTerminals.ExpressionList, ExpressionText);
		}

		protected abstract TextSegment ResolveAddedTextSegment();

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.Add(AddedTextSegment);
		}

		protected static bool TerminalCollectionEqual(IEnumerable<StatementGrammarNode> terminalCollection1, IList<StatementGrammarNode> terminalCollection2)
		{
			var index = 0;
			return terminalCollection1.All(i => terminalCollection2.Count > index && ExpressionTerminalEquals(terminalCollection2[index++], i)) &&
			       index == terminalCollection2.Count;
		}

		private void ResolveExpressionText()
		{
			SelectedTerminals = CurrentQueryBlock.RootNode.Terminals
				.Where(IsTerminalSelected)
				.ToArray();

			if (SelectedTerminals.Count == 0)
			{
				return;
			}

			var isSelectionWithinExpression = SelectedTerminals.All(t => t.IsWithinSelectClauseOrExpression());
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
				.Where(c => !c.ReferencesAllColumns && c.HasExplicitDefinition && SelectedTerminals.Contains(c.ColumnNode))
				.ToArray();
			if (columnReferences.Length == 0)
			{
				return;
			}

			var functionReferences = CurrentQueryBlock.AllProgramReferences
				.Where(c => c.HasExplicitDefinition && SelectedTerminals.Contains(c.ProgramIdentifierNode));

			var references = ((IEnumerable<OracleReference>)columnReferences).Concat(functionReferences).OrderBy(r => r.RootNode.SourcePosition.IndexStart).ToArray();

			var firstTerminalStartIndex = SelectedTerminals[0].SourcePosition.IndexStart;
			var lastTerminalEndIndex = SelectedTerminals[SelectedTerminals.Count - 1].SourcePosition.IndexEnd;

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

			ExpressionText = ExecutionContext.StatementText.Substring(firstTerminalStartIndex, lastTerminalEndIndex - firstTerminalStartIndex + 1);
		}

		private bool IsTerminalSelected(StatementGrammarNode terminal)
		{
			var isWithinSelection = terminal.SourcePosition.IndexEnd + 1 >= ExecutionContext.SelectionStart && terminal.SourcePosition.IndexStart <= ExecutionContext.SelectionEnd;
			return isWithinSelection && ExecutionContext.SelectionLength == 0
				? terminal.Id.IsIdentifier() && !String.Equals(terminal.Id, OracleGrammarDescription.Terminals.BindVariableIdentifier)
				: terminal.SourcePosition.IndexEnd + 1 > ExecutionContext.SelectionStart && terminal.SourcePosition.IndexStart < ExecutionContext.SelectionEnd;
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