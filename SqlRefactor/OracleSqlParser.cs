using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SqlRefactor
{
	public class OracleSqlParser
	{
		private readonly SqlGrammar _sqlGrammar;
		private readonly Dictionary<string, SqlGrammarRuleSequence[]> _startingNonTerminalSequences;
		private readonly Dictionary<string, SqlGrammarTerminal> _terminals;
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(SqlGrammar));
		private readonly List<OracleSql> _oracleSqlCollection = new List<OracleSql>();
		private readonly HashSet<string> _keywords;
		
		private readonly List<OracleToken> _tokenBuffer = new List<OracleToken>();

		public OracleSqlParser()
		{
			using (var grammarReader = XmlReader.Create("OracleSqlGrammar.xml"))
			{
				_sqlGrammar = (SqlGrammar)XmlSerializer.Deserialize(grammarReader);
			}

			_startingNonTerminalSequences = _sqlGrammar.Rules.ToDictionary(r => r.StartingNonTerminal, r => r.Sequences);
			_terminals = _sqlGrammar.Terminals.ToDictionary(t => t.Id, t => t);
			_keywords = new HashSet<string>(_sqlGrammar.Terminals.Where(t => t.IsKeyword).Select(t => t.Value));
		}

		public ICollection<OracleSql> Parse(string sqlText)
		{
			using (var reader = new StringReader(sqlText))
			{
				return Parse(OracleTokenReader.Create(reader));
			}
		}

		public ICollection<OracleSql> Parse(OracleTokenReader tokenReader)
		{
			if (tokenReader == null)
				throw new ArgumentNullException("tokenReader");

			return Parse(tokenReader.GetTokens());
		}

		public ICollection<OracleSql> Parse(IEnumerable<OracleToken> tokens)
		{
			_tokenBuffer.Clear();
			_tokenBuffer.AddRange(tokens);

			ProceedGrammar();

			return _oracleSqlCollection.AsReadOnly();
		}

		private void ProceedGrammar()
		{
			var availableNonTerminals = _sqlGrammar.StartSymbols.Select(s => s.Id).ToList();

			_oracleSqlCollection.Clear();

			var result = new ProcessingResult();

			do
			{
				var oracleSql = new OracleSql();

				foreach (var nonTerminal in availableNonTerminals)
				{
					result = ProceedNonTerminal(nonTerminal, 0, 0);

					if (result.Value != NonTerminalProcessingResult.Success)
						continue;
					
					var lastTerminal = result.Terminals.Last();
					_tokenBuffer.RemoveRange(0, result.TerminalCount);
					if (lastTerminal.Value.Value != ";" && _tokenBuffer.Count > 0)
						result.Value = NonTerminalProcessingResult.SequenceNotFound;

					break;
				}

				if (result.Value != NonTerminalProcessingResult.Success)
				{
					var index = _tokenBuffer.FindIndex(t => t.Value == ";");
					if (index == -1)
					{
						_tokenBuffer.Clear();
					}
					else
					{
						_tokenBuffer.RemoveRange(0, index + 1);
					}
				}

				if (result.Tokens.Count > 0)
				{
					oracleSql.TokenCollection = result.Tokens;
					_oracleSqlCollection.Add(oracleSql);
				}

				oracleSql.ProcessingResult = result.Value;
			}
			while (_tokenBuffer.Count > 0);
		}

		private ProcessingResult ProceedNonTerminal(string nonTerminal, int level, int tokenStartOffset)
		{
			var tokens = new List<SqlDescriptionNode>();
			var result = new ProcessingResult
			             {
				             Value = NonTerminalProcessingResult.Start,
							 Tokens = tokens
			             };

			foreach (var sequence in _startingNonTerminalSequences[nonTerminal])
			{
				if (result.Value == NonTerminalProcessingResult.Success)
				{
					break;
				}

				tokens.Clear();
				var localTokenIndex = 0;

				foreach (var item in sequence.Items)
				{
					var tokenOffset = tokenStartOffset + localTokenIndex;
					var nestedNonTerminal = item as SqlGrammarRuleSequenceNonTerminal;
					if (nestedNonTerminal != null)
					{
						var nestedResult = ProceedNonTerminal(nestedNonTerminal.Id, level + 1, tokenOffset);

						if (nestedNonTerminal.IsRequired)
						{
							result.Value = nestedResult.Value;
						}

						if (nestedResult.Value == NonTerminalProcessingResult.Success && nestedResult.Tokens.Count > 0)
						{
							localTokenIndex += nestedResult.TerminalCount;

							var nestedNode = new SqlDescriptionNode { Id = nestedNonTerminal.Id, Type = SqlNodeType.NonTerminal };
							foreach (var nonTerminalNode in nestedResult.Tokens)
							{
								nestedNode.ChildTokens.Add(nonTerminalNode);
								nonTerminalNode.ParentDescriptionNode = nestedNode;
							}

							tokens.Add(nestedNode);
						}

						if (result.Value == NonTerminalProcessingResult.SequenceNotFound ||
						    result.Value == NonTerminalProcessingResult.FailureStop)
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

						var sqlToken = new SqlDescriptionNode { Value = currentToken, Id = terminalReference.Id, Type = SqlNodeType.Terminal };
						tokens.Add(sqlToken);

						//Trace.WriteLine(string.Format("newTokenFetched: {0}; nonTerminal: {1}; token: {2}", newTokenFetched, nonTerminal, sqlToken));

						localTokenIndex++;
						result.Value = NonTerminalProcessingResult.Success;
					}
				}
			}

			return result;
		}
	}

	[DebuggerDisplay("ProcessingResult (Value={Value}, TerminalCount={TerminalCount})")]
	public struct ProcessingResult
	{
		public NonTerminalProcessingResult Value { get; set; }
		public ICollection<SqlDescriptionNode> Tokens { get; set; }

		public IEnumerable<SqlDescriptionNode> Terminals
		{
			get
			{
				return Tokens == null
					? Enumerable.Empty<SqlDescriptionNode>()
					: Tokens.SelectMany(t => t.Terminals);
			}
		} 

		public int TerminalCount
		{
			get { return Tokens == null ? 0 : Tokens.Sum(t => t.TerminalCount); }
		}
	}

	public enum NonTerminalProcessingResult
	{
		Start,
		Success,
		SequenceNotFound,
		FailureStop
	}
}
