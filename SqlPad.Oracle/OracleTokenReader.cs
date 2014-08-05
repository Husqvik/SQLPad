using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace SqlPad.Oracle
{
	public class OracleTokenReader : IDisposable, ITokenReader
	{
		private readonly TextReader _sqlReader;

		private readonly HashSet<char> _singleCharacterTerminalsNew =
			new HashSet<char> { '(', ')', ',', ';', '+', '@', ':' };

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

		IEnumerable<IToken> ITokenReader.GetTokens(bool includeCommentBlocks)
		{
			return (IEnumerable<IToken>)GetTokens(includeCommentBlocks);
		}

		public IEnumerable<OracleToken> GetTokens(bool includeCommentBlocks = false)
		{
			var builder = new StringBuilder();

			var index = 0;

			var inQuotedString = false;
			char? candidateCharacter = null;
			char? quotedInitializer = null;

			var specialMode = new SpecialModeFlags();
			var flags = new RecognizedFlags();

			int characterCode;
			while ((characterCode = _sqlReader.Read()) != -1)
			{
				index++;
				var character = (char)characterCode;
				var isNumericCharacter = characterCode >= 48 && characterCode <= 57;
				var isNumericPostfix = character == 'f' || character == 'F' || character == 'd' || character == 'D';
				var isSign = character == '+' || character == '-';

				var previousFlags = flags;
				FindFlags(specialMode, character, out flags);

				var isBlank = flags.IsSpace || flags.IsLineTerminator;
				var isSingleCharacterTerminal = _singleCharacterTerminalsNew.Contains(character);
				var characterYielded = false;

				if (flags.QuotedStringCandidate && (builder.Length > 1 || (builder.Length == 1 && character != 'q' && character != 'Q' && previousFlags.Character != 'n' && previousFlags.Character != 'N')))
				{
					flags.QuotedStringCandidate = false;
				}

				if (inQuotedString && quotedInitializer == null)
				{
					quotedInitializer = character;
				}

				if (previousFlags.OptionalParameterCandidate)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2, null);
					}

					builder.Append(previousFlags.Character);
					var indexOffset = 1;
					if (character == '>')
					{
						builder.Append(character);
						indexOffset = 0;
						characterYielded = true;
						flags.RelationalOperatorCandidate = false;
					}

					yield return BuildToken(builder, index - indexOffset, null);

					candidateCharacter = null;
				}

				if (specialMode.InString)
				{
					var isSimpleStringTerminator = previousFlags.StringEndCandidate && character != '\'' && !inQuotedString;
					var isQuotedStringTerminator = previousFlags.Character == quotedInitializer && character == '\'' && inQuotedString;

					if (isSimpleStringTerminator || isQuotedStringTerminator)
					{
						AppendCandidateCharacter(builder, ref candidateCharacter);

						var indexOffset = isQuotedStringTerminator ? 0 : 1;
						var addedCharacter = isQuotedStringTerminator ? (char?)character : null;
						yield return BuildToken(builder, index - indexOffset, addedCharacter);

						specialMode.InString = false;
						inQuotedString = false;
						quotedInitializer = null;
						characterYielded = isQuotedStringTerminator;
					}
					else if (previousFlags.StringEndCandidate && flags.StringEndCandidate && character == '\'')
					{
						flags.StringEndCandidate = false;
					}
				}
				else if (!specialMode.IsEnabled && character == '\'')
				{
					AppendCandidateCharacter(builder, ref candidateCharacter);

					if (builder.Length > 0 && !previousFlags.QuotedStringCandidate)
					{
						yield return BuildToken(builder, index - 1, null);
					}

					inQuotedString = previousFlags.QuotedStringCandidate;

					specialMode.InString = true;
				}

				if (previousFlags.BlockCommentBeginCandidate && !specialMode.IsEnabled)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2, null);
					}

					if (character == '*')
					{
						specialMode.InBlockComment = true;
						AppendCandidateCharacter(builder, ref candidateCharacter);
					}
					else
					{
						yield return BuildToken(candidateCharacter.Value, index - 1);
					}

					candidateCharacter = null;

					flags.BlockCommentEndCandidate = false;
				}

				if (previousFlags.LineCommentCandidate && !specialMode.IsEnabled)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2, null);
					}

					AppendCandidateCharacter(builder, ref candidateCharacter);

					if (character == '-')
					{
						specialMode.InLineComment = true;
					}
					else
					{
						yield return BuildToken(builder, index - 1, null);
					}
				}

				if (!specialMode.IsEnabled && specialMode.InNumber && !specialMode.InExponent && previousFlags.ExponentCandidate)
				{
					if (isNumericCharacter || isSign)
					{
						specialMode.InExponent = true;

						isSingleCharacterTerminal = false;
						flags.LineCommentCandidate = false;
					}
					else
					{
						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 2, null);
						}

						specialMode.InNumber = false;
					}

					AppendCandidateCharacter(builder, ref candidateCharacter);
				}
				else if (!specialMode.IsEnabled && specialMode.InNumber && (specialMode.InExponent || !flags.ExponentCandidate) && ((flags.IsDecimalCandidate && specialMode.InDecimalNumber) || !isNumericCharacter))
				{
					if (flags.IsDecimalCandidate && !specialMode.InDecimalNumber)
					{
						specialMode.InDecimalNumber = true;
						flags.IsDecimalCandidate = false;
					}
					else if (specialMode.InPostfixedNumber || !isNumericPostfix)
					{
						AppendCandidateCharacter(builder, ref candidateCharacter);

						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 1, null);
						}

						specialMode.InNumber = false;
					}
					else if (!specialMode.InPostfixedNumber && isNumericPostfix)
					{
						specialMode.InPostfixedNumber = true;
					}
				}

				if (!specialMode.IsEnabled && previousFlags.IsDecimalCandidate)
				{
					if (specialMode.InNumber)
					{
						if (specialMode.InDecimalNumber)
						{
							if (builder.Length > 0)
							{
								yield return BuildToken(builder, index - 2, null);
							}

							yield return BuildToken(previousFlags.Character, index - 1);
							specialMode.InNumber = false;
						}
						else
						{
							AppendCandidateCharacter(builder, ref candidateCharacter);
							specialMode.InDecimalNumber = true;
						}
					}
					else if (isNumericCharacter)
					{
						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 2, null);
						}

						AppendCandidateCharacter(builder, ref candidateCharacter);
						specialMode.InNumber = true;
					}
					else
					{
						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 2, null);
						}

						yield return BuildToken(previousFlags.Character, index - 1);
						specialMode.InNumber = false;
					}

					candidateCharacter = null;
				}

				if (specialMode.InQuotedIdentifier && character == '"')
				{
					AppendCandidateCharacter(builder, ref candidateCharacter);

					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index, character);
					}

					characterYielded = true;
					specialMode.InQuotedIdentifier = false;
				}
				else if (!specialMode.IsEnabled && character == '"')
				{
					AppendCandidateCharacter(builder, ref candidateCharacter);

					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 1, null);
					}

					specialMode.InQuotedIdentifier = true;
				}

				if (!specialMode.IsEnabled && (isBlank || isSingleCharacterTerminal || character == '*'))
				{
					specialMode.InNumber = false;

					AppendCandidateCharacter(builder, ref candidateCharacter);
					
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 1, null);
					}

					if (!isBlank)
					{
						characterYielded = true;
						yield return BuildToken(character, index);
					}

					previousFlags.Reset();
				}
				
				if (flags.IsLineTerminator && specialMode.InLineComment)
				{
					if (includeCommentBlocks)
					{
						yield return BuildToken(builder, index, character, true);
					}
					else
					{
						builder.Clear();
					}

					characterYielded = true;
					specialMode.InLineComment = false;
					candidateCharacter = null;
				}

				if (previousFlags.BlockCommentEndCandidate && specialMode.InBlockComment)
				{
					if (character == '/')
					{
						specialMode.InBlockComment = false;

						if (includeCommentBlocks)
						{
							yield return BuildToken(builder, index, character, true);
						}
						else
						{
							builder.Clear();
						}

						characterYielded = true;
					}

					candidateCharacter = null;
					flags.BlockCommentBeginCandidate = false;
				}

				if (previousFlags.RelationalOperatorCandidate)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2, null);
					}

					if (character == '=' || (character == '>' && previousFlags.Character == '<'))
					{
						builder.Append(previousFlags.Character);
						yield return BuildToken(builder, index, character);
						characterYielded = true;
					}
					else
					{
						yield return BuildToken(previousFlags.Character, index - 1);
					}

					candidateCharacter = null;

					flags.RelationalOperatorCandidate = false;
					flags.OptionalParameterCandidate = false;
				}

				if (previousFlags.ConcatenationCandidate)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2, null);
					}

					builder.Append(previousFlags.Character);
					var indexOffset = 1;
					if (candidateCharacter == '|')
					{
						builder.Append(candidateCharacter.Value);
						indexOffset = 0;
						characterYielded = true;
					}

					yield return BuildToken(builder, index - indexOffset, null);

					if (characterYielded)
					{
						flags.ConcatenationCandidate = false;
					}

					candidateCharacter = null;
				}

				var candidateMode = flags.ConcatenationCandidate || flags.OptionalParameterCandidate || flags.LineCommentCandidate || flags.BlockCommentBeginCandidate ||
				                    flags.RelationalOperatorCandidate || flags.IsDecimalCandidate || flags.ExponentCandidate;

				if (!characterYielded)
				{
					if (!specialMode.IsEnabled && !specialMode.InNumber && isNumericCharacter && builder.Length == 0 && !candidateCharacter.HasValue)
					{
						specialMode.InNumber = true;
					}

					if (candidateMode && !specialMode.IsEnabled)
					{
						candidateCharacter = character;
					}
					else if (!isBlank || specialMode.IsEnabled)
					{
						builder.Append(character);
					}
				}
			}

			if (!specialMode.InComment || includeCommentBlocks)
			{
				var indexOffset = candidateCharacter.HasValue ? 1 : 0;
				if (builder.Length > 0)
				{
					yield return new OracleToken(builder.ToString(), index - builder.Length - indexOffset, specialMode.InComment);
				}

				if (candidateCharacter.HasValue)
				{
					yield return new OracleToken(new String(candidateCharacter.Value, 1), index - indexOffset, specialMode.InComment);
				}
			}
		}

		private static void AppendCandidateCharacter(StringBuilder builder, ref char? candidateCharacter)
		{
			if (!candidateCharacter.HasValue)
				return;

			builder.Append(candidateCharacter.Value);
			candidateCharacter = null;
		}

		private static OracleToken BuildToken(char character, int index)
		{
			return new OracleToken(character.ToString(CultureInfo.InvariantCulture), index - 1);	
		}

		private static OracleToken BuildToken(StringBuilder builder, int index, char? currentCharacter, bool isComment = false)
		{
			if (currentCharacter.HasValue)
			{
				builder.Append(currentCharacter);
			}

			var token = new OracleToken(builder.ToString(), index - builder.Length, isComment);

			builder.Clear();
			
			return token;
		}

		private static void FindFlags(SpecialModeFlags specialMode, char character, out RecognizedFlags flags)
		{
			flags = new RecognizedFlags { Character = character };

			switch (character)
			{
				case '/':
					flags.BlockCommentBeginCandidate = !specialMode.InComment;
					break;
				case '*':
					flags.BlockCommentEndCandidate = specialMode.InBlockComment;
					break;
				case '-':
					flags.LineCommentCandidate = !specialMode.InComment;
					break;
				case '|':
					flags.ConcatenationCandidate = !specialMode.IsEnabled;
					break;
				case '=':
					flags.OptionalParameterCandidate = !specialMode.IsEnabled;
					break;
				case '>':
				case '<':
				case '^':
				case '!':
					flags.RelationalOperatorCandidate = !specialMode.IsEnabled;
					break;
				case 'e':
				case 'E':
					flags.ExponentCandidate = specialMode.InNumber && !specialMode.IsEnabled;
					break;
				case 'n':
				case 'N':
				case 'q':
				case 'Q':
					flags.QuotedStringCandidate = !specialMode.InString;
					break;
				case '\'':
					flags.StringEndCandidate = specialMode.InString;
					break;
				case '.':
					flags.IsDecimalCandidate = !specialMode.IsEnabled;
					break;
				case ' ':
				case '\t':
					flags.IsSpace = true;
					break;
				case '\r':
				case '\n':
					flags.IsLineTerminator = true;//!specialMode.InBlockComment && !specialMode.InString && !specialMode.InQuotedIdentifier;
					break;
			}
		}

		[DebuggerDisplay("RecognizedFlags (IsSpace={IsSpace}; IsLineTerminator={IsLineTerminator}; LineCommentCandidate={LineCommentCandidate}; BlockCommentBeginCandidate={BlockCommentBeginCandidate}; BlockCommentEndCandidate={BlockCommentEndCandidate}; ConcatenationCandidate={ConcatenationCandidate}; OptionalParameterCandidate={OptionalParameterCandidate}; RelationalOperatorCandidate={RelationalOperatorCandidate})")]
		private class RecognizedFlags
		{
			public char Character;

			public bool IsSpace;
			public bool IsLineTerminator;
			public bool IsDecimalCandidate;
			public bool LineCommentCandidate;
			public bool BlockCommentBeginCandidate;
			public bool BlockCommentEndCandidate;
			public bool ConcatenationCandidate;
			public bool OptionalParameterCandidate;
			public bool RelationalOperatorCandidate;
			public bool QuotedStringCandidate;
			public bool StringEndCandidate;
			public bool ExponentCandidate;

			public void Reset()
			{
				IsSpace = false;
				IsLineTerminator = false;
				IsDecimalCandidate = false;
				LineCommentCandidate = false;
				BlockCommentBeginCandidate = false;
				BlockCommentEndCandidate = false;
				ConcatenationCandidate = false;
				OptionalParameterCandidate = false;
				RelationalOperatorCandidate = false;
				QuotedStringCandidate = false;
				StringEndCandidate = false;
				ExponentCandidate = false;
			}
		}

		[DebuggerDisplay("SpecialModeFlags (InString={InString}; InBlockComment={InBlockComment}; InLineComment={InLineComment}; InQuotedIdentifier={InQuotedIdentifier}")]
		private struct SpecialModeFlags
		{
			private bool _inNumber;

			public bool InString;
			public bool InBlockComment;
			public bool InLineComment;
			public bool InQuotedIdentifier;

			public bool InPostfixedNumber;
			public bool InDecimalNumber;
			public bool InExponent;

			public bool InComment
			{
				get { return InLineComment || InBlockComment; }
			}

			public bool IsEnabled
			{
				get { return InString || InBlockComment || InLineComment || InQuotedIdentifier; }
			}

			public bool InNumber
			{
				get { return _inNumber; }
				set
				{
					_inNumber = value;

					if (value)
						return;

					InPostfixedNumber = false;
					InDecimalNumber = false;
					InExponent = false;
				}
			}
		}
	}
}
