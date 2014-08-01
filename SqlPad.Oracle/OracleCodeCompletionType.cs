using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	internal class OracleCodeCompletionType
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();
		private static readonly char[] Separators = { ' ', '\t', '\r', '\n' };

		public bool Schema { get; private set; }

		public bool SchemaDataObject { get; private set; }
		
		public bool Sequence { get; private set; }
		
		public bool PipelinedFunction { get; private set; }
		
		public bool SchemaDataObjectReference { get; private set; }
		
		public bool Column { get; private set; }

		public bool AllColumns { get; private set; }
		
		public bool JoinType { get; private set; }
		
		public bool JoinCondition { get; private set; }
		
		public bool Program { get; private set; }
		
		public bool DatabaseLink { get; private set; }
		
		public bool InUnparsedData { get; private set; }
		
		public StatementGrammarNode CurrentTerminal { get; private set; }

		public OracleStatement Statement { get; private set; }

		public ICollection<string> TerminalCandidates { get; private set; }

		private bool Any
		{
			get { return Schema || SchemaDataObject || PipelinedFunction || SchemaDataObjectReference || Column || AllColumns || JoinType || JoinCondition || Program || DatabaseLink || Sequence; }
		}

		public bool ExistsTerminalValue { get { return !String.IsNullOrEmpty(TerminalValuePartUntilCaret); } }
		
		public string TerminalValuePartUntilCaret { get; private set; }
		
		public string TerminalValueUnderCursor { get; private set; }

		public ReferenceIdentifier ReferenceIdentifier { get; private set; }

		public OracleCodeCompletionType(StatementCollection statementCollection, string statementText, int cursorPosition)
		{
			Statement = (OracleStatement)(statementCollection.GetStatementAtPosition(cursorPosition) ?? statementCollection.LastOrDefault());
			if (Statement == null)
				return;

			if (Statement.TerminatorNode != null && Statement.TerminatorNode.SourcePosition.IndexStart < cursorPosition)
				return;

			var nearestTerminal = Statement.GetNearestTerminalToPosition(cursorPosition);
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
					CurrentTerminal = new StatementGrammarNode(NodeType.Terminal, Statement, new OracleToken(TerminalValueUnderCursor, cursorPosition - TerminalValuePartUntilCaret.Length));
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

			TerminalCandidates = new HashSet<string>(_parser.GetTerminalCandidates(isCursorAfterToken ? nearestTerminal : precedingTerminal));
			Schema = TerminalCandidates.Contains(Terminals.SchemaIdentifier);
			Program = Column = TerminalCandidates.Contains(Terminals.Identifier);
			DatabaseLink = TerminalCandidates.Contains(Terminals.DatabaseLinkIdentifier);
			JoinType = !isCursorTouchingTwoTerminals && TerminalCandidates.Contains(Terminals.Join);

			var isWithinFromClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.FromClause) != null || (isCursorAfterToken && nearestTerminal.Id == Terminals.From);
			var isWithinJoinCondition = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.JoinClause, NonTerminals.JoinColumnsOrCondition) != null;
			SchemaDataObject = isWithinFromClause && !isWithinJoinCondition && TerminalCandidates.Contains(Terminals.ObjectIdentifier);

			var isWithinJoinClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause) != null;
			JoinCondition = isWithinJoinClause && isCursorAfterToken && (TerminalCandidates.Contains(Terminals.On) || nearestTerminal.Id == Terminals.On);

			var isWithinSelectList = (nearestTerminal.Id == Terminals.Select && isCursorAfterToken) || nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList) != null;
			AllColumns = isWithinSelectList && TerminalCandidates.Contains(Terminals.Asterisk);

			SchemaDataObjectReference = !isWithinFromClause && TerminalCandidates.Contains(Terminals.ObjectIdentifier);

			if (!isCursorAfterToken)
			{
				AnalyzeObjectReferencePrefixes();
			}
		}

		private void AnalyzeObjectReferencePrefixes()
		{
			AnalyzePrefixedColumnReference();

			AnalyzeQueryTableExpression();
		}

		private void AnalyzePrefixedColumnReference()
		{
			var prefixedColumnReference = CurrentTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
			if (prefixedColumnReference == null)
				return;

			var identifiers = prefixedColumnReference.GetPathFilterDescendants(n => n.Id != NonTerminals.Expression, Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier).ToArray();
			ReferenceIdentifier = BuildReferenceIdentifier(identifiers);
		}

		private ReferenceIdentifier BuildReferenceIdentifier(ICollection<StatementGrammarNode> identifiers)
		{
			return
				new ReferenceIdentifier
				{
					SchemaIdentifier = GetIdentifierTokenValue(identifiers, Terminals.SchemaIdentifier),
					ObjectIdentifier = GetIdentifierTokenValue(identifiers, Terminals.ObjectIdentifier),
					Identifier = GetIdentifierTokenValue(identifiers, Terminals.Identifier)
				};
		}

		private StatementGrammarNode GetIdentifierTokenValue(IEnumerable<StatementGrammarNode> identifiers, string identifierId)
		{
			return identifiers.FirstOrDefault(i => i.Id == identifierId);
		}

		private void AnalyzeQueryTableExpression()
		{
			var queryTableExpression = CurrentTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.InnerTableReference, NonTerminals.QueryTableExpression);
			if (queryTableExpression == null)
				return;

			var identifiers = queryTableExpression.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.Expression, NonTerminals.NestedQuery), Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier).ToArray();
			ReferenceIdentifier = BuildReferenceIdentifier(identifiers);
		}

		public void PrintSupportedCompletions()
		{
			Trace.WriteLine(ReferenceIdentifier.ToString());

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
			builder.Append("; ");
			builder.Append("DatabaseLink: ");
			builder.Append(DatabaseLink);
			builder.Append("; ");
			builder.Append("Sequence: ");
			builder.Append(Sequence);

			Trace.WriteLine(builder.ToString());
		}
	}

	public struct ReferenceIdentifier
	{
		public StatementGrammarNode SchemaIdentifier { get; set; }
		public StatementGrammarNode ObjectIdentifier { get; set; }
		public StatementGrammarNode Identifier { get; set; }

		public string SchemaIdentifierValue { get { return SchemaIdentifier == null ? null : SchemaIdentifier.Token.Value; } }
		public string ObjectIdentifierValue { get { return ObjectIdentifier == null ? null : ObjectIdentifier.Token.Value; } }
		public string IdentifierValue { get { return Identifier == null ? null : Identifier.Token.Value; } }

		public override string ToString()
		{
			var builder = new StringBuilder(98);
			if (SchemaIdentifier != null)
			{
				builder.Append(SchemaIdentifierValue);
				builder.Append(".");
			}

			if (ObjectIdentifier != null)
			{
				builder.Append(ObjectIdentifierValue);
				builder.Append(".");
			}

			if (Identifier != null)
			{
				builder.Append(IdentifierValue);
			}

			if (builder.Length == 0)
			{
				builder.Append("No reference identifier has been identified. ");
			}

			return builder.ToString();
		}
	}
}
