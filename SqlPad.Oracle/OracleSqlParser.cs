using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad.Oracle
{
	public class OracleSqlParser : ISqlParser
	{
		private static readonly Assembly LocalAssembly = typeof(OracleSqlParser).Assembly;
		private static readonly SqlGrammar OracleGrammar;
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(SqlGrammar));
		private static readonly Dictionary<string, SqlGrammarRuleSequence[]> StartingNonTerminalSequences;
		private static readonly Dictionary<string, SqlGrammarTerminal> Terminals;
		private static readonly HashSet<string> TerminatorIds;
		private static readonly HashSet<string> TerminatorValues;
		private static readonly string[] AvailableNonTerminals;
		
		static OracleSqlParser()
		{
			using (var grammarReader = XmlReader.Create(LocalAssembly.GetManifestResourceStream("SqlPad.Oracle.OracleSqlGrammar.xml")))
			{
				OracleGrammar = (SqlGrammar)XmlSerializer.Deserialize(grammarReader);
			}

			/*var hashSet = new HashSet<string>();
			foreach (var rule in _sqlGrammar.Rules)
			{
				if (hashSet.Contains(rule.StartingNonTerminal))
				{
					
				}

				hashSet.Add(rule.StartingNonTerminal);
			}*/

			StartingNonTerminalSequences = OracleGrammar.Rules.ToDictionary(r => r.StartingNonTerminal, r => r.Sequences);
			/*var containsSequenceWithAllOptionalMembers = _startingNonTerminalSequences.Values.SelectMany(s => s)
				.Any(s => s.Items.All(i => (i as SqlGrammarRuleSequenceTerminal != null && !((SqlGrammarRuleSequenceTerminal)i).IsRequired) ||
				                           (i as SqlGrammarRuleSequenceNonTerminal != null && !((SqlGrammarRuleSequenceNonTerminal)i).IsRequired)));
			if (containsSequenceWithAllOptionalMembers)
				throw new InvalidOperationException("Grammar sequence must have at least one mandatory item. ");*/

			Terminals = new Dictionary<string, SqlGrammarTerminal>();
			foreach (var terminal in OracleGrammar.Terminals)
			{
				if (Terminals.ContainsKey(terminal.Id))
					throw new InvalidOperationException(String.Format("Terminal '{0}' has been already defined. ", terminal.Id));

				terminal.Initialize();
				Terminals.Add(terminal.Id, terminal);
			}

			AvailableNonTerminals = OracleGrammar.StartSymbols.Select(s => s.Id).ToArray();
			TerminatorIds = new HashSet<string>(OracleGrammar.Terminators.Select(t => t.Id));
			TerminatorValues = new HashSet<string>(TerminatorIds.Select(id => Terminals[id].Value));
		}

		public static bool IsValidIdentifier(string identifier)
		{
			return Regex.IsMatch(identifier, Terminals[OracleGrammarDescription.Terminals.Identifier].RegexValue) &&
			       !identifier.IsKeyword();
		}

		public bool IsKeyword(string value)
		{
			return value.IsKeyword();
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
			return IsRuleValid(nonTerminalId, OracleTokenReader.Create(text).GetTokens().Cast<OracleToken>());
		}

		public bool IsRuleValid(StatementDescriptionNode node)
		{
			return IsRuleValid(node.Id, node.Terminals.Select(t => (OracleToken)t.Token));
		}

		private bool IsRuleValid(string nonTerminalId, IEnumerable<OracleToken> tokens)
		{
			var result = ProceedNonTerminal(null, nonTerminalId, 0, 0, false, new List<OracleToken>(tokens));
			return result.Status == ProcessingStatus.Success &&
			       result.Nodes.All(n => n.AllChildNodes.All(c => c.IsGrammarValid))/* &&
			       result.Terminals.Count() == result.BestCandidates.Sum(n => n.Terminals.Count())*/;
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
			if (tokenReader == null)
				throw new ArgumentNullException("tokenReader");

			return Parse(tokenReader.GetTokens().Cast<OracleToken>());
		}

		public StatementCollection Parse(IEnumerable<OracleToken> tokens)
		{
			return ProceedGrammar(tokens);
		}

		public ICollection<string> GetTerminalCandidates(StatementDescriptionNode node)
		{
			var candidates = new HashSet<string>();

			var nonTerminalIds = new List<string>();
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

		private void MatchNode(StatementDescriptionNode node, ICollection<string> candidates)
		{
			var parent = node.ParentNode;
			if (parent == null)
				return;

			var matchParent = false;
			var compatibleSequences = StartingNonTerminalSequences[parent.Id].SelectMany(s => GetCompatibleSequences(s, parent));

			if (parent.ParentNode != null)
			{
				compatibleSequences = compatibleSequences.Union(StartingNonTerminalSequences[parent.ParentNode.Id].SelectMany(s => GetCompatibleSequences(s, parent)));
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

		private IEnumerable<SqlGrammarRuleSequence> GetCompatibleSequences(SqlGrammarRuleSequence sequence, StatementDescriptionNode parentNode)
		{
			var inputItems = sequence.Items
				.Cast<ISqlGrammarRuleSequenceItem>()
				.TakeWhileInclusive(i => !i.IsRequired);

			var isInputSequence = inputItems.Any(i => i.Id == parentNode.ChildNodes[0].Id);

			return isInputSequence
				? Enumerable.Repeat(sequence, 1)
				: inputItems.Where(i => i.Type == NodeType.NonTerminal)
					.SelectMany(i => StartingNonTerminalSequences[i.Id])
					.SelectMany(s => GetCompatibleSequences(s, parentNode));
		}

		private void GatherCandidatesFromNonterminal(string nonTerminalId, ICollection<string> candidates)
		{
			foreach (var sequence in StartingNonTerminalSequences[nonTerminalId])
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
			if (item.Type == NodeType.NonTerminal)
			{
				GatherCandidatesFromNonterminal(item.Id, candidates);
			}
			else
			{
				candidates.Add(item.Id);
			}
		}

		#region not used - just for reference
		public ICollection<string> GetTerminalCandidatesOld(StatementDescriptionNode node)
		{
			var terminalsToMatch = new List<StatementDescriptionNode>();
			var nonTerminalIds = new List<string>();
			if (node != null)
			{
				terminalsToMatch.AddRange(node.RootNode.Terminals.TakeWhileInclusive(t => t != node.LastTerminalNode));
				nonTerminalIds.Add(node.RootNode.Id);
			}
			else
			{
				nonTerminalIds.AddRange(AvailableNonTerminals);
			}

			var nextItems = new HashSet<string>();

			foreach (var nonTerminalId in nonTerminalIds)
			{
				MatchNonTerminal(terminalsToMatch, nonTerminalId, 0, nextItems);
			}

			return nextItems.ToArray();
		}

		private struct MatchResult
		{
			public int TerminalCount { get; set; }

			public int CandidatesGathered { get; set; } 
		}

		private MatchResult MatchNonTerminal(IList<StatementDescriptionNode> terminalSource, string nonTerminalId, int startIndex, ICollection<string> nextItems)
		{
			var matchedTerminals = 0;
			var candidatesGatherEnabled = false;
			var candidatesGathered = 0;
			var sequenceMatched = true;
			foreach (var sequence in StartingNonTerminalSequences[nonTerminalId])
			{
				var nestedStartIndex = startIndex;
				candidatesGathered = 0;
				sequenceMatched = true;

				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					if (item.Type == NodeType.NonTerminal)
					{
						var result = MatchNonTerminal(terminalSource, item.Id, nestedStartIndex, nextItems);
						if (result.TerminalCount == 0 && item.IsRequired)
						{
							sequenceMatched = false;
							break;
						}

						nestedStartIndex += result.TerminalCount;
						matchedTerminals += result.TerminalCount + result.CandidatesGathered;
					}
					else
					{
						if (terminalSource.Count == nestedStartIndex)
						{
							candidatesGatherEnabled = true;
						}

						if (candidatesGatherEnabled)
						{
							nextItems.Add(item.Id);
							candidatesGathered++;

							if (item.IsRequired)
							{
								break;
							}
						}
						else if (item.Id == terminalSource[nestedStartIndex].Id)
						{
							nestedStartIndex++;
						}
						else if (item.IsRequired)
						{
							sequenceMatched = false;
							break;
						}
					}
				}

				if (sequenceMatched && !candidatesGatherEnabled)
				{
					matchedTerminals = nestedStartIndex - startIndex;
					break;
				}
			}

			return new MatchResult { TerminalCount = sequenceMatched ? matchedTerminals : 0, CandidatesGathered = candidatesGathered };
		}
		#endregion

		private StatementCollection ProceedGrammar(IEnumerable<OracleToken> tokens)
		{
			var tokenBuffer = new List<OracleToken>(tokens);

			var oracleSqlCollection = new List<StatementBase>();

			if (tokenBuffer.Count == 0)
			{
				oracleSqlCollection.Add(OracleStatement.EmptyStatement);
				return new StatementCollection(oracleSqlCollection);
			}
			
			do
			{
				var result = new ProcessingResult();
				var oracleStatement = new OracleStatement();

				foreach (var nonTerminal in AvailableNonTerminals)
				{
					var newResult = ProceedNonTerminal(oracleStatement, nonTerminal, 1, 0, false, tokenBuffer);

					//if (newResult.Nodes.SelectMany(n => n.AllChildNodes).Any(n => n.Terminals.Count() != n.TerminalCount))
					//	throw new ApplicationException("StatementDescriptionNode TerminalCount value is invalid. ");

					if (newResult.Status != ProcessingStatus.Success)
					{
						if (result.BestCandidates == null || newResult.BestCandidates.Sum(n => n.TerminalCount) > result.BestCandidates.Sum(n => n.TerminalCount))
						{
							result = newResult;
						}

						continue;
					}

					result = newResult;

					if (!TerminatorIds.Contains(result.Nodes.Last().Id) && tokenBuffer.Count > result.Terminals.Count())
					{
						result.Status = ProcessingStatus.SequenceNotFound;
					}

					break;
				}

				int indexStart;
				int indexEnd;
				if (result.Status != ProcessingStatus.Success)
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
					var lastTerminal = result.Terminals.Last().Token;
					indexStart = result.Terminals.First().Token.Index;
					indexEnd = lastTerminal.Index + lastTerminal.Value.Length - 1;

					tokenBuffer.RemoveRange(0, result.Terminals.Count());

					if (result.Nodes.Any(n => n.AllChildNodes.Any(c => !c.IsGrammarValid)))
					{
						//var invalidNodes = result.Nodes.SelectMany(n => n.AllChildNodes).Where(n => !n.IsGrammarValid).ToArray();
						result.Status = ProcessingStatus.SequenceNotFound;
					}
				}

				var lastNode = result.Nodes.LastOrDefault();
				if (lastNode != null && TerminatorIds.Contains(lastNode.Id))
				{
					oracleStatement.TerminatorNode = lastNode;
					result.Nodes.Remove(lastNode);
				}

				oracleStatement.SourcePosition = new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
				var rootNode = new StatementDescriptionNode(oracleStatement, NodeType.NonTerminal)
				               {
					               Id = result.NodeId,
								   IsGrammarValid = result.Nodes.All(n => n.IsGrammarValid),
								   IsRequired = true,
				               };
				
				rootNode.AddChildNodes(result.Nodes);
				
				oracleStatement.RootNode = rootNode;
				oracleStatement.ProcessingStatus = result.Status;
				oracleSqlCollection.Add(oracleStatement);
			}
			while (tokenBuffer.Count > 0);

			return new StatementCollection(oracleSqlCollection);
		}

		private ProcessingResult ProceedNonTerminal(OracleStatement statement, string nonTerminal, int level, int tokenStartOffset, bool tokenReverted, IList<OracleToken> tokenBuffer)
		{
			var bestCandidateNodes = new List<StatementDescriptionNode>();
			var workingNodes = new List<StatementDescriptionNode>();
			var result = new ProcessingResult
			             {
							 NodeId = nonTerminal,
							 Nodes = workingNodes,
							 BestCandidates = new List<StatementDescriptionNode>(),
			             };

			var workingTerminalMaxCount = 0;
			foreach (var sequence in StartingNonTerminalSequences[nonTerminal])
			{
				result.Status = ProcessingStatus.Success;
				workingNodes.Clear();

				var bestCandidatesCompatible = false;
				var isSequenceValid = true;

				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					var workingTerminalCount = workingNodes.Sum(t => t.TerminalCount);
					var tokenOffset = tokenStartOffset + workingTerminalCount;

					var bestCandidateTerminalCount = bestCandidateNodes.Sum(t => t.TerminalCount);
					var bestCandidateOffset = tokenStartOffset + bestCandidateTerminalCount;
					var tryBestCandidates = bestCandidatesCompatible && !tokenReverted && bestCandidateTerminalCount > workingTerminalCount;
					
					if (item.Type == NodeType.NonTerminal)
					{
						var nestedResult = ProceedNonTerminal(statement, item.Id, level + 1, tokenOffset, false, tokenBuffer);

						var optionalTokenReverted = TryRevertOptionalToken(optionalTerminalCount => ProceedNonTerminal(statement, item.Id, level + 1, tokenOffset - optionalTerminalCount, true, tokenBuffer), ref nestedResult, workingNodes);

						TryParseInvalidGrammar(tryBestCandidates, () => ProceedNonTerminal(statement, item.Id, level + 1, bestCandidateOffset, false, tokenBuffer), ref nestedResult, workingNodes, bestCandidateNodes);

						if (item.IsRequired || nestedResult.Status == ProcessingStatus.Success)
						{
							result.Status = nestedResult.Status;
						}

						var nestedNode = new StatementDescriptionNode(statement, NodeType.NonTerminal)
						                 {
											 Id = item.Id,
											 Level = level,
											 IsRequired = item.IsRequired,
											 IsGrammarValid = nestedResult.Status == ProcessingStatus.Success
						                 };
						
						var alternativeNode = nestedNode.Clone();

						if (nestedResult.BestCandidates.Count > 0 &&
							workingTerminalCount + nestedResult.BestCandidates.Sum(n => n.TerminalCount) > bestCandidateTerminalCount)
						{
							var bestCandidatePosition = new Dictionary<int, StatementDescriptionNode>();
							// Candidate nodes can be multiplied or terminals can be spread among different nonterminals,
							// therefore we fetch the node with most terminals or the later (when nodes contain same terminals).
							foreach (var candidate in nestedResult.BestCandidates)
							{
								if (!bestCandidatePosition.ContainsKey(candidate.SourcePosition.IndexStart) ||
									bestCandidatePosition[candidate.SourcePosition.IndexStart].SourcePosition.IndexEnd <= candidate.SourcePosition.IndexEnd)
									bestCandidatePosition[candidate.SourcePosition.IndexStart] = candidate;
							}

							alternativeNode.AddChildNodes(bestCandidatePosition.Values);

							if (workingNodes.Count != bestCandidateNodes.Count || optionalTokenReverted || nestedResult.Status == ProcessingStatus.SequenceNotFound)
								bestCandidateNodes = new List<StatementDescriptionNode>(workingNodes.Select(n => n.Clone()));

							bestCandidateNodes.Add(alternativeNode);
							
							bestCandidatesCompatible = true;
						}

						if (nestedResult.Nodes.Count > 0 && nestedResult.Status == ProcessingStatus.Success)
						{
							nestedNode.AddChildNodes(nestedResult.Nodes);
							workingNodes.Add(nestedNode);
						}

						if (result.Status == ProcessingStatus.SequenceNotFound)
						{
							if (workingNodes.Count == 0)
								break;

							isSequenceValid = false;
							workingNodes.Add(alternativeNode.Clone());
						}
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						var terminalResult = IsTokenValid(statement, terminalReference, level, tokenOffset, tokenBuffer);

						TryParseInvalidGrammar(tryBestCandidates, () => IsTokenValid(statement, terminalReference, level, bestCandidateOffset, tokenBuffer), ref terminalResult, workingNodes, bestCandidateNodes);

						if (terminalResult.Status == ProcessingStatus.SequenceNotFound)
						{
							if (terminalReference.IsRequired)
							{
								result.Status = ProcessingStatus.SequenceNotFound;
								break;
							}

							continue;
						}

						var terminalNode = terminalResult.Nodes.Single();
						workingNodes.Add(terminalNode);
						bestCandidateNodes.Add(terminalNode.Clone());
					}
				}

				if (result.Status == ProcessingStatus.Success)
				{
					#region CASE WHEN issue
					if (bestCandidateNodes.Count > 0)
					{
						var currentTerminalCount = bestCandidateNodes.SelectMany(n => n.Terminals).TakeWhile(t => !t.Id.IsIdentifierOrAlias() && !t.Id.IsLiteral()).Count();
						workingTerminalMaxCount = Math.Max(workingTerminalMaxCount, currentTerminalCount);

						var workingTerminalCount = workingNodes.Sum(t => t.TerminalCount);
						if (workingTerminalMaxCount > workingTerminalCount)
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

			return result;
		}

		private void TryParseInvalidGrammar(bool preconditionsValid, Func<ProcessingResult> getForceParseProcessingResultFunction, ref ProcessingResult processingResult, List<StatementDescriptionNode> workingNodes, IEnumerable<StatementDescriptionNode> bestCandidateNodes)
		{
			if (!preconditionsValid || processingResult.Status == ProcessingStatus.Success)
				return;

			var bestCandidateResult = getForceParseProcessingResultFunction();
			if (bestCandidateResult.Status == ProcessingStatus.SequenceNotFound)
				return;
			
			workingNodes.Clear();
			workingNodes.AddRange(bestCandidateNodes.Select(n => n.Clone()));
			processingResult = bestCandidateResult;
		}

		private bool TryRevertOptionalToken(Func<int, ProcessingResult> getAlternativeProcessingResultFunction, ref ProcessingResult currentResult, IList<StatementDescriptionNode> workingNodes)
		{
			var optionalNodeCandidate = workingNodes.Count > 0 ? workingNodes[workingNodes.Count - 1].LastTerminalNode : null;
			optionalNodeCandidate = optionalNodeCandidate != null && optionalNodeCandidate.IsRequired ? optionalNodeCandidate.ParentNode : optionalNodeCandidate;

			if (optionalNodeCandidate == null || optionalNodeCandidate.IsRequired)
				return false;

			var optionalTerminalCount = optionalNodeCandidate.TerminalCount;
			var newResult = getAlternativeProcessingResultFunction(optionalTerminalCount);

			var newResultTerminalCount = newResult.BestCandidates.Sum(n => n.TerminalCount);
			var revertNode = newResultTerminalCount >= optionalTerminalCount &&
			                 newResultTerminalCount > currentResult.Nodes.Sum(n => n.TerminalCount);

			if (!revertNode)
				return false;
			
			currentResult = newResult;
			RevertLastOptionalNode(workingNodes, optionalNodeCandidate.Type);
			return true;
		}

		private void RevertLastOptionalNode(IList<StatementDescriptionNode> workingNodes, NodeType nodeType)
		{
			var nodeToRemove = workingNodes[workingNodes.Count - 1];
			if (nodeType == NodeType.Terminal && nodeToRemove.Type == NodeType.NonTerminal)
			{
				nodeToRemove.RemoveLastChildNodeIfOptional();
			}
			else
			{
				workingNodes.RemoveAt(workingNodes.Count - 1);
			}
		}

		private ProcessingResult IsTokenValid(StatementBase statement, ISqlGrammarRuleSequenceItem terminalReference, int level, int tokenOffset, IList<OracleToken> tokenBuffer)
		{
			var tokenIsValid = false;
			ICollection<StatementDescriptionNode> nodes = null;

			if (tokenBuffer.Count > tokenOffset)
			{
				var currentToken = tokenBuffer[tokenOffset];

				var terminal = Terminals[terminalReference.Id];
				var isKeyword = false;
				if (!String.IsNullOrEmpty(terminal.RegexValue))
				{
					tokenIsValid = terminal.RegexMatcher.IsMatch(currentToken.Value) && !currentToken.Value.IsKeyword();
				}
				else
				{
					var tokenValue = currentToken.Value.ToUpperInvariant();
					tokenIsValid = terminal.Value == tokenValue || (terminal.AllowQuotedNotation && tokenValue == "\"" + terminal.Value + "\"");
					isKeyword = tokenIsValid && terminal.IsKeyword;
				}

				if (tokenIsValid)
				{
					var terminalNode = new StatementDescriptionNode(statement, NodeType.Terminal)
					               {
						               Id = terminalReference.Id,
						               Level = level,
						               IsRequired = terminalReference.IsRequired,
						               Token = currentToken,
									   IsKeyword = isKeyword
					               };

					nodes = new[] { terminalNode };
				}
			}
			
			return new ProcessingResult
			       {
					   NodeId = terminalReference.Id,
				       Status = tokenIsValid ? ProcessingStatus.Success : ProcessingStatus.SequenceNotFound,
					   Nodes = nodes
			       };
		}
	}
}
