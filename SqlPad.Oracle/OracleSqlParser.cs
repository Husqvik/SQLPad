using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad.Oracle
{
	public class OracleSqlParser : ISqlParser
	{
		private const char SingleQuoteCharacter = '\'';
		private static readonly Assembly LocalAssembly = typeof(OracleSqlParser).Assembly;
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(SqlGrammar));
		private static readonly Dictionary<string, SqlGrammarRule> NonTerminalRules;
		private static readonly HashSet<string> TerminatorIds;
		private static readonly HashSet<string> TerminatorValues;
		private static readonly SqlGrammarRuleSequenceNonTerminal[] AvailableNonTerminals;
		private static readonly Regex IdentifierMatcher;
		public static readonly OracleSqlParser Instance = new OracleSqlParser();
		
		static OracleSqlParser()
		{
			SqlGrammar oracleGrammar;
			using (var grammarReader = XmlReader.Create(LocalAssembly.GetManifestResourceStream("SqlPad.Oracle.OracleSqlGrammar.xml")))
			{
				oracleGrammar = (SqlGrammar)XmlSerializer.Deserialize(grammarReader);
			}

			NonTerminalRules = new Dictionary<string, SqlGrammarRule>();
			foreach (var rule in oracleGrammar.Rules)
			{
				if (NonTerminalRules.ContainsKey(rule.StartingNonTerminal))
					throw new InvalidOperationException($"Rule with starting non-terminal '{rule.StartingNonTerminal}' has been already defined. ");

				NonTerminalRules.Add(rule.StartingNonTerminal, rule);
			}

			var terminals = new Dictionary<string, SqlGrammarTerminal>();
			foreach (var terminal in oracleGrammar.Terminals)
			{
				if (terminals.ContainsKey(terminal.Id))
					throw new InvalidOperationException($"Terminal '{terminal.Id}' has been already defined. ");

				terminal.Initialize();
				terminals.Add(terminal.Id, terminal);
			}

			IdentifierMatcher = terminals[OracleGrammarDescription.Terminals.Identifier].RegexMatcher;

			var missingTerminals = new HashSet<string>();
			var missingNonTerminals = new HashSet<string>();

			foreach (var sequence in oracleGrammar.Rules.SelectMany(r => r.Sequences))
			{
				for (var i = 0; i < sequence.Items.Length; i++)
				{
					var item = sequence.Items[i];
					if (item is SqlGrammarRuleSequenceNonTerminal nonTerminal)
					{
						if (NonTerminalRules.TryGetValue(nonTerminal.Id, out var rule))
						{
							nonTerminal.TargetRule = rule;
						}
						else
						{
							missingNonTerminals.Add(nonTerminal.Id);
						}

						nonTerminal.ParentSequence = sequence;
						nonTerminal.SequenceIndex = i;
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						if (terminals.TryGetValue(terminalReference.Id, out var terminal))
						{
							terminalReference.Terminal = terminal;
						}
						else
						{
							missingTerminals.Add(terminalReference.Id);
						}

						terminalReference.ParentSequence = sequence;
						terminalReference.SequenceIndex = i;
					}
				}
			}

			var exceptionMessageBuilder = new StringBuilder();
			if (missingNonTerminals.Count > 0)
			{
				exceptionMessageBuilder.Append("Missing rules for non-terminal IDs: ");
				exceptionMessageBuilder.AppendLine(String.Join(", ", missingNonTerminals));
			}

			if (missingTerminals.Count > 0)
			{
				exceptionMessageBuilder.Append("Missing specifications for terminal IDs: ");
				exceptionMessageBuilder.Append(String.Join(", ", missingTerminals));
			}

			if (exceptionMessageBuilder.Length > 0)
			{
				throw new InvalidOperationException($"Grammar description is invalid. {Environment.NewLine}{exceptionMessageBuilder}");
			}

			AvailableNonTerminals = oracleGrammar.StartSymbols.Select(s => CreateInitialNonTerminal(s.Id)).ToArray();

			TerminatorIds = oracleGrammar.Terminators.Select(t => t.Id).ToHashSet();
			TerminatorValues = TerminatorIds.Select(id => terminals[id].Value).ToHashSet();
		}

		private OracleSqlParser() { }

		public static bool IsValidIdentifier(string identifier, ReservedWordScope scope = ReservedWordScope.Sql)
		{
			return IdentifierMatcher.IsMatch(identifier) && !identifier.IsReservedWord(scope);
		}

		public bool IsLiteral(string terminalId)
		{
			return terminalId.IsLiteral();
		}

		public bool IsAlias(string terminalId)
		{
			return terminalId.IsAlias();
		}

		public bool CanAddPairCharacter(string tokenValue, char character)
		{
			switch (character)
			{
				case SingleQuoteCharacter:
					bool isQuotedString;
					char? quoteInitializer;
					var trimToIndex = OracleExtensions.GetTrimIndex(tokenValue, out isQuotedString, out quoteInitializer) - (isQuotedString ? 2 : 1);
					var tokenValueLength = tokenValue.Length;
					if (trimToIndex >= tokenValueLength)
					{
						trimToIndex = tokenValueLength - 1;
					}

					return tokenValue[0] != SingleQuoteCharacter && (tokenValue[trimToIndex] != SingleQuoteCharacter);
				
				case '"':
					return tokenValue[0] != '"';
				
				default:
					return true;
			}
		}

		public bool IsRuleValid(string nonTerminalId, string text)
		{
			return IsRuleValid(nonTerminalId, OracleTokenReader.Create(text).GetTokens());
		}

		private static bool IsRuleValid(string nonTerminalId, IEnumerable<OracleToken> tokens)
		{
			var context =
				new ParseContext
				{
					Statement = new OracleStatement(),
					TokenBuffer = new List<OracleToken>(tokens)
				};

			var nonTerminal = CreateInitialNonTerminal(nonTerminalId);
			var result = ProceedNonTerminal(context, nonTerminal, 0, 0, false, nonTerminal.TargetRule.Scope);
			return result.Status == ParseStatus.Success &&
			       result.Nodes.Sum(n => n.TerminalCount) == context.TokenBuffer.Count &&
			       result.Nodes.All(n => n.AllChildNodes.All(c => c.IsGrammarValid));
		}

		public StatementCollection Parse(string sqlText)
		{
			using (var reader = new StringReader(sqlText))
			{
				return Parse(OracleTokenReader.Create(reader));
			}
		}

		public StatementCollection Parse(OracleTokenReader tokenReader)
		{
			EnsureReaderNotNull(tokenReader);

			return Parse(tokenReader.GetTokens(true));
		}

		public StatementCollection Parse(IEnumerable<OracleToken> tokens)
		{
			return ProceedGrammar(tokens, CancellationToken.None);
		}

		public async Task<StatementCollection> ParseAsync(string sqlText, CancellationToken cancellationToken)
		{
			using (var reader = new StringReader(sqlText))
			{
				return await ParseAsync(OracleTokenReader.Create(reader), cancellationToken);
			}
		}

		public Task<StatementCollection> ParseAsync(OracleTokenReader tokenReader, CancellationToken cancellationToken)
		{
			EnsureReaderNotNull(tokenReader);

			return ParseAsync(tokenReader.GetTokens(true), cancellationToken);
		}

		private static void EnsureReaderNotNull(OracleTokenReader tokenReader)
		{
			if (tokenReader == null)
			{
				throw new ArgumentNullException(nameof(tokenReader));
			}
		}

		public Task<StatementCollection> ParseAsync(IEnumerable<OracleToken> tokens, CancellationToken cancellationToken)
		{
			return Task.Run(() => ProceedGrammar(tokens, cancellationToken), cancellationToken);
		}

		public ICollection<TerminalCandidate> GetTerminalCandidates(StatementGrammarNode node)
		{
			var candidates = new HashSet<TerminalCandidate>();

			var nonTerminalIds = new List<SqlGrammarRuleSequenceNonTerminal>();
			if (node != null)
			{
				if (node.Type != NodeType.Terminal)
				{
					throw new ArgumentException("Node must be a terminal node. ", nameof(node));
				}

				MatchNode(node, candidates);
			}
			else
			{
				nonTerminalIds.AddRange(AvailableNonTerminals);
			}

			foreach (var nonTerminalId in nonTerminalIds)
			{
				GatherCandidatesFromNonterminal(nonTerminalId, candidates);
			}

			return candidates;
		}

		private void MatchNode(StatementGrammarNode node, ICollection<TerminalCandidate> candidates)
		{
			var parent = node.ParentNode;
			if (parent == null)
			{
				return;
			}

			var matchParent = false;
			var compatibleSequences = NonTerminalRules[parent.Id].Sequences.SelectMany(s => GetCompatibleSequences(s, parent));

			if (parent.ParentNode != null)
			{
				compatibleSequences = compatibleSequences.Union(NonTerminalRules[parent.ParentNode.Id].Sequences.SelectMany(s => GetCompatibleSequences(s, parent)));
			}

			var childNodeIndex = parent.IndexOf(node);

			foreach (var sequence in compatibleSequences)
			{
				var index = 0;
				var sequenceCompatible = true;
				var gatherCandidates = false;

				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					if (gatherCandidates)
					{
						GatherCandidatesFromGrammarItem(item, candidates);

						if (item.IsRequired)
						{
							sequenceCompatible = false;
							break;
						}

						continue;
					}

					if (index < parent.ChildNodes.Count && String.Equals(item.Id, parent.ChildNodes[index].Id))
					{
						if (index == childNodeIndex)
						{
							gatherCandidates = true;
						}

						index++;
					}
					else if (item.IsRequired)
					{
						sequenceCompatible = false;
						break;
					}
				}

				if (sequenceCompatible)
				{
					matchParent = true;
				}
			}

			if (matchParent)
			{
				MatchNode(parent, candidates);
			}
		}

		private static IEnumerable<SqlGrammarRuleSequence> GetCompatibleSequences(SqlGrammarRuleSequence sequence, StatementGrammarNode parentNode)
		{
			var inputItems = sequence.Items
				.Cast<ISqlGrammarRuleSequenceItem>()
				.TakeWhileInclusive(i => !i.IsRequired);

			var isInputSequence = inputItems.Any(i => String.Equals(i.Id, parentNode.ChildNodes[0].Id));

			return isInputSequence
				? Enumerable.Repeat(sequence, 1)
				: inputItems.Where(i => i.Type == NodeType.NonTerminal)
					.SelectMany(i => NonTerminalRules[i.Id].Sequences)
					.SelectMany(s => GetCompatibleSequences(s, parentNode));
		}

		private void GatherCandidatesFromNonterminal(SqlGrammarRuleSequenceNonTerminal nonTerminal, ICollection<TerminalCandidate> candidates)
		{
			foreach (var sequence in nonTerminal.TargetRule.Sequences)
			{
				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					GatherCandidatesFromGrammarItem(item, candidates);

					if (item.IsRequired)
					{
						break;
					}
				}
			}
		}

		private static bool GetFollowingMandatoryCandidates(ISqlGrammarRuleSequenceItem item, ICollection<string> followingMandatoryCandidates)
		{
			if (!item.IsRequired)
			{
				return false;
			}

			if (item.Type == NodeType.Terminal)
			{
				var terminal = ((SqlGrammarRuleSequenceTerminal)item).Terminal;
				if (terminal.IsFixed && terminal.Value.Length > 1)
				{
					followingMandatoryCandidates.Add(item.Id);
					return true;
				}

				return false;
			}

			var nonTerminal = (SqlGrammarRuleSequenceNonTerminal)item;
			return nonTerminal.TargetRule.Sequences.Length == 1 &&
				   nonTerminal.TargetRule.Sequences[0].Items
					   .All(childItem => GetFollowingMandatoryCandidates((ISqlGrammarRuleSequenceItem)childItem, followingMandatoryCandidates));
		}

		private void GatherCandidatesFromGrammarItem(ISqlGrammarRuleSequenceItem item, ICollection<TerminalCandidate> candidates)
		{
			if (item is SqlGrammarRuleSequenceNonTerminal nonTerminal)
			{
				GatherCandidatesFromNonterminal(nonTerminal, candidates);
			}
			else if (!TerminatorIds.Contains(item.Id))
			{
				List<string> followingMandatoryCandidates = null;
				var terminal = ((SqlGrammarRuleSequenceTerminal)item).Terminal;
				if (terminal.IsFixed)
				{
					followingMandatoryCandidates = new List<string>();

					for (var j = item.SequenceIndex + 1; j < item.ParentSequence.Items.Length; j++)
					{
						if (!GetFollowingMandatoryCandidates((ISqlGrammarRuleSequenceItem)item.ParentSequence.Items[j], followingMandatoryCandidates))
						{
							break;
						}
					}
				}

				candidates.Add(new TerminalCandidate(item.Id, followingMandatoryCandidates));
			}
		}

		private static StatementCollection ProceedGrammar(IEnumerable<OracleToken> tokens, CancellationToken cancellationToken)
		{
			var allTokens = new List<IToken>();
			var tokenBuffer = new List<OracleToken>();
			var commentBuffer = new List<OracleToken>();

			foreach (var token in tokens)
			{
				if (token.CommentType == CommentType.None)
				{
					tokenBuffer.Add(token);
				}
				else
				{
					commentBuffer.Add(token);
				}

				allTokens.Add(token);
			}

			var oracleSqlCollection = new List<StatementBase>();

			if (tokenBuffer.Count == 0)
			{
				return new OracleStatementCollection(oracleSqlCollection, allTokens, commentBuffer.Select(c => new StatementCommentNode(null, c)));
			}
			
			do
			{
				var result = new ParseResult();
				var context =
					new ParseContext
					{
						CancellationToken = cancellationToken,
						Statement = new OracleStatement(),
						TokenBuffer = tokenBuffer
					};

				foreach (var nonTerminal in AvailableNonTerminals)
				{
					var newResult = ProceedNonTerminal(context, nonTerminal, 1, 0, false, nonTerminal.TargetRule.Scope);

					//if (newResult.Nodes.SelectMany(n => n.AllChildNodes).Any(n => n.Terminals.Count() != n.TerminalCount))
					//	throw new ApplicationException("StatementGrammarNode TerminalCount value is invalid. ");

					if (newResult.Status != ParseStatus.Success)
					{
						if (result.BestCandidates == null || newResult.BestCandidates.Sum(n => n.TerminalCount) > result.BestCandidates.Sum(n => n.TerminalCount))
						{
							result = newResult;
						}

						continue;
					}

					result = newResult;

					var lastTerminal = result.Nodes[result.Nodes.Count - 1].LastTerminalNode;
					if (lastTerminal == null ||
					    !TerminatorIds.Contains(lastTerminal.Id) && tokenBuffer.Count > result.Nodes.Sum(n => n.TerminalCount))
					{
						if (lastTerminal != null)
						{
							var lastToken = result.BestCandidates.Last().LastTerminalNode.Token;
							var parsedTerminalCount = result.BestCandidates.Sum(n => n.TerminalCount);
							context.Statement.FirstUnparsedToken = tokenBuffer.Count > parsedTerminalCount ? tokenBuffer[parsedTerminalCount] : lastToken;
						}

						result.Status = ParseStatus.SequenceNotFound;
					}

					break;
				}

				int indexStart;
				int indexEnd;
				if (result.Status != ParseStatus.Success)
				{
					if (result.BestCandidates.Sum(n => n.TerminalCount) > result.Nodes.Sum(n => n.TerminalCount))
					{
						result.Nodes = result.BestCandidates;
					}

					indexStart = tokenBuffer.First().Index;

					var index = tokenBuffer.FindIndex(t => TerminatorValues.Contains(t.Value));
					if (index == -1)
					{
						var lastToken = tokenBuffer[tokenBuffer.Count - 1];
						indexEnd = lastToken.Index + lastToken.Value.Length - 1;
						tokenBuffer.Clear();
					}
					else
					{
						indexEnd = tokenBuffer[index].Index;
						tokenBuffer.RemoveRange(0, index + 1);
					}
				}
				else
				{
					var lastTerminal = result.Nodes[result.Nodes.Count - 1].LastTerminalNode.Token;
					indexStart = result.Nodes[0].FirstTerminalNode.Token.Index;
					indexEnd = lastTerminal.Index + lastTerminal.Value.Length - 1;

					tokenBuffer.RemoveRange(0, result.Nodes.Sum(n => n.TerminalCount));

					var hasInvalidGrammarNodes = result.Nodes.Any(HasInvalidGrammarNodes);
					if (hasInvalidGrammarNodes)
					{
						result.Status = ParseStatus.SequenceNotFound;
					}
				}

				var lastNode = result.Nodes.LastOrDefault();
				if (lastNode?.FirstTerminalNode != null && TerminatorIds.Contains(lastNode.FirstTerminalNode.Id))
				{
					context.Statement.TerminatorNode = lastNode.FirstTerminalNode;
					result.Nodes.Remove(lastNode);
				}

				context.Statement.SourcePosition = SourcePosition.Create(indexStart, indexEnd);
				var rootNode =
					new StatementGrammarNode(NodeType.NonTerminal, context.Statement, null)
					{
						Id = result.NodeId,
						IsGrammarValid = result.Nodes.All(n => n.IsGrammarValid),
						IsRequired = true,
					};
				
				rootNode.AddChildNodes(result.Nodes);

				context.Statement.RootNode = rootNode;
				context.Statement.ParseStatus = result.Status;

				oracleSqlCollection.Add(context.Statement);
			}
			while (tokenBuffer.Count > 0);

			var commentNodes = AddCommentNodes(oracleSqlCollection, commentBuffer);

			return new OracleStatementCollection(oracleSqlCollection, allTokens, commentNodes);
		}

		private static ParseResult ProceedNonTerminal(ParseContext context, SqlGrammarRuleSequenceNonTerminal nonTerminal, int level, int tokenStartOffset, bool tokenReverted, ReservedWordScope scope)
		{
			if (nonTerminal.TargetRule.Scope != ReservedWordScope.Inherit)
			{
				scope = nonTerminal.TargetRule.Scope;
			}

			var bestCandidateNodes = new List<StatementGrammarNode>();
			var workingNodes = new List<StatementGrammarNode>();
			var nonTerminalId = nonTerminal.Id;
			var result =
				new ParseResult
				{
					NodeId = nonTerminalId,
					Nodes = workingNodes,
					BestCandidates = bestCandidateNodes,
				};

			var workingTerminalCount = 0;
			var bestCandidateTerminalCount = 0;
			var totalTokenCount = context.TokenBuffer.Count;

			var isPlSqlStatement = String.Equals(nonTerminalId, OracleGrammarDescription.NonTerminals.PlSqlStatementType);
			if (isPlSqlStatement)
			{
				context.PlSqlStatementTokenIndex.Push(tokenStartOffset);
			}

			foreach (var sequence in nonTerminal.TargetRule.Sequences)
			{
				context.CancellationToken.ThrowIfCancellationRequested();

				result.Status = ParseStatus.Success;
				workingNodes.Clear();
				workingTerminalCount = 0;

				var bestCandidatesCompatible = false;
				var isSequenceValid = true;

				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					var tokenOffset = tokenStartOffset + workingTerminalCount;
					var isNodeRequired = item.IsRequired;
					if (tokenOffset >= totalTokenCount && !isNodeRequired)
					{
						continue;
					}

					var childNodeId = item.Id;
					if (!isNodeRequired && workingTerminalCount == 0 && String.Equals(childNodeId, nonTerminalId))
					{
						continue;
					}

					var bestCandidateOffset = tokenStartOffset + bestCandidateTerminalCount;
					var tryBestCandidates = bestCandidatesCompatible && !tokenReverted && bestCandidateTerminalCount > workingTerminalCount;
					if (item is SqlGrammarRuleSequenceNonTerminal childNonTerminal)
					{
						var nestedResult = ProceedNonTerminal(context, childNonTerminal, level + 1, tokenOffset, false, scope);

						var optionalTokenReverted = TryRevertOptionalToken(optionalTerminalCount => ProceedNonTerminal(context, childNonTerminal, level + 1, tokenOffset - optionalTerminalCount, true, scope), ref nestedResult, workingNodes);
						workingTerminalCount -= optionalTokenReverted;

						TryParseInvalidGrammar(tryBestCandidates, () => ProceedNonTerminal(context, childNonTerminal, level + 1, bestCandidateOffset, false, scope), ref nestedResult, workingNodes, bestCandidateNodes, ref workingTerminalCount);

						var isNestedNodeValid = nestedResult.Status == ParseStatus.Success;
						if (isNodeRequired || isNestedNodeValid)
						{
							result.Status = nestedResult.Status;
						}

						var nestedNode =
							new StatementGrammarNode(NodeType.NonTerminal, context.Statement, null)
							{
								Id = childNodeId,
								Level = level,
								IsRequired = isNodeRequired,
								IsGrammarValid = isNestedNodeValid
							};

						var alternativeNode = nestedNode.Clone();

						int currentTerminalCount;
						if (nestedResult.BestCandidates.Count > 0 &&
							((currentTerminalCount = workingTerminalCount + nestedResult.BestCandidateTerminalCount) > bestCandidateTerminalCount ||
							 (currentTerminalCount == bestCandidateTerminalCount && isNestedNodeValid)))
						{
							alternativeNode.AddChildNodes(ResolveAlternativeNodes(nestedResult));

							if (optionalTokenReverted > 0 || !isNestedNodeValid || workingNodes.Count != bestCandidateNodes.Count)
							{
								bestCandidateTerminalCount = CreateNewNodeList(workingNodes, bestCandidateNodes);
							}

							bestCandidateNodes.Add(alternativeNode);
							bestCandidateTerminalCount += alternativeNode.TerminalCount;

							bestCandidatesCompatible = true;
						}

						if (isNestedNodeValid && nestedResult.Nodes.Count > 0)
						{
							nestedNode.AddChildNodes(nestedResult.Nodes);
							workingNodes.Add(nestedNode);
							workingTerminalCount += nestedResult.TerminalCount;
						}

						if (result.Status == ParseStatus.SequenceNotFound)
						{
							if (workingNodes.Count == 0)
							{
								break;
							}

							isSequenceValid = false;
							workingNodes.Add(alternativeNode.Clone());
							workingTerminalCount += alternativeNode.TerminalCount;
						}
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						var terminalResult = IsTokenValid(context, terminalReference, level, tokenOffset, scope);

						TryParseInvalidGrammar(tryBestCandidates && isNodeRequired, () => IsTokenValid(context, terminalReference, level, bestCandidateOffset, scope), ref terminalResult, workingNodes, bestCandidateNodes, ref workingTerminalCount);

						if (terminalResult.Status == ParseStatus.SequenceNotFound)
						{
							if (isNodeRequired)
							{
								result.Status = ParseStatus.SequenceNotFound;
								break;
							}

							continue;
						}

						workingTerminalCount++;
						bestCandidateTerminalCount++;
						var terminalNode = terminalResult.Nodes[0];

						workingNodes.Add(terminalNode);
						bestCandidateNodes.Add(terminalNode.Clone());
					}
				}

				if (result.Status == ParseStatus.Success)
				{
					#region CASE WHEN issue
					if (bestCandidateTerminalCount > workingTerminalCount)
					{
						var currentTerminalCount = bestCandidateNodes.SelectMany(n => n.Terminals).TakeWhile(t => !t.Id.IsIdentifierOrAlias() && !t.Id.IsLiteral()).Count();
						if (currentTerminalCount > workingTerminalCount && workingNodes.FirstOrDefault()?.FirstTerminalNode.Id.IsIdentifierOrAlias() == true)
						{
							workingNodes.ForEach(n => n.IsGrammarValid = false);
						}
					}
					#endregion

					if (isSequenceValid)
					{
						break;
					}
				}
			}

			if (isPlSqlStatement)
			{
				context.PlSqlStatementTokenIndex.Pop();
			}

			result.BestCandidates = bestCandidateNodes;
			result.TerminalCount = workingTerminalCount;
			result.BestCandidateTerminalCount = bestCandidateTerminalCount;

			return result;
		}

		/// <summary>
		/// Candidate nodes can be multiplied or terminals can be spread among different nonterminals,
		/// therefore we fetch the node with most terminals or the later (when nodes contain same terminals).
		/// </summary>
		private static IEnumerable<StatementGrammarNode> ResolveAlternativeNodes(ParseResult nestedResult)
		{
			var usedCandidates = new List<StatementGrammarNode>();
			foreach (var candidate in nestedResult.BestCandidates)
			{
				var addNewCandidate = true;
				for (var i = 0; i < usedCandidates.Count; i++)
				{
					var usedCandidate = usedCandidates[i];
					if (usedCandidate.SourcePosition.IndexStart == candidate.SourcePosition.IndexStart || usedCandidate.SourcePosition.IndexEnd == candidate.SourcePosition.IndexEnd)
					{
						addNewCandidate = false;
						if (usedCandidate.SourcePosition.Length <= candidate.SourcePosition.Length)
						{
							usedCandidates[i] = candidate;
							break;
						}
					}
				}

				if (addNewCandidate)
				{
					usedCandidates.Add(candidate);
				}
			}

			return usedCandidates;
		}

		private static int CreateNewNodeList(IEnumerable<StatementGrammarNode> nodeSource, ICollection<StatementGrammarNode> targetList)
		{
			targetList.Clear();
			var terminalCount = 0;

			foreach (var node in nodeSource)
			{
				terminalCount += node.TerminalCount;
				targetList.Add(node.Clone());
			}

			return terminalCount;
		}

		private static void TryParseInvalidGrammar(bool preconditionsValid, Func<ParseResult> getForceParseResultFunction, ref ParseResult parseResult, IList<StatementGrammarNode> workingNodes, IEnumerable<StatementGrammarNode> bestCandidateNodes, ref int workingTerminalCount)
		{
			if (!preconditionsValid || parseResult.Status == ParseStatus.Success)
			{
				return;
			}

			var bestCandidateResult = getForceParseResultFunction();
			if (bestCandidateResult.Status == ParseStatus.SequenceNotFound)
			{
				return;
			}

			workingTerminalCount = CreateNewNodeList(bestCandidateNodes, workingNodes);

			var lastWorkingNode = workingNodes[workingNodes.Count - 1];
			if (lastWorkingNode.AllChildNodes.All(n => n.IsGrammarValid))
			{
				lastWorkingNode.IsGrammarValid = false;
			}

			parseResult = bestCandidateResult;
		}

		private static StatementGrammarNode TryGetOptionalAncestor(StatementGrammarNode node)
		{
			while (node != null && node.IsRequired)
			{
				node = node.ParentNode;
			}

			return node;
		}

		private static int TryRevertOptionalToken(Func<int, ParseResult> getAlternativeParseResultFunction, ref ParseResult currentResult, IList<StatementGrammarNode> workingNodes)
		{
			var optionalNodeCandidate = workingNodes.Count > 0 ? workingNodes[workingNodes.Count - 1].LastTerminalNode : null;
			if (optionalNodeCandidate != null && optionalNodeCandidate.IsRequired)
			{
				optionalNodeCandidate = currentResult.Status == ParseStatus.SequenceNotFound
					? TryGetOptionalAncestor(optionalNodeCandidate)
					: optionalNodeCandidate.ParentNode;
			}

			if (optionalNodeCandidate == null || optionalNodeCandidate.IsRequired)
			{
				return 0;
			}

			if (optionalNodeCandidate.ParentNode != null && optionalNodeCandidate.ParentNode.ChildNodes.All(n => !n.IsRequired))
			{
				return 0;
			}

			var optionalTerminalCount = optionalNodeCandidate.TerminalCount;
			var newResult = getAlternativeParseResultFunction(optionalTerminalCount);

			var effectiveTerminalCount = newResult.Status == ParseStatus.SequenceNotFound
				? newResult.BestCandidateTerminalCount
				: newResult.TerminalCount;
			
			var allowTerminalCountReversion = optionalTerminalCount == 1
				? effectiveTerminalCount > optionalTerminalCount
				: effectiveTerminalCount >= optionalTerminalCount;
			
			var revertNode = allowTerminalCountReversion &&
							 effectiveTerminalCount > currentResult.TerminalCount;

			if (!revertNode)
			{
				return 0;
			}
			
			currentResult = newResult;
			
			return RevertLastOptionalNode(workingNodes, optionalNodeCandidate);
		}

		private static int RevertLastOptionalNode(IList<StatementGrammarNode> workingNodes, StatementGrammarNode optionalNodeCandidate)
		{
			var indexToRemove = workingNodes.Count - 1;
			var nodeToRemove = workingNodes[indexToRemove];
			if (nodeToRemove.Type == NodeType.NonTerminal && nodeToRemove != optionalNodeCandidate)
			{
				return nodeToRemove.RemoveLastChildNodeIfOptional();
			}
			
			workingNodes.RemoveAt(indexToRemove);
			return nodeToRemove.TerminalCount;
		}

		private static ParseResult IsTokenValid(ParseContext context, SqlGrammarRuleSequenceTerminal terminalReference, int level, int tokenOffset, ReservedWordScope scope)
		{
			var tokenIsValid = false;
			IList<StatementGrammarNode> nodes = null;

			var terminalId = terminalReference.Id;
			if (context.TokenBuffer.Count > tokenOffset)
			{
				var currentToken = context.TokenBuffer[tokenOffset];

				var terminal = terminalReference.Terminal;
				var isReservedWord = false;
				if (String.IsNullOrEmpty(terminal.RegexValue))
				{
					var tokenValue = currentToken.UpperInvariantValue;
					tokenIsValid = String.Equals(terminal.Value, tokenValue) || (terminal.AllowQuotedNotation && tokenValue.Length == terminal.Value.Length + 2 && tokenValue[0] == '"' && tokenValue[tokenValue.Length - 1] == '"' && String.Equals(tokenValue.Substring(1, tokenValue.Length - 2), terminal.Value));
					isReservedWord = tokenIsValid && (scope == ReservedWordScope.Sql ? terminal.ReservedWord == ReservedWordType.Sql : terminal.ReservedWord > 0);
				}
				else
				{
					tokenIsValid = terminal.RegexMatcher.IsMatch(currentToken.Value);
					if (tokenIsValid && !terminalReference.AllowReservedWord)
					{
						var isNotReservedWord = !OracleGrammarDescription.ReservedWordsSql.Contains(currentToken.UpperInvariantValue);

						if (isNotReservedWord && scope == ReservedWordScope.PlSqlBody)
						{
							var effectiveReservedWords = context.PlSqlStatementTokenIndex.Count == 0 || context.PlSqlStatementTokenIndex.Peek() == tokenOffset
								? OracleGrammarDescription.ReservedWordsPlSqlBody
								: OracleGrammarDescription.ReservedWordsPlSql;

							isNotReservedWord = !effectiveReservedWords.Contains(currentToken.UpperInvariantValue);
						}

						if (isNotReservedWord && scope == ReservedWordScope.PlSqlDeclaration)
						{
							isNotReservedWord = !OracleGrammarDescription.ReservedWordsPlSqlDeclaration.Contains(currentToken.UpperInvariantValue);
						}

						tokenIsValid &= isNotReservedWord;
					}
				}

				if (tokenIsValid)
				{
					var terminalNode =
						new StatementGrammarNode(NodeType.Terminal, context.Statement, currentToken)
						{
							Id = terminalId,
							Level = level,
							IsRequired = terminalReference.IsRequired,
							IsReservedWord = isReservedWord
						};

					nodes = new[] { terminalNode };
				}
			}

			return
				new ParseResult
				{
					NodeId = terminalId,
					Status = tokenIsValid ? ParseStatus.Success : ParseStatus.SequenceNotFound,
					Nodes = nodes
				};
		}

		private static IEnumerable<StatementCommentNode> AddCommentNodes(IEnumerable<StatementBase> statements, IEnumerable<OracleToken> comments)
		{
			var commentEnumerator = comments.GetEnumerator();
			var statemenEnumerator = statements.GetEnumerator();

			var tokenYielded = false;
			while (commentEnumerator.MoveNext())
			{
				while (tokenYielded || statemenEnumerator.MoveNext())
				{
					tokenYielded = false;

					if (commentEnumerator.Current.Index > statemenEnumerator.Current.SourcePosition.IndexEnd)
					{
						continue;
					}

					var targetNode = FindCommentTargetNode(statemenEnumerator.Current.RootNode, commentEnumerator.Current.Index);
				
					var commentNode = new StatementCommentNode(targetNode, commentEnumerator.Current);
					targetNode?.Comments.Add(commentNode);

					tokenYielded = true;

					yield return commentNode;

					break;
				}

				if (tokenYielded)
				{
					continue;
				}

				yield return new StatementCommentNode(null, commentEnumerator.Current);
			}
		}

		private static StatementGrammarNode FindCommentTargetNode(StatementGrammarNode node, int index)
		{
			while (true)
			{
				if (!node.SourcePosition.ContainsIndex(index))
				{
					return null;
				}

				// NOTE: FirstOrDefault must be used because child nodes in invalid grammar can overlap.
				var candidateNode = node.ChildNodes.FirstOrDefault(n => n.SourcePosition.ContainsIndex(index));
				if (candidateNode == null || candidateNode.Type == NodeType.Terminal)
				{
					return node;
				}

				node = candidateNode;
			}
		}

		private static bool HasInvalidGrammarNodes(StatementGrammarNode node)
		{
			return !node.IsGrammarValid || node.ChildNodes.Any(HasInvalidGrammarNodes);
		}

		private static SqlGrammarRuleSequenceNonTerminal CreateInitialNonTerminal(string nonTerminalId)
		{
			var rule = NonTerminalRules[nonTerminalId];
			return new SqlGrammarRuleSequenceNonTerminal { Id = nonTerminalId, TargetRule = rule };
		}

		private class ParseContext
		{
			public OracleStatement Statement;
			public IList<OracleToken> TokenBuffer;
			public CancellationToken CancellationToken;
			public readonly Stack<int> PlSqlStatementTokenIndex = new Stack<int>(); 
		}
	}

	[DebuggerDisplay("TerminalCandidate (Id={Id}; FollowingMandatoryCandidates={System.String.Join(\", \", FollowingMandatoryCandidates)})")]
	public struct TerminalCandidate
	{
		private static readonly string[] EmptyFollowingCandidates = new string[0];

		public readonly string Id;

		public readonly IReadOnlyList<string> FollowingMandatoryCandidates;

		public TerminalCandidate(string id, List<string> followingIds)
		{
			Id = id;
			FollowingMandatoryCandidates = followingIds == null ? EmptyFollowingCandidates : (IReadOnlyList<string>)followingIds.AsReadOnly();
		}

		public bool Equals(TerminalCandidate other)
		{
			return string.Equals(Id, other.Id);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is TerminalCandidate && Equals((TerminalCandidate)obj);
		}

		public override int GetHashCode()
		{
			return Id?.GetHashCode() ?? 0;
		}

		public static implicit operator string(TerminalCandidate candidate)
		{
			return candidate.Id;
		}

		public static implicit operator TerminalCandidate(string id)
		{
			return new TerminalCandidate(id, null);
		}

		public override string ToString()
		{
			var builder = new StringBuilder(GetCandidateLabel(Id));
			foreach (var id in FollowingMandatoryCandidates)
			{
				builder.Append(" ");
				builder.Append(GetCandidateLabel(id));
			}

			return builder.ToString();
		}

		private static string GetCandidateLabel(string id)
		{
			if (id.IsIdentifierOrAlias())
			{
				return "<identifier>";
			}

			switch (id)
			{
				case OracleGrammarDescription.Terminals.IntegerLiteral:
					return "<integer>";
				case OracleGrammarDescription.Terminals.NumberLiteral:
					return "<number>";
				case OracleGrammarDescription.Terminals.StringLiteral:
					return "<string>";
				case OracleGrammarDescription.Terminals.DoubleQuotedStringLiteral:
					return "<double quoted string>";
				default:
					return OracleGrammarDescription.Terminals.AllTerminals[id];
			}
		}
	}
}
