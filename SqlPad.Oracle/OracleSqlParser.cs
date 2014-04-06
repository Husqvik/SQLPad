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
		private readonly SqlGrammar _sqlGrammar;
		private readonly Dictionary<string, SqlGrammarRuleSequence[]> _startingNonTerminalSequences;
		private readonly Dictionary<string, SqlGrammarTerminal> _terminals;
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(SqlGrammar));
		private readonly List<IStatement> _oracleSqlCollection = new List<IStatement>();
		private readonly HashSet<string> _keywords;
		private readonly HashSet<string> _terminators;
		
		private readonly List<string> _availableNonTerminals;

		public OracleSqlParser()
		{
			using (var grammarReader = XmlReader.Create(LocalAssembly.GetManifestResourceStream("SqlPad.Oracle.OracleSqlGrammar.xml")))
			{
				_sqlGrammar = (SqlGrammar)XmlSerializer.Deserialize(grammarReader);
			}

			/*var hashSet = new HashSet<string>();
			foreach (var rule in _sqlGrammar.Rules)
			{
				if (hashSet.Contains(rule.StartingNonTerminal))
				{
					
				}

				hashSet.Add(rule.StartingNonTerminal);
			}*/
			
			_startingNonTerminalSequences = _sqlGrammar.Rules.ToDictionary(r => r.StartingNonTerminal, r => r.Sequences);
			/*var containsSequenceWithAllOptionalMembers = _startingNonTerminalSequences.Values.SelectMany(s => s)
				.Any(s => s.Items.All(i => (i as SqlGrammarRuleSequenceTerminal != null && !((SqlGrammarRuleSequenceTerminal)i).IsRequired) ||
				                           (i as SqlGrammarRuleSequenceNonTerminal != null && !((SqlGrammarRuleSequenceNonTerminal)i).IsRequired)));
			if (containsSequenceWithAllOptionalMembers)
				throw new InvalidOperationException("Grammar sequence must have at least one mandatory item. ");*/

			_terminals = _sqlGrammar.Terminals.ToDictionary(t => t.Id, t => t);
			_keywords = new HashSet<string>(_sqlGrammar.Terminals.Where(t => t.IsKeyword).Select(t => t.Value));
			_availableNonTerminals = _sqlGrammar.StartSymbols.Select(s => s.Id).ToList();
			_terminators = new HashSet<string>(_sqlGrammar.Terminators.Select(t => t.Value));
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
			var result = ProceedNonTerminal(nonTerminalId, 0, 0, false, new List<OracleToken>(tokens));
			return result.Status == ProcessingStatus.Success &&
			       result.Nodes.All(n => n.AllChildNodes.All(c => c.IsGrammarValid))/* &&
			       result.Terminals.Count() == result.BestCandidates.Sum(n => n.Terminals.Count())*/;
		}

		public ICollection<IStatement> Parse(string sqlText)
		{
			using (var reader = new StringReader(sqlText))
			{
				return Parse(OracleTokenReader.Create(reader));
			}
		}

		public ICollection<IStatement> Parse(OracleTokenReader tokenReader)
		{
			if (tokenReader == null)
				throw new ArgumentNullException("tokenReader");

			return Parse(tokenReader.GetTokens().Cast<OracleToken>());
		}

		public ICollection<IStatement> Parse(IEnumerable<OracleToken> tokens)
		{
			ProceedGrammar(tokens);

			return _oracleSqlCollection.AsReadOnly();
		}

		/*public ICollection<string> GetTerminalCandidates(StatementDescriptionNode node)
		{
			var startNode = node;
			var visitedIds = new HashSet<string>();
			var terminals = Enumerable.Empty<string>();

			while (node.ParentNode != null)
			{
				var skipTerminalScan = !node.ParentNode.IsGrammarValid && !node.IsGrammarValid;
				node = node.ParentNode;

				if (skipTerminalScan)
				{
					continue;
				}

				var nextIds = new List<ISqlGrammarRuleSequenceItem>();

				foreach (var sequence in _startingNonTerminalSequences[node.Id])
				{
					var grammarItemEnumerator = sequence.Items.Cast<ISqlGrammarRuleSequenceItem>().GetEnumerator();
					var nodeEnumerator = node.ChildNodes.Where(n => n.SourcePosition.IndexStart <= startNode.SourcePosition.IndexStart).GetEnumerator();

					if (!nodeEnumerator.MoveNext())
					{
						break;
					}

					var sequenceValid = true;
					do
					{
						var grammarItemFound = false;
						while (grammarItemEnumerator.MoveNext())
						{
							if (nodeEnumerator.Current.Id == grammarItemEnumerator.Current.Id)
							{
								grammarItemFound = true;
								break;
							}

							if (grammarItemEnumerator.Current.IsRequired)
							{
								break;
							}
						}

						if (!grammarItemFound)
						{
							sequenceValid = false;
							break;
						}
					}
					while (nodeEnumerator.MoveNext());

					if (sequenceValid)
					{
						while (grammarItemEnumerator.MoveNext())
						{
							nextIds.Add(grammarItemEnumerator.Current);

							if (grammarItemEnumerator.Current.IsRequired)
							{
								break;
							}
						}

						//break;
					}
				}

				terminals = terminals.Concat(GetTerminalCandidates(nextIds, visitedIds));
			}

			terminals = terminals.ToArray();

			return terminals.ToArray();
		}*/

		public ICollection<string> GetTerminalCandidates(StatementDescriptionNode node)
		{
			var terminalsToMatch = node.RootNode.Terminals.TakeWhileInclusive(t => t != node.LastTerminalNode).ToArray();
			var nextItems = new HashSet<ISqlGrammarRuleSequenceItem>();

			MatchNonTerminal(terminalsToMatch, node.RootNode.Id, 0, nextItems);

			return new HashSet<string>(nextItems.Select(i => i.Id)).ToArray();
		}

		private int MatchNonTerminal(IList<StatementDescriptionNode> terminalSource, string nonTerminalId, int startIndex, ICollection<ISqlGrammarRuleSequenceItem> nextItems)
		{
			var matchedTerminals = 0;
			var candidatesGathered = false;
			foreach (var sequence in _startingNonTerminalSequences[nonTerminalId])
			{
				var nestedStartIndex = startIndex;
				var sequenceMatched = true;

				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					if (item.Type == NodeType.NonTerminal)
					{
						var nestedMatchedTerminals = MatchNonTerminal(terminalSource, item.Id, nestedStartIndex, nextItems);
						if (nestedMatchedTerminals == 0 && item.IsRequired)
						{
							sequenceMatched = false;
							break;
						}

						nestedStartIndex += nestedMatchedTerminals;
					}
					else
					{
						if (terminalSource.Count == nestedStartIndex)
						{
							candidatesGathered = true;
						}

						if (candidatesGathered)
						{
							nextItems.Add(item);

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

				if (sequenceMatched && !candidatesGathered)
				{
					matchedTerminals = nestedStartIndex - startIndex;
					break;
				}
			}

			return matchedTerminals;
		}

		/*private IEnumerable<string> GetTerminalCandidates(IEnumerable<ISqlGrammarRuleSequenceItem> nodes, ISet<string> visitedIds)
		{
			foreach (var childNode in nodes)
			{
				if (!visitedIds.Add(childNode.Id))
					continue;

				if (childNode.Type == NodeType.NonTerminal)
				{
					foreach (var terminalId in _startingNonTerminalSequences[childNode.Id]
						.SelectMany(s => GetTerminalCandidates(GetSequenceItemCandidates(s), visitedIds)))
					{
						yield return terminalId;
					}
				}
				else
				{
					yield return childNode.Id;

					if (childNode.IsRequired)
					{
						break;
					}
				}
			}
		}

		private IEnumerable<ISqlGrammarRuleSequenceItem> GetSequenceItemCandidates(SqlGrammarRuleSequence sequence)
		{
			foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
			{
				yield return item;

				if (item.IsRequired)
				{
					break;
				}
			}
		}*/

		private void ProceedGrammar(IEnumerable<OracleToken> tokens)
		{
			var tokenBuffer = new List<OracleToken>(tokens);

			_oracleSqlCollection.Clear();

			if (tokenBuffer.Count == 0)
			{
				_oracleSqlCollection.Add(OracleStatement.EmptyStatement);
				return;
			}
			
			var result = new ProcessingResult();

			do
			{
				var oracleSql = new OracleStatement();

				foreach (var nonTerminal in _availableNonTerminals)
				{
					result = ProceedNonTerminal(nonTerminal, 0, 0, false, tokenBuffer);

					if (result.Status != ProcessingStatus.Success)
						continue;
					
					var lastTerminal = result.Terminals.Last();
					if (!_terminators.Contains(lastTerminal.Token.Value) && tokenBuffer.Count > result.Terminals.Count())
					{
						result.Status = ProcessingStatus.SequenceNotFound;
					}

					break;
				}

				int indexStart;
				int indexEnd;
				if (result.Status != ProcessingStatus.Success)
				{
					oracleSql.TerminalCandidates = result.TerminalCandidates;

					if (result.BestCandidates.Sum(n => n.Terminals.Count()) > result.Nodes.Sum(n => n.Terminals.Count()))
					{
						result.Nodes = result.BestCandidates;
					}

					indexStart = tokenBuffer.First().Index;

					var index = tokenBuffer.FindIndex(t => _terminators.Contains(t.Value));
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

				oracleSql.SourcePosition = new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
				oracleSql.NodeCollection = result.Nodes;
				oracleSql.ProcessingStatus = result.Status;
				_oracleSqlCollection.Add(oracleSql);
			}
			while (tokenBuffer.Count > 0);
		}

		private ProcessingResult ProceedNonTerminal(string nonTerminal, int level, int tokenStartOffset, bool tokenReverted, IList<OracleToken> tokenBuffer)
		{
			var bestCandidateNodes = new List<StatementDescriptionNode>();
			var workingNodes = new List<StatementDescriptionNode>();
			var terminalCandidates = new HashSet<string>();
			var result = new ProcessingResult
			             {
							 Nodes = workingNodes,
							 BestCandidates = new List<StatementDescriptionNode>(),
							 TerminalCandidates = terminalCandidates
			             };

			foreach (var sequence in _startingNonTerminalSequences[nonTerminal])
			{
				result.Status = ProcessingStatus.Success;
				workingNodes.Clear();

				var bestCandidatesCompatible = false;

				foreach (ISqlGrammarRuleSequenceItem item in sequence.Items)
				{
					var workingTerminalCount = workingNodes.Sum(t => t.Terminals.Count());
					var tokenOffset = tokenStartOffset + workingTerminalCount;
					
					var bestCandidateTerminalCount = bestCandidateNodes.Sum(t => t.Terminals.Count());
					var bestCandidateOffset = tokenStartOffset + bestCandidateTerminalCount;
					var tryBestCandidates = bestCandidatesCompatible && !tokenReverted && bestCandidateTerminalCount > workingTerminalCount;
					
					if (item.Type == NodeType.NonTerminal)
					{
						var nestedResult = ProceedNonTerminal(item.Id, level + 1, tokenOffset, false, tokenBuffer);

						TryRevertOptionalToken(optionalTerminalCount => ProceedNonTerminal(item.Id, level + 1, tokenOffset - optionalTerminalCount, true, tokenBuffer), ref nestedResult, workingNodes);

						TryParseInvalidGrammar(tryBestCandidates, () => ProceedNonTerminal(item.Id, level + 1, bestCandidateOffset, false, tokenBuffer), ref nestedResult, workingNodes, bestCandidateNodes);

						if (item.IsRequired || nestedResult.Status == ProcessingStatus.Success)
						{
							result.Status = nestedResult.Status;
						}

						var nestedNode = new StatementDescriptionNode(NodeType.NonTerminal)
						                 {
											 Id = item.Id,
											 Level = level,
											 IsRequired = item.IsRequired,
											 IsGrammarValid = nestedResult.Status == ProcessingStatus.Success
						                 };
						
						var alternativeNode = nestedNode.Clone();

						if (nestedResult.BestCandidates.Count > 0 &&
							workingTerminalCount + nestedResult.BestCandidates.Sum(n => n.Terminals.Count()) > bestCandidateTerminalCount)
						{
							var bestCandidatePosition = new Dictionary<SourcePosition, StatementDescriptionNode>();
							// Candidate nodes can be multiplied, therefore we fetch always the last node.
							foreach (var candidate in nestedResult.BestCandidates)
								bestCandidatePosition[candidate.SourcePosition] = candidate.Clone();

							alternativeNode.AddChildNodes(bestCandidatePosition.Values);
							bestCandidateNodes = new List<StatementDescriptionNode>(workingNodes) { alternativeNode };
							bestCandidatesCompatible = true;
						}

						if (nestedResult.Nodes.Count > 0 && nestedResult.Status == ProcessingStatus.Success)
						{
							nestedNode.AddChildNodes(nestedResult.Nodes);
							workingNodes.Add(nestedNode);
						}

						if (result.Status == ProcessingStatus.SequenceNotFound)
						{
							foreach (var terminalCandidate in nestedResult.TerminalCandidates)
								terminalCandidates.Add(terminalCandidate);

							if (workingNodes.Count == 0/* && workingNodes[0].FirstTerminalNode.Id == OracleGrammarDescription.Terminals.Comma*/)
								break;

							workingNodes.Add(alternativeNode);
						}
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						var terminalResult = IsTokenValid(terminalReference, level, tokenOffset, tokenBuffer);

						TryParseInvalidGrammar(tryBestCandidates, () => IsTokenValid(terminalReference, level, bestCandidateOffset, tokenBuffer), ref terminalResult, workingNodes, bestCandidateNodes);

						if (terminalResult.Status == ProcessingStatus.SequenceNotFound)
						{
							terminalCandidates.Add(terminalReference.Id);

							if (terminalReference.IsRequired)
							{
								result.Status = ProcessingStatus.SequenceNotFound;
								break;
							}

							continue;
						}

						var terminalNode = terminalResult.Nodes.Single();
						workingNodes.Add(terminalNode);
						bestCandidateNodes.Add(terminalNode);
					}
				}

				if (result.Status == ProcessingStatus.Success)
				{
					terminalCandidates.Clear();
					break;
				}
			}

			result.BestCandidates = bestCandidateNodes;
			result.TerminalCandidates = terminalCandidates;

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
			workingNodes.AddRange(bestCandidateNodes);
			processingResult = bestCandidateResult;
		}

		private void TryRevertOptionalToken(Func<int, ProcessingResult> getAlternativeProcessingResultFunction, ref ProcessingResult currentResult, IList<StatementDescriptionNode> workingNodes)
		{
			if (currentResult.Status == ProcessingStatus.Success)
				return;

			var optionalNodeCandidate = workingNodes.Count > 0 ? workingNodes[workingNodes.Count - 1].Terminals.LastOrDefault() : null;
			optionalNodeCandidate = optionalNodeCandidate != null && optionalNodeCandidate.IsRequired ? optionalNodeCandidate.ParentNode : optionalNodeCandidate;

			if (optionalNodeCandidate == null || optionalNodeCandidate.IsRequired)
				return;

			var optionalTerminalCount = optionalNodeCandidate.Terminals.Count();
			var newResult = getAlternativeProcessingResultFunction(optionalTerminalCount);

			var newResultTerminalCount = newResult.BestCandidates.Sum(n => n.Terminals.Count());
			if (newResultTerminalCount < optionalTerminalCount)
				return;

			var originalTerminalCount = currentResult.Terminals.Count();
			currentResult = newResult;
			
			var nodeReverted = newResultTerminalCount > originalTerminalCount;
			if (nodeReverted)
				RevertLastOptionalNode(workingNodes, optionalNodeCandidate.Type);
		}

		private void RevertLastOptionalNode(IList<StatementDescriptionNode> workingNodes, NodeType nodeType)
		{
			if (nodeType == NodeType.Terminal)
			{
				workingNodes[workingNodes.Count - 1].RemoveLastChildNodeIfOptional();
			}
			else
			{
				workingNodes.RemoveAt(workingNodes.Count - 1);
			}
		}

		private ProcessingResult IsTokenValid(SqlGrammarRuleSequenceTerminal terminalReference, int level, int tokenOffset, IList<OracleToken> tokenBuffer)
		{
			var tokenIsValid = false;
			var terminalNode = new StatementDescriptionNode(NodeType.Terminal)
			{
				Id = terminalReference.Id,
				Level = level,
				IsRequired = terminalReference.IsRequired
			};

			if (tokenBuffer.Count > tokenOffset)
			{
				var currentToken = tokenBuffer[tokenOffset];

				var terminal = _terminals[terminalReference.Id];
				if (!String.IsNullOrEmpty(terminal.RegexValue))
				{
					tokenIsValid = new Regex(terminal.RegexValue).IsMatch(currentToken.Value) && !_keywords.Contains(currentToken.Value.ToUpperInvariant());
				}
				else
				{
					tokenIsValid = terminal.Value == currentToken.Value.ToUpperInvariant();
					terminalNode.IsKeyword = tokenIsValid && terminal.IsKeyword;
				}

				terminalNode.Token = currentToken;
			}
			
			return new ProcessingResult
			       {
				       Status = tokenIsValid ? ProcessingStatus.Success : ProcessingStatus.SequenceNotFound,
					   Nodes = new [] { terminalNode },
					   BestCandidates = new [] { terminalNode }
			       };
		}
	}
}
