using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad.Oracle
{
	public class OracleSqlParser : ISqlParser
	{
		private static readonly Assembly LocalAssembly = typeof(OracleSqlParser).Assembly;
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(SqlGrammar));
		private static readonly Dictionary<string, SqlGrammarRule> NonTerminalRules;
		private static readonly Dictionary<string, SqlGrammarTerminal> Terminals;
		private static readonly HashSet<string> TerminatorIds;
		private static readonly HashSet<string> TerminatorValues;
		private static readonly SqlGrammarRuleSequenceNonTerminal[] AvailableNonTerminals;
		private static readonly Regex IdentifierMatcher;
		
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
					throw new InvalidOperationException(String.Format("Rule with starting non-terminal '{0}' has been already defined. ", rule.StartingNonTerminal));

				NonTerminalRules.Add(rule.StartingNonTerminal, rule);
			}

			Terminals = new Dictionary<string, SqlGrammarTerminal>();
			foreach (var terminal in oracleGrammar.Terminals)
			{
				if (Terminals.ContainsKey(terminal.Id))
					throw new InvalidOperationException(String.Format("Terminal '{0}' has been already defined. ", terminal.Id));

				terminal.Initialize();
				Terminals.Add(terminal.Id, terminal);
			}

			IdentifierMatcher = Terminals[OracleGrammarDescription.Terminals.Identifier].RegexMatcher;

			foreach (var rule in oracleGrammar.Rules)
			{
				foreach (var item in rule.Sequences.SelectMany(s => s.Items))
				{
					var nonTerminal = item as SqlGrammarRuleSequenceNonTerminal;
					if (nonTerminal != null)
					{
						nonTerminal.TargetRule = NonTerminalRules[nonTerminal.Id];
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;
						terminalReference.Terminal = Terminals[terminalReference.Id];
					}
				}
			}

			AvailableNonTerminals = oracleGrammar.StartSymbols.Select(s => CreateInitialNonTerminal(s.Id)).ToArray();

			TerminatorIds = new HashSet<string>(oracleGrammar.Terminators.Select(t => t.Id));
			TerminatorValues = new HashSet<string>(TerminatorIds.Select(id => Terminals[id].Value));
		}

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

		public bool IsRuleValid(string nonTerminalId, string text)
		{
			return IsRuleValid(nonTerminalId, OracleTokenReader.Create(text).GetTokens());
		}

		public bool IsRuleValid(StatementGrammarNode node)
		{
			return IsRuleValid(node.Id, node.Terminals.Select(t => (OracleToken)t.Token));
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
				throw new ArgumentNullException("tokenReader");
		}

		public Task<StatementCollection> ParseAsync(IEnumerable<OracleToken> tokens, CancellationToken cancellationToken)
		{
			return Task.Factory.StartNew(() => ProceedGrammar(tokens, cancellationToken), cancellationToken);
		}

		public ICollection<string> GetTerminalCandidates(StatementGrammarNode node)
		{
			var candidates = new HashSet<string>();

			var nonTerminalIds = new List<SqlGrammarRuleSequenceNonTerminal>();
			if (node != null)
			{
				if (node.Type != NodeType.Terminal)
				{
					throw new ArgumentException("Node must be a terminal node. ", "node");
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

		private void MatchNode(StatementGrammarNode node, ICollection<string> candidates)
		{
			var parent = node.ParentNode;
			if (parent == null)
				return;

			var matchParent = false;
			var compatibleSequences = NonTerminalRules[parent.Id].Sequences.SelectMany(s => GetCompatibleSequences(s, parent));

			if (parent.ParentNode != null)
			{
				compatibleSequences = compatibleSequences.Union(NonTerminalRules[parent.ParentNode.Id].Sequences.SelectMany(s => GetCompatibleSequences(s, parent)));
			}

			var childNodeIndex = parent.ChildNodes.IndexOf(node);

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

					if (item.Id == parent.ChildNodes[index].Id)
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

		private IEnumerable<SqlGrammarRuleSequence> GetCompatibleSequences(SqlGrammarRuleSequence sequence, StatementGrammarNode parentNode)
		{
			var inputItems = sequence.Items
				.Cast<ISqlGrammarRuleSequenceItem>()
				.TakeWhileInclusive(i => !i.IsRequired);

			var isInputSequence = inputItems.Any(i => i.Id == parentNode.ChildNodes[0].Id);

			return isInputSequence
				? Enumerable.Repeat(sequence, 1)
				: inputItems.Where(i => i.Type == NodeType.NonTerminal)
					.SelectMany(i => NonTerminalRules[i.Id].Sequences)
					.SelectMany(s => GetCompatibleSequences(s, parentNode));
		}

		private void GatherCandidatesFromNonterminal(SqlGrammarRuleSequenceNonTerminal nonTerminal, ICollection<string> candidates)
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

		private void GatherCandidatesFromGrammarItem(ISqlGrammarRuleSequenceItem item, ICollection<string> candidates)
		{
			var nonTerminal = item as SqlGrammarRuleSequenceNonTerminal;
			if (nonTerminal != null)
			{
				GatherCandidatesFromNonterminal(nonTerminal, candidates);
			}
			else if (!TerminatorIds.Contains(item.Id))
			{
				candidates.Add(item.Id);
			}
		}

		private static StatementCollection ProceedGrammar(IEnumerable<OracleToken> tokens, CancellationToken cancellationToken)
		{
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
			}

			var oracleSqlCollection = new List<StatementBase>();

			if (tokenBuffer.Count == 0)
			{
				oracleSqlCollection.Add(OracleStatement.EmptyStatement);
				return new StatementCollection(oracleSqlCollection, commentBuffer.Select(c => new StatementCommentNode(null, c)));
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

					if (!TerminatorIds.Contains(result.Nodes[result.Nodes.Count - 1].LastTerminalNode.Id) && tokenBuffer.Count > result.Nodes.Sum(n => n.TerminalCount))
					{
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
				if (lastNode != null && lastNode.FirstTerminalNode != null && TerminatorIds.Contains(lastNode.FirstTerminalNode.Id))
				{
					context.Statement.TerminatorNode = lastNode.FirstTerminalNode;
					result.Nodes.Remove(lastNode);
				}

				context.Statement.SourcePosition = new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
				var rootNode = new StatementGrammarNode(NodeType.NonTerminal, context.Statement, null)
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

			return new StatementCollection(oracleSqlCollection, commentNodes);
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
					if (tokenOffset >= context.TokenBuffer.Count && !isNodeRequired)
					{
						continue;
					}

					var childNodeId = item.Id;
					if (childNodeId == nonTerminalId && !isNodeRequired && workingTerminalCount == 0)
					{
						continue;
					}

					var bestCandidateOffset = tokenStartOffset + bestCandidateTerminalCount;
					var tryBestCandidates = bestCandidatesCompatible && !tokenReverted && bestCandidateTerminalCount > workingTerminalCount;
					var childNonTerminal = item as SqlGrammarRuleSequenceNonTerminal;
					if (item.Type == NodeType.NonTerminal)
					{
						var nestedResult = ProceedNonTerminal(context, childNonTerminal, level + 1, tokenOffset, false,  scope);

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
							var bestCandidatePosition = new Dictionary<int, StatementGrammarNode>();

							// Candidate nodes can be multiplied or terminals can be spread among different nonterminals,
							// therefore we fetch the node with most terminals or the later (when nodes contain same terminals).
							foreach (var candidate in nestedResult.BestCandidates)
							{
								StatementGrammarNode storedNode;
								if (!bestCandidatePosition.TryGetValue(candidate.SourcePosition.IndexStart, out storedNode) ||
									storedNode.SourcePosition.IndexEnd <= candidate.SourcePosition.IndexEnd)
								{
									bestCandidatePosition[candidate.SourcePosition.IndexStart] = candidate;
								}
							}

							alternativeNode.AddChildNodes(bestCandidatePosition.Values);

							if (workingNodes.Count != bestCandidateNodes.Count || optionalTokenReverted > 0 || nestedResult.Status == ParseStatus.SequenceNotFound)
							{
								bestCandidateTerminalCount = CreateNewNodeList(workingNodes, bestCandidateNodes);
							}

							bestCandidateNodes.Add(alternativeNode);
							bestCandidateTerminalCount += alternativeNode.TerminalCount;
							
							bestCandidatesCompatible = true;
						}

						if (nestedResult.Nodes.Count > 0 && isNestedNodeValid)
						{
							nestedNode.AddChildNodes(nestedResult.Nodes);
							workingNodes.Add(nestedNode);
							workingTerminalCount += nestedResult.TerminalCount;
						}

						if (result.Status == ParseStatus.SequenceNotFound)
						{
							if (workingNodes.Count == 0)
								break;

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
					if (bestCandidateNodes.Count > 0)
					{
						var currentTerminalCount = bestCandidateNodes.SelectMany(n => n.Terminals).TakeWhile(t => !t.Id.IsIdentifierOrAlias() && !t.Id.IsLiteral()).Count();
						if (currentTerminalCount > workingTerminalCount)
						{
							workingNodes.ForEach(n => n.IsGrammarValid = false);
						}
					}
					#endregion

					if (isSequenceValid)
						break;
				}
			}

			result.BestCandidates = bestCandidateNodes;
			result.TerminalCount = workingTerminalCount;
			result.BestCandidateTerminalCount = bestCandidateTerminalCount;

			return result;
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
				return;

			var bestCandidateResult = getForceParseResultFunction();
			if (bestCandidateResult.Status == ParseStatus.SequenceNotFound)
				return;

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

			var effectiveTerminalCount = newResult.Status == ParseStatus.SequenceNotFound ? newResult.BestCandidateTerminalCount : newResult.TerminalCount;
			var revertNode = effectiveTerminalCount >= optionalTerminalCount &&
							 effectiveTerminalCount > currentResult.TerminalCount;

			if (!revertNode)
				return 0;
			
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
					tokenIsValid = String.CompareOrdinal(terminal.Value, tokenValue) == 0 || (terminal.AllowQuotedNotation && tokenValue.Length == terminal.Value.Length + 2 && tokenValue[0] == '"' && tokenValue[tokenValue.Length - 1] == '"' && String.CompareOrdinal(tokenValue.Substring(1, tokenValue.Length - 2), terminal.Value) == 0);
					isReservedWord = tokenIsValid && (scope == ReservedWordScope.Sql ? terminal.ReservedWord == ReservedWordType.Sql : terminal.ReservedWord > 0);
				}
				else
				{
					tokenIsValid = terminal.RegexMatcher.IsMatch(currentToken.Value);
					if (tokenIsValid && !terminalReference.AllowReservedWord)
					{
						var isNotReservedWord = !OracleGrammarDescription.ReservedWordsSql.Contains(currentToken.UpperInvariantValue);

						if (isNotReservedWord && (scope == ReservedWordScope.PlSqlBody || scope == ReservedWordScope.PlSqlDeclaration))
						{
							isNotReservedWord = !OracleGrammarDescription.ReservedWordsPlSql.Contains(currentToken.UpperInvariantValue);
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
			
			return new ParseResult
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
					if (targetNode != null)
					{
						targetNode.Comments.Add(commentNode);
					}
					
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
		}
	}
}
