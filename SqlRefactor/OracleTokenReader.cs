using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlRefactor
{
	[DebuggerDisplay("OracleToken (Value={Value}, Index={Index})")]
	public struct OracleToken
	{
		public static OracleToken EmptyToken = new OracleToken();

		public string Value { get; internal set; }
		public int Index { get; internal set; }
	}

	public class OracleTokenReader : IDisposable
	{
		private readonly TextReader _sqlReader;
		private readonly Queue<int> _buffer = new Queue<int>();
		private readonly StringBuilder _builder = new StringBuilder();
		private readonly HashSet<char> _singleCharacterTerminals =
			new HashSet<char> { '(', ')', ',', ';', '.', '/', '+', '-', '*', ';', '@' };

		private bool _inString;
		private bool _inLineComment;
		private bool _inBlockComment;
		private bool _inQuotedIdentifier;
		private int _currentIndex;

		private OracleTokenReader(TextReader sqlReader)
		{
			if (sqlReader == null)
				throw new ArgumentNullException("sqlReader");
			
			_sqlReader = sqlReader;
		}

		public static OracleTokenReader Create(string sqlText)
		{
			return new OracleTokenReader(new StringReader(sqlText));
		}

		public static OracleTokenReader Create(TextReader sqlReader)
		{
			return new OracleTokenReader(sqlReader);
		}

		#region Implementation of IDisposable
		public void Dispose()
		{
			_sqlReader.Dispose();
		}
		#endregion

		public IEnumerable<OracleToken> GetTokens(bool includeCommentBlocks = false)
		{
			Initialize();

			var yieldToken = false;
			var inNumber = false;
			var inDecimalNumber = false;
			var inExponent = false;
			var inExponentWithOperator = false;
			string token;

			int characterCode;
			while ((characterCode = GetNextCharacterCode()) != -1)
			{
				var character = (char)characterCode;
				var quotedIdentifierOrStringLiteralEnabled = false;

				var isSpace = character == ' ' || character == '\t' || character == '\n' || character == '\r';
				if (!_inBlockComment && !_inLineComment)
				{
					if (isSpace && !_inString && !_inQuotedIdentifier)
					{
						yieldToken = true;
					}

					if (character == '"')
					{
						if (!_inQuotedIdentifier)
						{
							quotedIdentifierOrStringLiteralEnabled = true;
						}

						_inQuotedIdentifier = !_inQuotedIdentifier;
						yieldToken |= !_inQuotedIdentifier;
					}
					else if (character == '\'' && !_inQuotedIdentifier)
					{
						var nextCharacter = (char)_sqlReader.Read();
						if (nextCharacter != '\'' || !_inString)
						{
							if (!_inString &&
								!(_builder.Length == 1 && new[] { 'Q', 'N' }.Any(c => c == _builder.ToString(0, 1).ToUpperInvariant()[0]) ||
								  _builder.Length == 2 && _builder.ToString(0, 2).ToUpperInvariant() == "NQ"))
							{
								quotedIdentifierOrStringLiteralEnabled = true;
							}

							_inString = !_inString;
							yieldToken |= !_inString;
						}

						_buffer.Enqueue(nextCharacter);
					}
				}

				if (!_inString && !_inQuotedIdentifier && (_inBlockComment || _inLineComment))
				{
					var nextCharacterCode = _sqlReader.Read();
					if (nextCharacterCode == -1)
						continue;

					var nextCharacter = (char)nextCharacterCode;
					if (_inLineComment &&
						character == Environment.NewLine[0] &&
						(Environment.NewLine.Length == 1 || nextCharacter == Environment.NewLine[1]))
					{
						_inLineComment = false;
					}

					if (_inBlockComment && character == '*' && nextCharacter == '/')
					{
						_inBlockComment = false;
					}

					if (!_inLineComment && !_inBlockComment)
					{
						_currentIndex++;
						continue;
					}

					_buffer.Enqueue(nextCharacter);
				}

				if (!_inString && !_inQuotedIdentifier && !_inBlockComment && !_inLineComment && (character == '-' || character == '/'))
				{
					var nextCharacterCode = _sqlReader.Read();
					var nextCharacter = (char)nextCharacterCode;
					if (character == '-' && nextCharacter == '-' && !_inLineComment)
						_inLineComment = true;

					if (character == '/' && nextCharacter == '*' && !_inBlockComment)
						_inBlockComment = true;

					if (_inLineComment || _inBlockComment)
					{
						_currentIndex++;
						yieldToken = true;
					}
					else if (nextCharacterCode != -1)
						_buffer.Enqueue(nextCharacter);
				}

				var isSingleCharacterSeparator = _singleCharacterTerminals.Contains(character) && !_inString && !_inQuotedIdentifier && !_inLineComment && !_inBlockComment;

				if (!_inString && !_inQuotedIdentifier && !_inBlockComment && !_inLineComment)
				{
					if (characterCode >= 48 && characterCode <= 57)
					{
						if (_builder.Length == 0 || (_builder.Length == 1 && _builder[0] == '.'))
							inNumber = true;
					}
					else
					{
						var nextCharacterCode = _sqlReader.Read();
						var nextCharacter = (char)nextCharacterCode;

						if (characterCode == '.' && (inNumber || _builder.Length == 0) && !inDecimalNumber)
						{
							inDecimalNumber = true;
							isSingleCharacterSeparator = false;
						}
						else if (inNumber && !inExponent && (character == 'e' || character == 'E') &&
							(nextCharacter == '+' || nextCharacter == '-' || (nextCharacterCode >= 48 && nextCharacterCode <= 57)))
						{
							inExponent = true;
						}
						else if (inExponent && !inExponentWithOperator && (character == '+' || character == '-'))
						{
							inExponentWithOperator = true;
							isSingleCharacterSeparator = false;
						}
						else if (inNumber && !isSingleCharacterSeparator)
						{
							var previousCharacterCode = (int)_builder[_builder.Length - 1];
							if ((character != 'd' && character != 'D' && character != 'f' && character != 'F') || previousCharacterCode == 'f' || previousCharacterCode == 'F' || previousCharacterCode == 'd' || previousCharacterCode == 'D')
							{
								inNumber = false;
								quotedIdentifierOrStringLiteralEnabled = true;
							}
						}

						if (character == '.' && !inNumber && _builder.Length > 0)
						{
							if (nextCharacterCode != -1 && nextCharacterCode >= 48 && nextCharacterCode <= 57)
							{
								quotedIdentifierOrStringLiteralEnabled = true;
								isSingleCharacterSeparator = false;
							}
						}

						if (nextCharacterCode != -1)
							_buffer.Enqueue(nextCharacter);
					}

					if (character == '=')
					{
						yieldToken = true;

						if (_builder.Length == 1 && _builder[0] != '<' && _builder[0] != '>' && _builder[0] != '^')
						{
							quotedIdentifierOrStringLiteralEnabled = false;
							isSingleCharacterSeparator = true;
						}
					}
				}

				yieldToken |= isSingleCharacterSeparator;

				if (!isSingleCharacterSeparator && !_inLineComment && !_inBlockComment && !quotedIdentifierOrStringLiteralEnabled)
					_builder.Append(character);

				if (yieldToken || quotedIdentifierOrStringLiteralEnabled)
				{
					var indexOffset = _builder.Length + (quotedIdentifierOrStringLiteralEnabled || isSingleCharacterSeparator ? 1 : 0);
					if (TryNormalizeToken(out token))
						yield return new OracleToken { Value = token, Index = _currentIndex - indexOffset };

					_builder.Clear();
					
					yieldToken = false;
					inNumber = false;
					inDecimalNumber = false;
					inExponent = false;
					inExponentWithOperator = false;

					if (isSingleCharacterSeparator)
						yield return new OracleToken { Value = new String(character, 1), Index = _currentIndex - 1 };

					if (quotedIdentifierOrStringLiteralEnabled && !isSpace)
						_builder.Append(character);
				}
			}

			if (TryNormalizeToken(out token))
				yield return new OracleToken { Value = token, Index = _currentIndex - token.Length };
		}

		private void Initialize()
		{
			_builder.Clear();
			_currentIndex = 0;
			_inLineComment = false;
			_inBlockComment = false;
			_inQuotedIdentifier = false;
			_inString = false;
		}

		private int GetNextCharacterCode()
		{
			var characterCode = _buffer.Count > 0 ? _buffer.Dequeue() : _sqlReader.Read();
			if (characterCode != -1)
				_currentIndex++;

			return characterCode;
		}

		private bool TryNormalizeToken(out string token)
		{
			token = _builder.ToString().Trim();
			return !String.IsNullOrEmpty(token);
		}
	}
}