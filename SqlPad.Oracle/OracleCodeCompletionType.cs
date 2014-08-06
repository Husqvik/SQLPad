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
		private readonly int _cursorPosition;

		public bool Schema { get; private set; }

		public bool SchemaDataObject { get; private set; }
		
		public bool Sequence { get; private set; }
		
		public bool PipelinedFunction { get; private set; }
		
		public bool SchemaDataObjectReference { get; private set; }
		
		public bool Column { get; private set; }

		public bool AllColumns { get; private set; }
		
		public bool JoinType { get; private set; }
		
		public bool JoinCondition { get; private set; }
		
		public bool SchemaProgram { get; private set; }

		public bool PackageFunction { get; private set; }

		public bool DatabaseLink { get; private set; }
		
		public bool InUnparsedData { get; private set; }

		public bool IsCursorTouchingTwoTerminals { get; private set; }
		
		public StatementGrammarNode CurrentTerminal { get; private set; }

		public OracleStatement Statement { get; private set; }
		
		public OracleStatementSemanticModel SemanticModel { get; private set; }

		public ICollection<string> TerminalCandidates { get; private set; }

		private bool Any
		{
			get { return Schema || SchemaDataObject || PipelinedFunction || SchemaDataObjectReference || Column || AllColumns || JoinType || JoinCondition || SchemaProgram || DatabaseLink || Sequence || PackageFunction; }
		}

		public bool ExistsTerminalValue { get { return !String.IsNullOrEmpty(TerminalValuePartUntilCaret); } }
		
		public string TerminalValuePartUntilCaret { get; private set; }
		
		public string TerminalValueUnderCursor { get; private set; }

		public ReferenceIdentifier ReferenceIdentifier { get; private set; }

		public OracleCodeCompletionType(SqlDocumentRepository documentRepository, string statementText, int cursorPosition)
		{
			_cursorPosition = cursorPosition;

			Statement = (OracleStatement)(documentRepository.Statements.GetStatementAtPosition(cursorPosition) ?? documentRepository.Statements.LastOrDefault());
			if (Statement == null)
				return;

			if (Statement.TerminatorNode != null && Statement.TerminatorNode.SourcePosition.IndexStart < cursorPosition)
				return;

			var nearestTerminal = Statement.GetNearestTerminalToPosition(cursorPosition);
			if (nearestTerminal == null)
				return;

			SemanticModel = (OracleStatementSemanticModel)documentRepository.ValidationModels[Statement].SemanticModel;

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

			var effectiveTerminal = Statement.GetNearestTerminalToPosition(cursorPosition, n => !n.Id.In(Terminals.RightParenthesis, Terminals.Comma));
			AnalyzeObjectReferencePrefixes(effectiveTerminal);

			var precedingTerminal = nearestTerminal.PrecedingTerminal;
			TerminalCandidates = new HashSet<string>(_parser.GetTerminalCandidates(isCursorAfterToken ? nearestTerminal : precedingTerminal));
			
			IsCursorTouchingTwoTerminals = nearestTerminal.SourcePosition.IndexStart == cursorPosition && precedingTerminal != null && precedingTerminal.SourcePosition.IndexEnd + 1 == cursorPosition;

			var isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix = IsCursorTouchingTwoTerminals && precedingTerminal.Id == Terminals.Identifier && !ReferenceIdentifier.HasObjectIdentifier;
			Schema = TerminalCandidates.Contains(Terminals.SchemaIdentifier) || isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix;
			SchemaProgram = Column = TerminalCandidates.Contains(Terminals.Identifier) || isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix;
			DatabaseLink = TerminalCandidates.Contains(Terminals.DatabaseLinkIdentifier);
			JoinType = !IsCursorTouchingTwoTerminals && TerminalCandidates.Contains(Terminals.Join);

			var isWithinFromClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.FromClause) != null || (isCursorAfterToken && nearestTerminal.Id == Terminals.From);
			var isWithinJoinCondition = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.JoinClause, NonTerminals.JoinColumnsOrCondition) != null;
			SchemaDataObject = isWithinFromClause && !isWithinJoinCondition && TerminalCandidates.Contains(Terminals.ObjectIdentifier);

			var isWithinJoinClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause) != null;
			JoinCondition = isWithinJoinClause && isCursorAfterToken && (TerminalCandidates.Contains(Terminals.On) || nearestTerminal.Id == Terminals.On);

			var isWithinSelectList = (nearestTerminal.Id == Terminals.Select && isCursorAfterToken) || nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList) != null;
			AllColumns = isWithinSelectList && TerminalCandidates.Contains(Terminals.Asterisk);

			SchemaDataObjectReference = !isWithinFromClause && (TerminalCandidates.Contains(Terminals.ObjectIdentifier) || isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix);

			PackageFunction = !String.IsNullOrEmpty(ReferenceIdentifier.ObjectIdentifierOriginalValue) && TerminalCandidates.Contains(Terminals.Identifier);
		}

		private void AnalyzeObjectReferencePrefixes(StatementGrammarNode effectiveTerminal)
		{
			AnalyzePrefixedColumnReference(effectiveTerminal);

			AnalyzeQueryTableExpression(effectiveTerminal);
		}

		private void AnalyzePrefixedColumnReference(StatementGrammarNode effectiveTerminal)
		{
			var prefixedColumnReference = effectiveTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
			var prefix = effectiveTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.Prefix);
			var lookupNode = prefixedColumnReference ?? prefix;
			if (lookupNode == null)
				return;

			var identifiers = lookupNode.GetPathFilterDescendants(n => n.Id != NonTerminals.Expression, Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier).ToArray();
			ReferenceIdentifier = BuildReferenceIdentifier(identifiers);
		}

		private void AnalyzeQueryTableExpression(StatementGrammarNode effectiveTerminal)
		{
			var queryTableExpression = effectiveTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.InnerTableReference, NonTerminals.QueryTableExpression);
			if (queryTableExpression == null)
				return;

			var identifiers = queryTableExpression.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.Expression, NonTerminals.NestedQuery), Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier).ToArray();
			ReferenceIdentifier = BuildReferenceIdentifier(identifiers);
		}

		private ReferenceIdentifier BuildReferenceIdentifier(ICollection<StatementGrammarNode> identifiers)
		{
			return
				new ReferenceIdentifier
				{
					SchemaIdentifier = GetIdentifierTokenValue(identifiers, Terminals.SchemaIdentifier),
					ObjectIdentifier = GetIdentifierTokenValue(identifiers, Terminals.ObjectIdentifier),
					Identifier = GetIdentifierTokenValue(identifiers, Terminals.Identifier),
					CursorPosition = _cursorPosition
				};
		}

		private StatementGrammarNode GetIdentifierTokenValue(IEnumerable<StatementGrammarNode> identifiers, string identifierId)
		{
			return identifiers.FirstOrDefault(i => i.Id == identifierId);
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
			builder.Append("SchemaProgram: ");
			builder.Append(SchemaProgram);
			builder.Append("; ");
			builder.Append("PackageFunction: ");
			builder.Append(PackageFunction);
			builder.Append("; ");
			builder.Append("DatabaseLink: ");
			builder.Append(DatabaseLink);
			builder.Append("; ");
			builder.Append("Sequence: ");
			builder.Append(Sequence);

			Trace.WriteLine(builder.ToString());
			Trace.WriteLine(ReferenceIdentifier.ToString());
		}
	}

	public struct ReferenceIdentifier
	{
		public StatementGrammarNode SchemaIdentifier { get; set; }
		public StatementGrammarNode ObjectIdentifier { get; set; }
		public StatementGrammarNode Identifier { get; set; }
		public int CursorPosition { get; set; }

		public bool HasSchemaIdentifier { get { return SchemaIdentifier != null; } }
		public bool HasObjectIdentifier { get { return ObjectIdentifier != null; } }
		public bool HasIdentifier { get { return Identifier != null; } }

		public string SchemaIdentifierOriginalValue { get { return SchemaIdentifier == null ? null : SchemaIdentifier.Token.Value; } }
		public string ObjectIdentifierOriginalValue { get { return ObjectIdentifier == null ? null : ObjectIdentifier.Token.Value; } }
		public string IdentifierOriginalValue { get { return Identifier == null ? null : Identifier.Token.Value; } }

		public string SchemaIdentifierEffectiveValue { get { return GetTerminalEffectiveValue(SchemaIdentifier); } }
		public string ObjectIdentifierEffectiveValue { get { return GetTerminalEffectiveValue(ObjectIdentifier); } }
		public string IdentifierEffectiveValue { get { return GetTerminalEffectiveValue(Identifier); } }

		private string GetTerminalEffectiveValue(StatementNode terminal)
		{
			if (terminal == null || terminal.SourcePosition.IndexStart >= CursorPosition)
				return null;

			return terminal.SourcePosition.IndexEnd < CursorPosition
				? terminal.Token.Value
				: terminal.Token.Value.Substring(0, CursorPosition - terminal.SourcePosition.IndexStart);
		}

		public override string ToString()
		{
			var builder = new StringBuilder(98);
			if (SchemaIdentifier != null)
			{
				builder.Append(SchemaIdentifierOriginalValue);
				builder.Append(".");
			}

			if (ObjectIdentifier != null)
			{
				builder.Append(ObjectIdentifierOriginalValue);
				builder.Append(".");
			}

			if (Identifier != null)
			{
				builder.Append(IdentifierOriginalValue);
			}

			if (builder.Length == 0)
			{
				builder.Append("No reference identifier has been identified. ");
			}

			return builder.ToString();
		}
	}
}
