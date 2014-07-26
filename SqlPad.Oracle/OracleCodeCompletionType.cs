using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SqlPad.Oracle
{
	internal class OracleCodeCompletionType
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();
		private static readonly char[] Separators = { ' ', '\t', '\r', '\n' };

		public bool Schema { get; private set; }

		public bool SchemaDataObject { get; private set; }
		
		public bool PipelinedFunction { get; private set; }
		
		public bool SchemaDataObjectReference { get; private set; }
		
		public bool Column { get; private set; }

		public bool AllColumns { get; private set; }
		
		public bool JoinType { get; private set; }
		
		public bool JoinCondition { get; private set; }
		
		public bool Program { get; private set; }
		
		public bool InUnparsedData { get; private set; }
		
		public StatementGrammarNode CurrentTerminal { get; private set; }

		private bool Any
		{
			get { return Schema || SchemaDataObject || PipelinedFunction || SchemaDataObjectReference || Column || AllColumns || JoinType || JoinCondition || Program; }
		}

		public bool ExistsTerminalValue { get { return !String.IsNullOrEmpty(TerminalValuePartUntilCaret); } }
		
		public string TerminalValuePartUntilCaret { get; private set; }
		
		public string TerminalValueUnderCursor { get; private set; }

		public OracleCodeCompletionType(StatementCollection statementCollection, string statementText, int cursorPosition)
		{
			var statement = (OracleStatement)(statementCollection.GetStatementAtPosition(cursorPosition) ?? statementCollection.LastOrDefault());
			if (statement == null)
				return;

			var nearestTerminal = statement.GetNearestTerminalToPosition(cursorPosition);
			if (nearestTerminal == null)
				return;

			var requiredOffsetAfterToken = nearestTerminal.Id.IsZeroOffsetTerminalId() ? 0 : 1;
			var isCursorAfterToken = nearestTerminal.SourcePosition.IndexEnd + requiredOffsetAfterToken < cursorPosition;
			if (isCursorAfterToken)
			{
				var unparsedTextBetweenTokenAndCursor = statementText.Substring(nearestTerminal.SourcePosition.IndexEnd + 1, cursorPosition - nearestTerminal.SourcePosition.IndexEnd - 1).Trim();
				var extraUnparsedTokens = unparsedTextBetweenTokenAndCursor.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
				if (extraUnparsedTokens.Length > 1)
				{
					InUnparsedData = true;
					return;
				}

				TerminalValueUnderCursor = extraUnparsedTokens.FirstOrDefault();

				if (TerminalValueUnderCursor != null)
				{
					TerminalValuePartUntilCaret = TerminalValueUnderCursor;
					CurrentTerminal = new StatementGrammarNode(NodeType.Terminal, statement, new OracleToken(TerminalValueUnderCursor, cursorPosition - TerminalValuePartUntilCaret.Length));
				}
			}
			else
			{
				CurrentTerminal = nearestTerminal;

				if (nearestTerminal.Id.IsIdentifierOrAlias())
				{
					TerminalValueUnderCursor = nearestTerminal.Token.Value;
					TerminalValuePartUntilCaret = nearestTerminal.Token.Value.Substring(0, cursorPosition - nearestTerminal.SourcePosition.IndexStart).Trim('"');
				}
			}

			var precedingTerminal = nearestTerminal.PrecedingTerminal;
			var isCursorTouchingTwoTerminals = nearestTerminal.SourcePosition.IndexStart == cursorPosition && precedingTerminal != null && precedingTerminal.SourcePosition.IndexEnd + 1 == cursorPosition;
			if (isCursorTouchingTwoTerminals)
			{
				precedingTerminal = precedingTerminal.PrecedingTerminal;
			}

			var terminalCandidates = new HashSet<string>(_parser.GetTerminalCandidates(isCursorAfterToken ? nearestTerminal : precedingTerminal));
			Schema = terminalCandidates.Contains(OracleGrammarDescription.Terminals.SchemaIdentifier);
			Program = Column = terminalCandidates.Contains(OracleGrammarDescription.Terminals.Identifier);
			JoinType = !isCursorTouchingTwoTerminals && terminalCandidates.Contains(OracleGrammarDescription.Terminals.Join);

			var isWithinFromClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != OracleGrammarDescription.NonTerminals.QueryBlock, OracleGrammarDescription.NonTerminals.FromClause) != null || (isCursorAfterToken && nearestTerminal.Id == OracleGrammarDescription.Terminals.From);
			var isWithinJoinCondition = nearestTerminal.GetPathFilterAncestor(n => n.Id != OracleGrammarDescription.NonTerminals.JoinClause, OracleGrammarDescription.NonTerminals.JoinColumnsOrCondition) != null;
			SchemaDataObject = isWithinFromClause && !isWithinJoinCondition && terminalCandidates.Contains(OracleGrammarDescription.Terminals.ObjectIdentifier);

			var isWithinJoinClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != OracleGrammarDescription.NonTerminals.FromClause, OracleGrammarDescription.NonTerminals.JoinClause) != null;
			JoinCondition = isWithinJoinClause && isCursorAfterToken && (terminalCandidates.Contains(OracleGrammarDescription.Terminals.On) || nearestTerminal.Id == OracleGrammarDescription.Terminals.On);

			var isWithinSelectList = (nearestTerminal.Id == OracleGrammarDescription.Terminals.Select && isCursorAfterToken) || nearestTerminal.GetPathFilterAncestor(n => n.Id != OracleGrammarDescription.NonTerminals.QueryBlock, OracleGrammarDescription.NonTerminals.SelectList) != null;
			AllColumns = isWithinSelectList && terminalCandidates.Contains(OracleGrammarDescription.Terminals.Asterisk);

			SchemaDataObjectReference = !isWithinFromClause && terminalCandidates.Contains(OracleGrammarDescription.Terminals.ObjectIdentifier);
		}

		public void PrintSupportedCompletions()
		{
			if (!Any)
			{
				Trace.WriteLine("No completions available");
				return;
			}

			var builder = new StringBuilder(255);
			builder.Append("TerminalValueUnderCursor: '");
			builder.Append(TerminalValueUnderCursor);
			builder.Append("'; ");
			builder.Append("TerminalValuePartUntilCaret: '");
			builder.Append(TerminalValuePartUntilCaret);
			builder.Append("'; ");
			builder.Append("Schema: ");
			builder.Append(Schema);
			builder.Append("; ");
			builder.Append("SchemaDataObject: ");
			builder.Append(SchemaDataObject);
			builder.Append("; ");
			builder.Append("SchemaDataObjectReference: ");
			builder.Append(SchemaDataObjectReference);
			builder.Append("; ");
			builder.Append("Column: ");
			builder.Append(Column);
			builder.Append("; ");
			builder.Append("AllColumns: ");
			builder.Append(AllColumns);
			builder.Append("; ");
			builder.Append("JoinType: ");
			builder.Append(JoinType);
			builder.Append("; ");
			builder.Append("JoinCondition: ");
			builder.Append(JoinCondition);
			builder.Append("; ");
			builder.Append("Program: ");
			builder.Append(Program);

			Trace.WriteLine(builder.ToString());
		}
	}
}
