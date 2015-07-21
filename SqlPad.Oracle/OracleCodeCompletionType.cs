using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SqlPad.Oracle.SemanticModel;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	internal class OracleCodeCompletionType
	{
		private static readonly HashSet<string> AvailableKeywordsToSuggest =
			new HashSet<string>
			{
				Terminals.Where,
				Terminals.Group,
				Terminals.Having,
				Terminals.Order,
				Terminals.Union,
				Terminals.Intersect,
				Terminals.SetMinus,
				Terminals.Connect,
				Terminals.Year,
				Terminals.Month,
				Terminals.Day,
				Terminals.Hour,
				Terminals.Minute,
				Terminals.Second,
				Terminals.TimezoneHour,
				Terminals.TimezoneMinute,
				Terminals.TimezoneRegion,
				Terminals.TimezoneAbbreviation
			};

		private readonly List<SuggestedKeywordClause> _keywordsClauses = new List<SuggestedKeywordClause>();

		public int CursorPosition { get; }

		public bool Schema { get; }

		public bool SchemaDataObject { get; }
		
		public bool Sequence { get; }
		
		public bool PipelinedFunction { get; private set; }
		
		public bool SchemaDataObjectReference { get; }
		
		public bool Column { get; }

		public bool UpdateSetColumn { get; private set; }

		public bool AllColumns { get; }
		
		public bool JoinType { get; }
		
		public bool DataType { get; }
		
		public bool JoinCondition { get; }
		
		public bool SchemaProgram { get; }

		public bool PackageFunction { get; }

		public bool DatabaseLink { get; }
		
		public bool ColumnAlias { get; private set; }

		public bool ExplicitPartition { get; }
		
		public bool ExplicitSubPartition { get; }

		public bool SpecialFunctionParameter { get; }
		
		public bool InsertIntoColumns { get; private set; }
		
		public bool InUnparsedData { get; private set; }

		public bool InComment { get; }

		public bool InQueryBlockFromClause { get; }

		public bool InSelectList { get; }

		public bool IsCursorTouchingIdentifier { get; }

		public bool IsNewExpressionWithInvalidGrammar { get; private set; }
		
		public StatementGrammarNode CurrentTerminal { get; private set; }

		public StatementGrammarNode EffectiveTerminal { get; }

		public OracleStatement Statement { get; }
		
		public OracleStatementSemanticModel SemanticModel { get; }

		public OracleQueryBlock CurrentQueryBlock { get; }

		public ICollection<TerminalCandidate> TerminalCandidates { get; }

		public ICollection<SuggestedKeywordClause> KeywordsClauses => _keywordsClauses;

	    private bool Any => Schema || SchemaDataObject || PipelinedFunction || SchemaDataObjectReference || Column || AllColumns || JoinType || DataType || JoinCondition || SchemaProgram || DatabaseLink || Sequence || PackageFunction || SpecialFunctionParameter || ExplicitPartition || ExplicitSubPartition || _keywordsClauses.Count > 0;

	    public bool ExistsTerminalValue => !String.IsNullOrEmpty(TerminalValuePartUntilCaret);

	    public string TerminalValuePartUntilCaret { get; private set; }
		
		public string TerminalValueUnderCursor { get; private set; }

		public ReferenceIdentifier ReferenceIdentifier { get; private set; }

		public OracleCodeCompletionType(SqlDocumentRepository documentRepository, string statementText, int cursorPosition)
		{
			CursorPosition = cursorPosition;

			Statement = (OracleStatement)(documentRepository.Statements.GetStatementAtPosition(cursorPosition) ?? documentRepository.Statements.LastOrDefault());
			if (Statement == null)
				return;

			if (Statement.TerminatorNode != null && Statement.TerminatorNode.SourcePosition.IndexStart < cursorPosition)
				return;

			var nearestTerminal = Statement.GetNearestTerminalToPosition(cursorPosition);
			if (nearestTerminal == null)
				return;

			var precedingTerminal = nearestTerminal.PrecedingTerminal;

			InComment = Statement.Comments.Any(c => c.SourcePosition.ContainsIndex(cursorPosition));

			IValidationModel validationModel;
			if (!documentRepository.ValidationModels.TryGetValue(Statement, out validationModel))
			{
				return;
			}

			SemanticModel = (OracleStatementSemanticModel)validationModel.SemanticModel;

			var requiredOffsetAfterToken = nearestTerminal.Id.IsZeroOffsetTerminalId() ? 0 : 1;
			var isCursorAfterToken = nearestTerminal.SourcePosition.IndexEnd + requiredOffsetAfterToken < cursorPosition;
			var atAdHocTemporaryTerminal = false;
			if (isCursorAfterToken)
			{
				var unparsedTextBetweenTokenAndCursor = statementText.Substring(nearestTerminal.SourcePosition.IndexEnd + 1, cursorPosition - nearestTerminal.SourcePosition.IndexEnd - 1);
				var unparsedEndTrimmedTextBetweenTokenAndCursor = unparsedTextBetweenTokenAndCursor.TrimEnd();

				OracleToken[] extraUnparsedTokens;
				using (var tokenReader = OracleTokenReader.Create(unparsedEndTrimmedTextBetweenTokenAndCursor))
				{
					extraUnparsedTokens = tokenReader.GetTokens(true).ToArray();
				}
				
				if (extraUnparsedTokens.Length > 0)
				{
					TerminalCandidates = OracleSqlParser.Instance.GetTerminalCandidates(nearestTerminal);
					if (TerminalCandidates.Count == 0 || extraUnparsedTokens.Length > 1 || unparsedEndTrimmedTextBetweenTokenAndCursor.Length < unparsedTextBetweenTokenAndCursor.Length)
					{
						InUnparsedData = true;
						return;
					}
				}

				TerminalValueUnderCursor = extraUnparsedTokens.FirstOrDefault().Value;

				if (TerminalValueUnderCursor != null)
				{
					TerminalValuePartUntilCaret = TerminalValueUnderCursor;
					precedingTerminal = nearestTerminal;
					nearestTerminal = CurrentTerminal = new StatementGrammarNode(NodeType.Terminal, Statement, new OracleToken(TerminalValueUnderCursor, cursorPosition - TerminalValuePartUntilCaret.Length));
					precedingTerminal.ParentNode.Clone().AddChildNodes(nearestTerminal);
					atAdHocTemporaryTerminal = true;

					nearestTerminal.Id = nearestTerminal.Token.Value[0] == '"'
						? Terminals.Identifier
						: GetIdentifierCandidate();

					if (nearestTerminal.Id != null)
					{
						ReferenceIdentifier = BuildReferenceIdentifier(nearestTerminal.ParentNode.GetDescendants(Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier).ToArray());
					}

					if (!String.IsNullOrEmpty(TerminalValueUnderCursor) && nearestTerminal.SourcePosition.ContainsIndex(cursorPosition))
					{
						isCursorAfterToken = false;
					}
				}
			}
			else
			{
				CurrentTerminal = nearestTerminal;
				ResolveCurrentTerminalValue(nearestTerminal);
			}

			var effectiveTerminal = Statement.GetNearestTerminalToPosition(cursorPosition, n => !n.Id.In(Terminals.RightParenthesis, Terminals.Comma, Terminals.Semicolon)) ?? nearestTerminal;
			CurrentQueryBlock = SemanticModel.GetQueryBlock(effectiveTerminal);
			
			AnalyzeObjectReferencePrefixes(effectiveTerminal);
			var isCursorAfterEffectiveTerminal = cursorPosition > effectiveTerminal.SourcePosition.IndexEnd + 1;

			if (precedingTerminal == null && nearestTerminal != Statement.RootNode.FirstTerminalNode)
			{
				precedingTerminal = nearestTerminal;
			}

			var isCursorTouchingTwoTerminals = nearestTerminal.SourcePosition.IndexStart == cursorPosition && precedingTerminal != null && precedingTerminal.SourcePosition.IndexEnd + 1 == cursorPosition;
			if (isCursorTouchingTwoTerminals && !String.Equals(nearestTerminal.Id, Terminals.Identifier))
			{
				IsCursorTouchingIdentifier = String.Equals(precedingTerminal.Id, Terminals.Identifier);
				EffectiveTerminal = precedingTerminal;
			}
			else
			{
				EffectiveTerminal = nearestTerminal;
			}

			var terminalCandidateSourceToken = isCursorAfterToken ? nearestTerminal : precedingTerminal;
			if (nearestTerminal.Id.In(Terminals.RightParenthesis, Terminals.Comma) && isCursorTouchingTwoTerminals && precedingTerminal.Id.IsIdentifier())
			{
				terminalCandidateSourceToken = precedingTerminal.PrecedingTerminal;
				ResolveCurrentTerminalValue(precedingTerminal);
			}

			if (TerminalCandidates == null)
			{
				TerminalCandidates = OracleSqlParser.Instance.GetTerminalCandidates(terminalCandidateSourceToken);
			}

			InSelectList = (atAdHocTemporaryTerminal ? precedingTerminal : EffectiveTerminal).GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.SelectList) != null;

			var invalidGrammarFilteredNearestTerminal = Statement.GetNearestTerminalToPosition(cursorPosition, n => !String.Equals(n.Id, Terminals.ObjectAlias));
			var isWithinExplicitPartitionClause = invalidGrammarFilteredNearestTerminal != null && String.Equals(invalidGrammarFilteredNearestTerminal.ParentNode.Id, NonTerminals.PartitionNameOrKeySet) && (invalidGrammarFilteredNearestTerminal != nearestTerminal || TerminalCandidates.Contains(Terminals.ObjectIdentifier));
			ExplicitPartition = isWithinExplicitPartitionClause && String.Equals(invalidGrammarFilteredNearestTerminal.ParentNode.ParentNode.FirstTerminalNode.Id, Terminals.Partition);
			ExplicitSubPartition = isWithinExplicitPartitionClause && String.Equals(invalidGrammarFilteredNearestTerminal.ParentNode.ParentNode.FirstTerminalNode.Id, Terminals.Subpartition);
			if (isWithinExplicitPartitionClause)
			{
				if (String.Equals(EffectiveTerminal.Id, Terminals.ObjectIdentifier))
				{
					ReferenceIdentifier = BuildReferenceIdentifier(new[] { EffectiveTerminal });
				}

				if (invalidGrammarFilteredNearestTerminal != nearestTerminal)
				{
					EffectiveTerminal = invalidGrammarFilteredNearestTerminal;
					TerminalValuePartUntilCaret = null;
				}
			}

			if (!isWithinExplicitPartitionClause)
			{
				ResolveSuggestedKeywords();
			}

			var isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix = IsCursorTouchingIdentifier && !ReferenceIdentifier.HasObjectIdentifier;
			Schema = TerminalCandidates.Contains(Terminals.SchemaIdentifier) || isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix;

			var isCurrentClauseSupported = EffectiveTerminal.IsWithinSelectClauseOrExpression() ||
			                               EffectiveTerminal.ParentNode.Id.In(NonTerminals.WhereClause, NonTerminals.GroupByClause, NonTerminals.HavingClause, NonTerminals.OrderByClause) ||
			                               EffectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.NestedQuery), NonTerminals.WhereClause) != null;
			if (isCurrentClauseSupported)
			{
				SchemaProgram = Column = TerminalCandidates.Contains(Terminals.Identifier) || isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix;

				var functionParameterOptionalExpression = EffectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.OptionalParameterExpressionList), NonTerminals.OptionalParameterExpression);
				if (functionParameterOptionalExpression != null)
				{
					var functionParameterExpression = functionParameterOptionalExpression[NonTerminals.Expression];
					SpecialFunctionParameter = functionParameterExpression != null && functionParameterExpression.TerminalCount == 1 && (functionParameterExpression.FirstTerminalNode.Id.IsLiteral() || String.Equals(functionParameterExpression.FirstTerminalNode.Id, Terminals.Identifier));
				}
			}

			DatabaseLink = TerminalCandidates.Contains(Terminals.DatabaseLinkIdentifier);
			JoinType = !isCursorTouchingTwoTerminals && !isWithinExplicitPartitionClause && TerminalCandidates.Contains(Terminals.Join);
			DataType = TerminalCandidates.Contains(Terminals.DataTypeIdentifier);

			InQueryBlockFromClause = effectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.FromClause) != null || (effectiveTerminal.Id == Terminals.From && effectiveTerminal.ParentNode.Id == NonTerminals.QueryBlock);
			var isWithinJoinCondition = effectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.JoinClause) && !String.Equals(n.Id, NonTerminals.NestedQuery), NonTerminals.JoinColumnsOrCondition) != null;
			var isAfterUpdateOrDeleteTerminal = (nearestTerminal.Id.In(Terminals.Update, Terminals.Delete) || (String.Equals(nearestTerminal.Id, Terminals.From) && nearestTerminal.PrecedingTerminal != null && String.Equals(nearestTerminal.PrecedingTerminal.Id, Terminals.Delete))) && isCursorAfterToken;
			var isWithinQueryBlock = nearestTerminal.GetAncestor(NonTerminals.QueryBlock) != null;
			var isWithinMainObjectReference = nearestTerminal.GetAncestor(NonTerminals.TableReference) != null && !isWithinQueryBlock;
			var isInInsertIntoTableReference = nearestTerminal.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.DmlTableExpressionClause) != null ||
			                                   (nearestTerminal.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.InsertIntoClause) != null && nearestTerminal.Id == Terminals.Into && isCursorAfterToken);
			SchemaDataObject = (InQueryBlockFromClause || isAfterUpdateOrDeleteTerminal || isWithinMainObjectReference || isInInsertIntoTableReference) && !isWithinJoinCondition && !isWithinExplicitPartitionClause && TerminalCandidates.Contains(Terminals.ObjectIdentifier);

			var isWithinJoinClause = effectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.FromClause), NonTerminals.JoinClause) != null;
			JoinCondition = isWithinJoinClause && isCursorAfterEffectiveTerminal && (TerminalCandidates.Contains(Terminals.On) || String.Equals(nearestTerminal.Id, Terminals.On));

			var isWithinSelectList = (String.Equals(nearestTerminal.Id, Terminals.Select) && isCursorAfterToken) || nearestTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.SelectList) != null;
			AllColumns = isWithinSelectList && TerminalCandidates.Contains(Terminals.Asterisk);

			SchemaDataObjectReference = !InQueryBlockFromClause && (TerminalCandidates.Contains(Terminals.ObjectIdentifier) || isCursorBetweenTwoTerminalsWithPrecedingIdentifierWithoutPrefix);

			PackageFunction = !String.IsNullOrEmpty(ReferenceIdentifier.ObjectIdentifierOriginalValue) && TerminalCandidates.Contains(Terminals.Identifier);

			var inMainQueryBlockOrMainObjectReference = CurrentQueryBlock == SemanticModel.MainQueryBlock || (CurrentQueryBlock == null && SemanticModel.MainObjectReferenceContainer.MainObjectReference != null);
			Sequence = inMainQueryBlockOrMainObjectReference && (nearestTerminal.IsWithinSelectClause() || !nearestTerminal.IsWithinExpression() || nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.InsertValuesClause) != null);

			var isWithinUpdateSetNonTerminal = String.Equals(nearestTerminal.ParentNode.Id, NonTerminals.PrefixedUpdatedColumnReference) || nearestTerminal.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.SetColumnListEqualsNestedQuery) != null;
			var isAfterSetTerminal = String.Equals(nearestTerminal.Id, Terminals.Set) && isCursorAfterToken;
			UpdateSetColumn = TerminalCandidates.Contains(Terminals.Identifier) && (isWithinUpdateSetNonTerminal || isAfterSetTerminal);

			ColumnAlias = Column && nearestTerminal.IsWithinOrderByClause();
		}

		private string GetIdentifierCandidate()
		{
			return TerminalCandidates.Contains(Terminals.Identifier)
				? Terminals.Identifier
				: TerminalCandidates.Contains(Terminals.ObjectIdentifier)
					? Terminals.ObjectIdentifier
					: TerminalCandidates.Contains(Terminals.SchemaIdentifier)
						? Terminals.SchemaIdentifier
						: null;
		}

		private void ResolveSuggestedKeywords()
		{
			var aggregateFunctionCallNode = EffectiveTerminal.GetPathFilterAncestor(
				n => !String.Equals(n.Id, NonTerminals.QueryBlock),
				n => String.Equals(n.Id, NonTerminals.AggregateFunctionCall) ||
				     (String.Equals(n.Id, NonTerminals.ColumnReference) && n[NonTerminals.ParenthesisEnclosedAggregationFunctionParameters] != null));
			
			var supportDistinct = EffectiveTerminal.ParentNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), n => String.Equals(n.Id, NonTerminals.AggregateFunctionCall) || String.Equals(n.Id, NonTerminals.ColumnReference)) == null;
			if (aggregateFunctionCallNode != null)
			{
				var programReference = SemanticModel.GetProgramReference(aggregateFunctionCallNode.FirstTerminalNode);
				supportDistinct = programReference?.Metadata != null && programReference.Metadata.IsAggregate;
			}

			var keywordClauses = TerminalCandidates.Where(c => AvailableKeywordsToSuggest.Contains(c.Id) || (InSelectList && String.Equals(c.Id, Terminals.Partition)) || (supportDistinct && c.Id.In(Terminals.Distinct, Terminals.Unique)))
				.Select(CreateKeywordClause);
			_keywordsClauses.AddRange(keywordClauses);
		}

		private SuggestedKeywordClause CreateKeywordClause(TerminalCandidate candidate)
		{
			var keywordClause = new SuggestedKeywordClause { TerminalId = candidate.Id };
			switch (candidate.Id)
			{
				case Terminals.Group:
					return keywordClause.WithText("GROUP BY");
				case Terminals.Order:
					return keywordClause.WithText("ORDER BY");
				case Terminals.Partition:
					return keywordClause.WithText("PARTITION BY");
				case Terminals.Connect:
					return keywordClause.WithText("CONNECT BY");
				case Terminals.SetMinus:
					return keywordClause.WithText("MINUS");
				default:
					return keywordClause.WithText(Terminals.AllTerminals[candidate.Id]);
			}
		}

		private void ResolveCurrentTerminalValue(StatementGrammarNode terminal)
		{
			TerminalValueUnderCursor = terminal.Token.Value;
			TerminalValuePartUntilCaret = CursorPosition > terminal.SourcePosition.IndexEnd + 1
				? String.Empty
				: terminal.Token.Value.Substring(0, CursorPosition - terminal.SourcePosition.IndexStart).Trim('"');
		}

		private void AnalyzeObjectReferencePrefixes(StatementGrammarNode effectiveTerminal)
		{
			if (effectiveTerminal == null || ReferenceIdentifier.CursorPosition > 0)
			{
				return;
			}

			AnalyzePrefixedColumnReference(effectiveTerminal);

			AnalyzeQueryTableExpression(effectiveTerminal);

			AnalyzeDataType(effectiveTerminal);
		}

		private void AnalyzeDataType(StatementGrammarNode effectiveTerminal)
		{
			var dataTypeNode = effectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.Expression), NonTerminals.DataType);
			if (dataTypeNode == null)
			{
				return;
			}

			ReferenceIdentifier = BuildReferenceIdentifier(dataTypeNode.GetDescendants(Terminals.SchemaIdentifier, Terminals.DataTypeIdentifier).ToArray());
		}

		private void AnalyzePrefixedColumnReference(StatementGrammarNode effectiveTerminal)
		{
			IsNewExpressionWithInvalidGrammar = String.Equals(effectiveTerminal.Id, Terminals.SchemaIdentifier) && effectiveTerminal.FollowingTerminal != null && String.Equals(effectiveTerminal.FollowingTerminal.Id, Terminals.ObjectIdentifier);
			var isObjectAlias = String.Equals(effectiveTerminal.Id, Terminals.ObjectAlias);
			if (IsNewExpressionWithInvalidGrammar || isObjectAlias)
			{
				ReferenceIdentifier =
					new ReferenceIdentifier
					{
						Identifier = effectiveTerminal,
						CursorPosition = CursorPosition
					};

				ResolveCurrentTerminalValue(effectiveTerminal);
				
				return;
			}

			var prefixedColumnReference = effectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.Expression), NonTerminals.PrefixedColumnReference);
			var prefix = effectiveTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.Expression) && !String.Equals(n.Id, NonTerminals.AliasedExpressionOrAllTableColumns), NonTerminals.Prefix);
			var lookupNode = prefixedColumnReference ?? prefix;
			if (lookupNode == null && effectiveTerminal.Id.In(Terminals.Asterisk, Terminals.User, Terminals.SystemDate, Terminals.Level, Terminals.RowIdPseudoColumn, Terminals.Null))
			{
				lookupNode = effectiveTerminal.ParentNode;
			}

			if (lookupNode == null && effectiveTerminal.Id.IsIdentifier())
			{
				lookupNode = effectiveTerminal.ParentNode;
			}

			if (lookupNode == null)
			{
				return;
			}

			var identifiers = lookupNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.Expression) && !String.Equals(n.Id, NonTerminals.AliasedExpressionOrAllTableColumns), Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier, Terminals.Asterisk, Terminals.User, Terminals.SystemDate, Terminals.Level, Terminals.RowIdPseudoColumn, Terminals.Null).ToList();
			ReferenceIdentifier = BuildReferenceIdentifier(identifiers);
		}

		private ReferenceIdentifier BuildReferenceIdentifier(ICollection<StatementGrammarNode> identifiers)
		{
			return
				new ReferenceIdentifier
				{
					SchemaIdentifier = GetIdentifierTokenValue(identifiers, Terminals.SchemaIdentifier),
					ObjectIdentifier = GetIdentifierTokenValue(identifiers, Terminals.ObjectIdentifier, Terminals.DataTypeIdentifier),
					Identifier = GetIdentifierTokenValue(identifiers, Terminals.Identifier, Terminals.Asterisk, Terminals.User, Terminals.SystemDate, Terminals.Level, Terminals.RowIdPseudoColumn, Terminals.Null),
					CursorPosition = CursorPosition
				};
		}

		private void AnalyzeQueryTableExpression(StatementGrammarNode effectiveTerminal)
		{
			var queryTableExpression = effectiveTerminal.GetPathFilterAncestor(n => !n.Id.In(NonTerminals.InnerTableReference, NonTerminals.QueryBlock), NonTerminals.QueryTableExpression);
			if (queryTableExpression == null)
				return;

			var identifiers = queryTableExpression.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.Expression, NonTerminals.NestedQuery), Terminals.SchemaIdentifier, Terminals.ObjectIdentifier, Terminals.Identifier).ToArray();
			ReferenceIdentifier = BuildReferenceIdentifier(identifiers);
		}

		private StatementGrammarNode GetIdentifierTokenValue(IEnumerable<StatementGrammarNode> identifiers, params string[] identifierIds)
		{
			return identifiers.FirstOrDefault(i => i.Id.In(identifierIds));
		}

		public void PrintResults()
		{
			Trace.WriteLine(ReferenceIdentifier.ToString());

			if (!Any)
			{
				Trace.WriteLine("No completions available");
				return;
			}

			var builder = new StringBuilder(512);
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
			builder.Append("; ");
			builder.Append("Data type: ");
			builder.Append(DataType);
			builder.Append("; ");
			builder.Append("InsertIntoColumns: ");
			builder.Append(InsertIntoColumns);
			builder.Append("; ");
			builder.Append("In comment: ");
			builder.Append(InComment);

			Trace.WriteLine(builder.ToString());
			Trace.WriteLine(ReferenceIdentifier.ToString());
		}
	}

	public struct ReferenceIdentifier
	{
		public StatementGrammarNode SchemaIdentifier { get; set; }
		public StatementGrammarNode ObjectIdentifier { get; set; }
		public StatementGrammarNode Identifier { get; set; }

		public StatementGrammarNode IdentifierUnderCursor => GetTerminalIfUnderCursor(Identifier) ?? GetTerminalIfUnderCursor(ObjectIdentifier) ?? GetTerminalIfUnderCursor(SchemaIdentifier);

	    public int CursorPosition { get; set; }

		public bool HasSchemaIdentifier => SchemaIdentifier != null;
	    public bool HasObjectIdentifier => ObjectIdentifier != null;
	    public bool HasIdentifier => Identifier != null;

	    public string SchemaIdentifierOriginalValue => SchemaIdentifier?.Token.Value;
	    public string ObjectIdentifierOriginalValue => ObjectIdentifier?.Token.Value;
	    public string IdentifierOriginalValue => Identifier?.Token.Value;

	    public string SchemaIdentifierEffectiveValue => GetTerminalEffectiveValue(SchemaIdentifier);
	    public string ObjectIdentifierEffectiveValue => GetTerminalEffectiveValue(ObjectIdentifier);
	    public string IdentifierEffectiveValue => GetTerminalEffectiveValue(Identifier);

	    private string GetTerminalEffectiveValue(StatementNode terminal)
		{
			if (terminal == null || terminal.SourcePosition.IndexStart > CursorPosition)
				return null;

			return terminal.SourcePosition.IndexEnd < CursorPosition
				? terminal.Token.Value.Trim('"')
				: terminal.Token.Value.Substring(0, CursorPosition - terminal.SourcePosition.IndexStart).Trim('"');
		}

		private StatementGrammarNode GetTerminalIfUnderCursor(StatementGrammarNode terminal)
		{
			return terminal != null && terminal.SourcePosition.ContainsIndex(CursorPosition)
				? terminal
				: null;
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

	[DebuggerDisplay("SuggestedKeywordClause (TerminalId={TerminalId}; Text={Text})")]
	public struct SuggestedKeywordClause
	{
		public string TerminalId;
		public string Text { get; private set; }

		public SuggestedKeywordClause WithText(string text)
		{
			Text = text;
			return this;
		}
	}
}
