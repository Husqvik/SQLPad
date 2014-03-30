using System;
using System.Collections.Generic;
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
			var result = ProceedNonTerminal(nonTerminalId, 0, 0, new List<OracleToken>(tokens));
			return result.Status == ProcessingStatus.Success &&
			       result.Terminals.Count() == result.BestCandidates.Sum(n => n.Terminals.Count());
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
					result = ProceedNonTerminal(nonTerminal, 0, 0, tokenBuffer);

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
				}

				oracleSql.SourcePosition = new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
				oracleSql.NodeCollection = result.Nodes;
				oracleSql.ProcessingStatus = result.Status;
				_oracleSqlCollection.Add(oracleSql);
			}
			while (tokenBuffer.Count > 0);
		}

		private ProcessingResult ProceedNonTerminal(string nonTerminal, int level, int tokenStartOffset, IList<OracleToken> tokenBuffer)
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

				foreach (var item in sequence.Items)
				{
					var tokenOffset = tokenStartOffset + workingNodes.SelectMany(t => t.Terminals).Count();
					var nestedNonTerminal = item as SqlGrammarRuleSequenceNonTerminal;
					if (nestedNonTerminal != null)
					{
						var nestedResult = ProceedNonTerminal(nestedNonTerminal.Id, level + 1, tokenOffset, tokenBuffer);

						var nodeReverted = false;
						if (nestedResult.Status == ProcessingStatus.SequenceNotFound)
						{
							nodeReverted = TryRevertOptionalToken(optionalTerminalCount => ProceedNonTerminal(nestedNonTerminal.Id, level + 1, tokenOffset - optionalTerminalCount, tokenBuffer), ref nestedResult, workingNodes);
						}

						if (nestedNonTerminal.IsRequired || nestedResult.Status == ProcessingStatus.Success || nodeReverted)
						{
							result.Status = nestedResult.Status;
						}

						var nestedNode = new StatementDescriptionNode(NodeType.NonTerminal) { Id = nestedNonTerminal.Id, Level = level, IsRequired = nestedNonTerminal.IsRequired };
						var alternativeNode = nestedNode.Clone();

						if (nestedResult.BestCandidates.Count > 0 &&
							workingNodes.Sum(n => n.Terminals.Count()) + nestedResult.BestCandidates.Sum(n => n.Terminals.Count()) > bestCandidateNodes.Sum(n => n.Terminals.Count()))
						{
							var bestCandidatePosition = new Dictionary<SourcePosition, StatementDescriptionNode>();
							// Candidate nodes can be multiplied, therefore we fetch always the last node.
							foreach (var candidate in nestedResult.BestCandidates)
								bestCandidatePosition[candidate.SourcePosition] = candidate.Clone();

							alternativeNode.AddChildNodes(bestCandidatePosition.Values);
							bestCandidateNodes = new List<StatementDescriptionNode>(workingNodes) { alternativeNode };
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

							break;
						}
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						var terminalResult = IsTokenValid(terminalReference, level, tokenOffset, tokenBuffer);

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

						workingNodes.AddRange(terminalResult.Nodes);
						bestCandidateNodes.AddRange(terminalResult.Nodes);
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

		private bool TryRevertOptionalToken(Func<int, ProcessingResult> getAlternativeProcessingResultFunction, ref ProcessingResult currentResult, IList<StatementDescriptionNode> workingNodes)
		{
			var optionalNodeCandidate = workingNodes.Count > 0 ? workingNodes[workingNodes.Count - 1].Terminals.Last() : null;
			optionalNodeCandidate = optionalNodeCandidate != null && optionalNodeCandidate.IsRequired ? optionalNodeCandidate.ParentNode : optionalNodeCandidate;

			if (optionalNodeCandidate == null || optionalNodeCandidate.IsRequired)
				return false;

			var optionalTerminalCount = optionalNodeCandidate.Terminals.Count();
			var newResult = getAlternativeProcessingResultFunction(optionalTerminalCount);

			var newResultTerminalCount = newResult.BestCandidates.Sum(n => n.Terminals.Count());
			if (newResultTerminalCount < optionalTerminalCount)
				return false;

			var originalTerminalCount = currentResult.Terminals.Count();
			currentResult = newResult;
			
			var nodeReverted = newResultTerminalCount > originalTerminalCount;
			if (nodeReverted)
				RevertLastOptionalNode(workingNodes, optionalNodeCandidate.Type);

			return nodeReverted;
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
			OracleToken currentToken;

			if (tokenBuffer.Count > tokenOffset)
			{
				currentToken = tokenBuffer[tokenOffset];

				var terminal = _terminals[terminalReference.Id];
				if (!String.IsNullOrEmpty(terminal.RegexValue))
				{
					tokenIsValid = new Regex(terminal.RegexValue).IsMatch(currentToken.Value) && !_keywords.Contains(currentToken.Value.ToUpperInvariant());
				}
				else
				{
					tokenIsValid = terminal.Value == currentToken.Value.ToUpperInvariant();
				}
			}
			else
			{
				currentToken = OracleToken.Empty;
			}

			var terminalNode = new StatementDescriptionNode(NodeType.Terminal) { Token = currentToken, Id = terminalReference.Id, Level = level, IsRequired = terminalReference.IsRequired };
			return new ProcessingResult
			       {
				       Status = tokenIsValid ? ProcessingStatus.Success : ProcessingStatus.SequenceNotFound,
					   Nodes = new []{ terminalNode },
					   BestCandidates = new []{ terminalNode }
			       };
		}
	}
}
