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
		private IEnumerator<OracleToken> _tokenEnumerator;
		
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

			/*var hashSet = new HashSet<string>();
			foreach (var x in _sqlGrammar.Terminals)
			{
				if (hashSet.Contains(x.Id))
				{
					
				}

				hashSet.Add(x.Id);
			}*/
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

			_tokenEnumerator = tokenReader.GetTokens().GetEnumerator();

			return Parse();
		}

		public ICollection<OracleSql> Parse(IEnumerable<OracleToken> tokens)
		{
			_tokenEnumerator = tokens.GetEnumerator();

			return Parse();
		}

		private ICollection<OracleSql> Parse()
		{
			_tokenBuffer.Clear();
			
			/*var tokenEnumerator = GetTokens();
			while (tokenEnumerator.MoveNext())
			{
				Trace.WriteLine(tokenEnumerator.Current);
			}*/

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
					
					if (result.Value == NonTerminalProcessingResult.TerminatorDetected ||
						result.Value == NonTerminalProcessingResult.Success ||
						result.Value == NonTerminalProcessingResult.InputEnd)
						break;
				}

				if (result.Value != NonTerminalProcessingResult.TerminatorDetected)
				{
					bool isTokenAvailable;
					while (isTokenAvailable = _tokenEnumerator.MoveNext())
						if (_tokenEnumerator.Current.Value == ";")
							break;

					if (!isTokenAvailable)
						result.Value = NonTerminalProcessingResult.InputEnd;
				}

				if (result.Tokens.Count > 0)
				{
					oracleSql.TokenCollection = result.Tokens;
					_oracleSqlCollection.Add(oracleSql);
				}

				oracleSql.ProcessingResult = result.Value;
			}
			while (result.Value != NonTerminalProcessingResult.InputEnd);
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
				if (result.Value == NonTerminalProcessingResult.Success ||
					result.Value == NonTerminalProcessingResult.TerminatorDetected ||
					result.Value == NonTerminalProcessingResult.InputEnd)
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

						if (result.Value == NonTerminalProcessingResult.SequenceNotFound ||
							result.Value == NonTerminalProcessingResult.FailureStop ||
							result.Value == NonTerminalProcessingResult.InputEnd)
							break;

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
					}
					else
					{
						var terminalReference = (SqlGrammarRuleSequenceTerminal)item;

						bool tokenIsValid;
						OracleToken currentToken;
						var newTokenFetched = false;

						if (_tokenBuffer.Count > tokenOffset || (newTokenFetched = _tokenEnumerator.MoveNext()))
						{
							if (newTokenFetched)
							{
								_tokenBuffer.Add(_tokenEnumerator.Current);
							}

							currentToken = _tokenBuffer[tokenOffset];

							var terminal = _terminals[terminalReference.Id];
							if (!String.IsNullOrEmpty(terminal.RegexValue))
							{
								tokenIsValid = new Regex(terminal.RegexValue).IsMatch(currentToken.Value) && !_keywords.Contains(currentToken.Value.ToUpperInvariant());
							}
							else
							{
								tokenIsValid = terminal.Value == currentToken.Value.ToUpperInvariant();

								if (tokenIsValid && terminal.Value == ";")
								{
									result.Value = NonTerminalProcessingResult.TerminatorDetected;
									break;
								}
							}
						}
						else
						{
							result.Value = NonTerminalProcessingResult.InputEnd;
							break;
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

					if (result.Value == NonTerminalProcessingResult.FailureStop ||
						result.Value == NonTerminalProcessingResult.InputEnd ||
						result.Value == NonTerminalProcessingResult.TerminatorDetected ||
						result.Value == NonTerminalProcessingResult.InputEnd)
						break;
				}
			}

			return result;
		}
	}

	public struct ProcessingResult
	{
		public NonTerminalProcessingResult Value { get; set; }
		public ICollection<SqlDescriptionNode> Tokens { get; set; }

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
		FailureStop,
		InputEnd,
		TerminatorDetected
	}
}
