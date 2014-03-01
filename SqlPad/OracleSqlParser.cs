using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad
{
	public class OracleSqlParser
	{
		private readonly SqlGrammar _sqlGrammar;
		private readonly Dictionary<string, SqlGrammarRuleSequence[]> _startingNonTerminalSequences;
		private readonly Dictionary<string, SqlGrammarTerminal> _terminals;
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(SqlGrammar));
		private readonly List<OracleStatement> _oracleSqlCollection = new List<OracleStatement>();
		private readonly HashSet<string> _keywords;
		private readonly HashSet<string> _terminators;
		
		private readonly List<OracleToken> _tokenBuffer = new List<OracleToken>();
		private readonly List<string> _availableNonTerminals;

		public OracleSqlParser()
		{
			using (var grammarReader = XmlReader.Create("OracleSqlGrammar.xml"))
			{
				_sqlGrammar = (SqlGrammar)XmlSerializer.Deserialize(grammarReader);
			}

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

		public ICollection<OracleStatement> Parse(string sqlText)
		{
			using (var reader = new StringReader(sqlText))
			{
				var statements = Parse(OracleTokenReader.Create(reader));
				foreach (var statement in statements)
					statement.Text = sqlText.Substring(statement.SourcePosition.IndexStart, statement.SourcePosition.IndexEnd - statement.SourcePosition.IndexStart + 1);

				return statements;
			}
		}

		public ICollection<OracleStatement> Parse(OracleTokenReader tokenReader)
		{
			if (tokenReader == null)
				throw new ArgumentNullException("tokenReader");

			return Parse(tokenReader.GetTokens().Cast<OracleToken>());
		}

		public ICollection<OracleStatement> Parse(IEnumerable<OracleToken> tokens)
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

					if (result.Value != NonTerminalProcessingResult.Success)
						continue;
					
					var lastTerminal = result.Terminals.Last();
					if (!_terminators.Contains(lastTerminal.Token.Value) && _tokenBuffer.Count > result.TerminalCount)
						result.Value = NonTerminalProcessingResult.SequenceNotFound;

					break;
				}

				int indexStart;
				int indexEnd;
				if (result.Value != NonTerminalProcessingResult.Success)
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
				oracleSql.ProcessingResult = result.Value;
				_oracleSqlCollection.Add(oracleSql);
			}
			while (_tokenBuffer.Count > 0);
		}

		private ProcessingResult ProceedNonTerminal(string nonTerminal, int level, int tokenStartOffset)
		{
			var workingNodes = new List<StatementDescriptionNode>();
			var result = new ProcessingResult
			             {
				             Value = NonTerminalProcessingResult.Start,
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

						if (nestedResult.Value == NonTerminalProcessingResult.SequenceNotFound &&
							workingNodes.Count > 0 &&
							!workingNodes[workingNodes.Count - 1].Terminals.Last().IsRequired)
						{
							nestedResult = ProceedNonTerminal(nestedNonTerminal.Id, level + 1, tokenOffset - 1);
							if (nestedResult.Value == NonTerminalProcessingResult.Success)
							{
								workingNodes[workingNodes.Count - 1].RemoveLastChildNodeIfOptional();
							}
						}

						if (nestedNonTerminal.IsRequired || nestedResult.Value == NonTerminalProcessingResult.Success)
						{
							result.Value = nestedResult.Value;
						}

						if (nestedResult.Value == NonTerminalProcessingResult.Success && nestedResult.Nodes.Count > 0)
						{
							var nestedNode = new StatementDescriptionNode(NodeType.NonTerminal) { Id = nestedNonTerminal.Id, Level = level, IsRequired = nestedNonTerminal.IsRequired };
							nestedNode.AddChildNodes(nestedResult.Nodes);
							workingNodes.Add(nestedNode);
						}

						if (result.Value == NonTerminalProcessingResult.SequenceNotFound)
							break;
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						var tokenIsValid = false;
						var currentToken = OracleToken.Empty;

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

						if (!tokenIsValid)
						{
							if (!terminalReference.IsRequired)
								continue;
							
							result.Value = NonTerminalProcessingResult.SequenceNotFound;
							break;
						}

						var node = new StatementDescriptionNode(NodeType.Terminal) { Token = currentToken, Id = terminalReference.Id, Level = level, IsRequired = terminalReference.IsRequired };
						workingNodes.Add(node);

						//Trace.WriteLine(string.Format("newTokenFetched: {0}; nonTerminal: {1}; token: {2}", newTokenFetched, nonTerminal, sqlToken));

						result.Value = NonTerminalProcessingResult.Success;
					}
				}

				if (result.Value == NonTerminalProcessingResult.Success)
				{
					break;
				}
			}

			return result;
		}
	}

	[DebuggerDisplay("ProcessingResult (Value={Value}, TerminalCount={TerminalCount})")]
	public struct ProcessingResult
	{
		public NonTerminalProcessingResult Value { get; set; }
		
		public IList<StatementDescriptionNode> Nodes { get; set; }

		public IEnumerable<StatementDescriptionNode> Terminals
		{
			get
			{
				return Nodes == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: Nodes.SelectMany(t => t.Terminals);
			}
		} 

		public int TerminalCount
		{
			get { return Nodes == null ? 0 : Terminals.Count(); }
		}
	}

	public enum NonTerminalProcessingResult
	{
		Start,
		Success,
		SequenceNotFound
	}
}
