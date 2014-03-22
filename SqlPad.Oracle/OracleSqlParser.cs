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
		
		private readonly List<OracleToken> _tokenBuffer = new List<OracleToken>();
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
			_tokenBuffer.Clear();
			_tokenBuffer.AddRange(tokens);

			ProceedGrammar();

			return _oracleSqlCollection.AsReadOnly();
		}

		private void ProceedGrammar()
		{
			_oracleSqlCollection.Clear();

			if (_tokenBuffer.Count == 0)
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
					result = ProceedNonTerminal(nonTerminal, 0, 0);

					if (result.Status != ProcessingStatus.Success)
						continue;
					
					var lastTerminal = result.Terminals.Last();
					if (!_terminators.Contains(lastTerminal.Token.Value) && _tokenBuffer.Count > result.TerminalCount)
						result.Status = ProcessingStatus.SequenceNotFound;

					break;
				}

				int indexStart;
				int indexEnd;
				if (result.Status != ProcessingStatus.Success)
				{
					indexStart = _tokenBuffer.First().Index;

					var index = _tokenBuffer.FindIndex(t => _terminators.Contains(t.Value));
					if (index == -1)
					{
						var lastToken = _tokenBuffer[_tokenBuffer.Count - 1];
						indexEnd = lastToken.Index + lastToken.Value.Length - 1;
						_tokenBuffer.Clear();
					}
					else
					{
						indexEnd = _tokenBuffer[index].Index;
						_tokenBuffer.RemoveRange(0, index + 1);
					}
				}
				else
				{
					var lastTerminal = result.Terminals.Last().Token;
					indexStart = result.Terminals.First().Token.Index;
					indexEnd = lastTerminal.Index + lastTerminal.Value.Length - 1;

					_tokenBuffer.RemoveRange(0, result.TerminalCount);
				}

				oracleSql.SourcePosition = new SourcePosition { IndexStart = indexStart, IndexEnd = indexEnd };
				oracleSql.NodeCollection = result.Nodes;
				oracleSql.ProcessingStatus = result.Status;
				_oracleSqlCollection.Add(oracleSql);
			}
			while (_tokenBuffer.Count > 0);
		}

		private ProcessingResult ProceedNonTerminal(string nonTerminal, int level, int tokenStartOffset)
		{
			var workingNodes = new List<StatementDescriptionNode>();
			var result = new ProcessingResult
			             {
				             Status = ProcessingStatus.Start,
							 Nodes = workingNodes
			             };

			foreach (var sequence in _startingNonTerminalSequences[nonTerminal])
			{
				workingNodes.Clear();

				foreach (var item in sequence.Items)
				{
					var tokenOffset = tokenStartOffset + workingNodes.SelectMany(t => t.Terminals).Count();
					var nestedNonTerminal = item as SqlGrammarRuleSequenceNonTerminal;
					if (nestedNonTerminal != null)
					{
						var nestedResult = ProceedNonTerminal(nestedNonTerminal.Id, level + 1, tokenOffset);

						if (nestedResult.Status == ProcessingStatus.SequenceNotFound)
						{
							TryRevertOptionalToken(optionalTerminalCount => ProceedNonTerminal(nestedNonTerminal.Id, level + 1, tokenOffset - optionalTerminalCount), ref nestedResult, workingNodes);
						}

						if (nestedNonTerminal.IsRequired || nestedResult.Status == ProcessingStatus.Success)
						{
							result.Status = nestedResult.Status;
						}

						if (nestedResult.Status == ProcessingStatus.Success && nestedResult.Nodes.Count > 0)
						{
							var nestedNode = new StatementDescriptionNode(NodeType.NonTerminal) { Id = nestedNonTerminal.Id, Level = level, IsRequired = nestedNonTerminal.IsRequired };
							nestedNode.AddChildNodes(nestedResult.Nodes);
							workingNodes.Add(nestedNode);
						}

						if (result.Status == ProcessingStatus.SequenceNotFound)
							break;
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						var terminalResult = IsTokenValid(terminalReference, level, tokenOffset);

						if (terminalResult.Status == ProcessingStatus.SequenceNotFound)
						{
							if (!terminalReference.IsRequired)
								continue;

							TryRevertOptionalToken(optionalTerminalCount => IsTokenValid(terminalReference, level, tokenOffset - optionalTerminalCount), ref terminalResult, workingNodes);
						}

						result.Status = terminalResult.Status;

						if (terminalResult.Status == ProcessingStatus.SequenceNotFound)
							break;

						workingNodes.AddRange(terminalResult.Nodes);

						//Trace.WriteLine(string.Format("newTokenFetched: {0}; nonTerminal: {1}; token: {2}", newTokenFetched, nonTerminal, sqlToken));
					}
				}

				if (result.Status == ProcessingStatus.Success)
					break;
			}

			return result;
		}

		private void TryRevertOptionalToken(Func<int, ProcessingResult> getAlternativeProcessingResultFunction, ref ProcessingResult currentResult, IList<StatementDescriptionNode> workingNodes)
		{
			var optionalNodeCandidate = workingNodes.Count > 0 ? workingNodes[workingNodes.Count - 1].Terminals.Last() : null;
			optionalNodeCandidate = optionalNodeCandidate != null && optionalNodeCandidate.IsRequired ? optionalNodeCandidate.ParentNode : optionalNodeCandidate;

			if (optionalNodeCandidate == null || optionalNodeCandidate.IsRequired)
				return;

			var optionalTerminalCount = optionalNodeCandidate.Terminals.Count();
			var newResult = getAlternativeProcessingResultFunction(optionalTerminalCount);

			if (newResult.Status != ProcessingStatus.Success || newResult.TerminalCount < optionalTerminalCount)
				return;

			currentResult = newResult;
			
			if (optionalNodeCandidate.Type == NodeType.Terminal)
			{
				workingNodes[workingNodes.Count - 1].RemoveLastChildNodeIfOptional();
			}
			else
			{
				workingNodes.RemoveAt(workingNodes.Count - 1);
			}
		}

		private ProcessingResult IsTokenValid(SqlGrammarRuleSequenceTerminal terminalReference, int level, int tokenOffset)
		{
			var tokenIsValid = false;
			OracleToken currentToken;

			if (_tokenBuffer.Count > tokenOffset)
			{
				currentToken = _tokenBuffer[tokenOffset];

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

			return new ProcessingResult
			       {
				       Status = tokenIsValid ? ProcessingStatus.Success : ProcessingStatus.SequenceNotFound,
					   Nodes = new []{ new StatementDescriptionNode(NodeType.Terminal) { Token = currentToken, Id = terminalReference.Id, Level = level, IsRequired = terminalReference.IsRequired } }
			       };
		}
	}
}
