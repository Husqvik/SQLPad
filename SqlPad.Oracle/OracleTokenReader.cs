using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlPad.Oracle
{
	public class OracleTokenReader : IDisposable, ITokenReader
	{
		private readonly TextReader _sqlReader;
		private readonly Queue<int> _buffer = new Queue<int>();
		private readonly StringBuilder _builder = new StringBuilder();
		private readonly HashSet<char> _singleCharacterTerminals =
			new HashSet<char> { '(', ')', ',', ';', '.', '/', '+', '-', '*', '@', ':' };

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

		public IEnumerable<IToken> GetTokens(bool includeCommentBlocks = false)
		{
			_builder.Clear();
			_currentIndex = 0;

			var inString = false;
			var inLineComment = false;
			var inBlockComment = false;
			var inQuotedIdentifier = false;
			var inNumber = false;
			var inDecimalNumber = false;
			var inExponent = false;
			var inExponentWithOperator = false;
			var yieldToken = false;
			var currentIndexOffset = 0;
			string token;

			int characterCode;
			while ((characterCode = GetNextCharacterCode()) != -1)
			{
				_currentIndex += currentIndexOffset;
				currentIndexOffset = 0;

				var character = (char)characterCode;
				var quotedIdentifierOrLiteralEnabled = false;

				var isSpace = character == ' ' || character == '\t' || character == '\n' || character == '\r';
				if (!inBlockComment && !inLineComment)
				{
					if (isSpace && !inString && !inQuotedIdentifier)
					{
						yieldToken = true;
					}

					if (character == '"')
					{
						if (!inQuotedIdentifier && (_builder.Length != 1 || _builder[0] != ':'))
						{
							quotedIdentifierOrLiteralEnabled = true;
						}

						inQuotedIdentifier = !inQuotedIdentifier;
						yieldToken |= !inQuotedIdentifier;
					}
					else if (character == '\'' && !inQuotedIdentifier)
					{
						var nextCharacterCode = _sqlReader.Read();
						var nextCharacter = (char)nextCharacterCode;
						if (nextCharacter != '\'' || !inString)
						{
							if (!inString &&
								!(_builder.Length == 1 && new[] { 'Q', 'N' }.Any(c => c == _builder.ToString(0, 1).ToUpperInvariant()[0]) ||
								  _builder.Length == 2 && _builder.ToString(0, 2).ToUpperInvariant() == "NQ"))
							{
								quotedIdentifierOrLiteralEnabled = true;
							}

							inString = !inString;
							yieldToken |= !inString;
						}

						if (nextCharacterCode != -1)
							_buffer.Enqueue(nextCharacter);
					}
				}

				if (!inString && !inQuotedIdentifier && (inBlockComment || inLineComment))
				{
					var nextCharacterCode = _sqlReader.Read();
					if (nextCharacterCode == -1)
						continue;

					var nextCharacter = (char)nextCharacterCode;
					if (inLineComment &&
						character == Environment.NewLine[0] &&
						(Environment.NewLine.Length == 1 || nextCharacter == Environment.NewLine[1]))
					{
						inLineComment = false;
					}

					if (inBlockComment && character == '*' && nextCharacter == '/')
					{
						inBlockComment = false;
					}

					if (!inLineComment && !inBlockComment)
					{
						currentIndexOffset = 1;
						continue;
					}

					_buffer.Enqueue(nextCharacter);
				}

				if (!inString && !inQuotedIdentifier && !inBlockComment && !inLineComment && (character == '-' || character == '/'))
				{
					var nextCharacterCode = _sqlReader.Read();
					var nextCharacter = (char)nextCharacterCode;
					if (character == '-' && nextCharacter == '-' && !inLineComment)
						inLineComment = true;

					if (character == '/' && nextCharacter == '*' && !inBlockComment)
						inBlockComment = true;

					if (inLineComment || inBlockComment)
					{
						currentIndexOffset = 1;
						yieldToken = true;
					}
					else if (nextCharacterCode != -1)
						_buffer.Enqueue(nextCharacter);
				}

				var isSingleCharacterSeparator = _singleCharacterTerminals.Contains(character) && !inString && !inQuotedIdentifier && !inLineComment && !inBlockComment;

				if (!inString && !inQuotedIdentifier && !inBlockComment && !inLineComment)
				{
					if (characterCode >= 48 && characterCode <= 57)
					{
						if (_builder.Length == 0 || (_builder.Length == 1 && _builder[0] == '.'))
							inNumber = true;
					}
					else
					{
						var nextCharacterCode = GetNextCharacterCode(false);
						var nextCharacter = (char)nextCharacterCode;

						if (characterCode == '.' && (inNumber || (_builder.Length == 0 && nextCharacterCode >= 48 && nextCharacterCode <= 57)) && !inDecimalNumber)
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
							var precedingCharacterCode = (int)_builder[_builder.Length - 1];
							if ((character != 'd' && character != 'D' && character != 'f' && character != 'F') || precedingCharacterCode == 'f' || precedingCharacterCode == 'F' || precedingCharacterCode == 'd' || precedingCharacterCode == 'D')
							{
								inNumber = false;
								quotedIdentifierOrLiteralEnabled = true;
							}
						}

						if (character == '.' && !inNumber && _builder.Length > 0)
						{
							if (nextCharacterCode != -1 && nextCharacterCode >= 48 && nextCharacterCode <= 57)
							{
								quotedIdentifierOrLiteralEnabled = true;
								isSingleCharacterSeparator = false;
							}
						}

						if (nextCharacterCode != -1)
							_buffer.Enqueue(nextCharacter);
					}

					if (character == '=')
					{
						yieldToken = true;

						if (_builder.Length == 1 && _builder[0] != '<' && _builder[0] != '>' && _builder[0] != '^' && _builder[0] != '!')
						{
							quotedIdentifierOrLiteralEnabled = false;
							isSingleCharacterSeparator = true;
						}
					}
				}

				yieldToken |= isSingleCharacterSeparator;

				if (!isSingleCharacterSeparator && !inLineComment && !inBlockComment && !quotedIdentifierOrLiteralEnabled)
					_builder.Append(character);

				if (yieldToken || quotedIdentifierOrLiteralEnabled)
				{
					var indexOffset = _builder.Length + (quotedIdentifierOrLiteralEnabled || isSingleCharacterSeparator || inLineComment || inBlockComment ? 1 : 0);
					if (TryNormalizeToken(out token))
						yield return new OracleToken(token, _currentIndex - indexOffset);

					_builder.Clear();
					
					yieldToken = false;
					inNumber = false;
					inDecimalNumber = false;
					inExponent = false;
					inExponentWithOperator = false;

					if (isSingleCharacterSeparator)
						yield return new OracleToken(new String(character, 1), _currentIndex - 1);

					if (quotedIdentifierOrLiteralEnabled && !isSpace)
						_builder.Append(character);
				}
			}

			if (TryNormalizeToken(out token))
				yield return new OracleToken(token, _currentIndex - token.Length);

			//Trace.WriteLine(null);
		}

		private int GetNextCharacterCode(bool incrementIndex = true)
		{
			var characterCode = _buffer.Count > 0 ? _buffer.Dequeue() : _sqlReader.Read();
			if (characterCode != -1 && incrementIndex)
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