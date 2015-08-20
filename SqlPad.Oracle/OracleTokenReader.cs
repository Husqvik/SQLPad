using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlPad.Oracle
{
	public class OracleTokenReader : IDisposable, ITokenReader
	{
		private const int EmptyCharacterCode = -1;
		private const char SingleQuoteCharacter = '\'';
		private const char AsteriskCharacter = '*';

		private readonly TextReader _sqlReader;

		private OracleTokenReader(TextReader sqlReader)
		{
			if (sqlReader == null)
				throw new ArgumentNullException(nameof(sqlReader));
			
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
			return GetTokens(includeCommentBlocks).Cast<IToken>();
		}

		public IEnumerable<OracleToken> GetTokens(bool includeCommentBlocks = false)
		{
			var builder = new StringBuilder();

			var index = 0;

			var inQuotedString = false;
			var sqlPlusTerminatorCandidate = false;
			var candidateCharacterCode = EmptyCharacterCode;
			char? quotingInitializer = null;
			var quotingInitializerIndex = 0;

			var specialMode = new SpecialMode();
			var flags = new RecognizedFlags();
			var previousFlags = new RecognizedFlags();

			int characterCode;
			while ((characterCode = _sqlReader.Read()) != EmptyCharacterCode)
			{
				index++;
				var character = (char)characterCode;

				flags.CopyToPrevious(previousFlags);
				FindFlags(specialMode, character, flags);

				var isBlank = flags.IsSpace || flags.IsLineTerminator;
				var isSingleCharacterTerminal = character == ',' || character == '(' || character == ')' || character == '+' || character == ';' || character == '@' || character == '[' || character == ']' || character == '{' || character == '}' || character == '%' || character == '?';
				var characterYielded = false;
				
				if (flags.BlockCommentBeginCandidate && previousFlags.IsLineTerminator)
				{
					sqlPlusTerminatorCandidate = true;
				}
				else
				{
					if (sqlPlusTerminatorCandidate && flags.IsLineTerminator)
					{
						builder.Append("\n/\n");
						candidateCharacterCode = EmptyCharacterCode;
						yield return BuildToken(builder, index);
						previousFlags.BlockCommentBeginCandidate = false;
					}

					sqlPlusTerminatorCandidate = false;
				}

				if ((flags.QuotedStringCandidate || flags.UnicodeCandidate) &&
				    builder.Length > 0 && character != 'q' && character != 'Q' && previousFlags.Character != 'n' && previousFlags.Character != 'N')
				{
					flags.QuotedStringCandidate = false;
					flags.UnicodeCandidate = false;
				}

				if (inQuotedString && quotingInitializer == null)
				{
					quotingInitializer = character;
					quotingInitializerIndex = index;
				}

				if (previousFlags.OptionalParameterCandidate)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2);
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

					yield return BuildToken(builder, index - indexOffset);

					candidateCharacterCode = EmptyCharacterCode;
				}

				if ((specialMode.Flags & SpecialModeFlags.InString) != 0)
				{
					var isSimpleStringTerminator = !inQuotedString && previousFlags.StringEndCandidate && character != SingleQuoteCharacter;
					var isQuotedStringTerminator = inQuotedString && character == SingleQuoteCharacter && index > quotingInitializerIndex + 1 && IsQuotedStringClosingCharacter(quotingInitializer.Value, previousFlags.Character);

					if (isSimpleStringTerminator || isQuotedStringTerminator)
					{
						AppendCandidateCharacter(builder, ref candidateCharacterCode);

						var indexOffset = isQuotedStringTerminator ? 0 : 1;
						if (isQuotedStringTerminator)
						{
							builder.Append(character);
						}

						yield return BuildToken(builder, index - indexOffset);

						specialMode.Flags &= ~SpecialModeFlags.InString;
						inQuotedString = false;
						quotingInitializer = null;
						characterYielded = isQuotedStringTerminator;

						FindFlags(specialMode, character, flags);
					}
					else if (previousFlags.StringEndCandidate && flags.StringEndCandidate && character == SingleQuoteCharacter)
					{
						flags.StringEndCandidate = false;
					}
				}
				else if (specialMode.Flags == 0 && character == SingleQuoteCharacter)
				{
					AppendCandidateCharacter(builder, ref candidateCharacterCode);

					if (builder.Length > 0 && !previousFlags.QuotedStringCandidate && !previousFlags.UnicodeCandidate)
					{
						yield return BuildToken(builder, index - 1);
					}

					inQuotedString = previousFlags.QuotedStringCandidate;

					specialMode.Flags |= SpecialModeFlags.InString;

					previousFlags.AssignmentOperatorCandidate = false;
				}

				if (previousFlags.BlockCommentBeginCandidate && specialMode.Flags == 0)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2);
					}

					if (character == AsteriskCharacter)
					{
						specialMode.Flags |= SpecialModeFlags.InBlockComment;
						AppendCandidateCharacter(builder, ref candidateCharacterCode);
					}
					else
					{
						yield return BuildToken((char)candidateCharacterCode, index - 1);
					}

					candidateCharacterCode = EmptyCharacterCode;

					flags.BlockCommentEndCandidate = false;
				}

				if (previousFlags.LineCommentCandidate && specialMode.Flags == 0)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2);
					}

					AppendCandidateCharacter(builder, ref candidateCharacterCode);

					if (character == '-')
					{
						specialMode.Flags |= SpecialModeFlags.InLineComment;
					}
					else
					{
						yield return BuildToken(builder, index - 1);
					}
				}

				if (flags.IsDecimalCandidate)
				{
					if (previousFlags.IsDecimalCandidate)
					{
						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 2);
						}

						AppendCandidateCharacter(builder, ref candidateCharacterCode);

						builder.Append(character);
						
						yield return BuildToken(builder, index);

						specialMode.InNumber = false;
						flags.IsDecimalCandidate = false;
						characterYielded = true;
					}
					else if (specialMode.InDecimalNumber || (!specialMode.InNumber && builder.Length > 0))
					{
						AppendCandidateCharacter(builder, ref candidateCharacterCode);
						
						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 1);
						}

						specialMode.InNumber = false;
					}
				}

				if (flags.IsDigit && builder.Length == 0)
				{
					specialMode.InNumber = true;
				}
				else if (!flags.IsDecimalCandidate && specialMode.InNumber)
				{
					if (!specialMode.InExponent && previousFlags.ExponentCandidate)
					{
						var isSign = character == '+' || character == '-';
						if (isSign || flags.IsDigit)
						{
							specialMode.InExponent = true;

							isSingleCharacterTerminal = false;
							flags.LineCommentCandidate = false;
						}
						else
						{
							if (builder.Length > 0)
							{
								yield return BuildToken(builder, index - 2);
							}

							specialMode.InNumber = false;
						}

						AppendCandidateCharacter(builder, ref candidateCharacterCode);
					}
					else if (!flags.ExponentCandidate && !flags.IsDigit)
					{
						if (specialMode.InPostfixedNumber || !flags.IsDataTypePostFix)
						{
							AppendCandidateCharacter(builder, ref candidateCharacterCode);

							if (builder.Length > 0)
							{
								yield return BuildToken(builder, index - 1);
							}

							specialMode.InNumber = false;
							flags.ExponentCandidate = false;
						}
						else if (!specialMode.InPostfixedNumber && flags.IsDataTypePostFix)
						{
							specialMode.InPostfixedNumber = true;
						}
					}
				}

				if (previousFlags.IsDecimalCandidate)
				{
					if (specialMode.InNumber && !specialMode.InDecimalNumber)
					{
						AppendCandidateCharacter(builder, ref candidateCharacterCode);
						specialMode.InDecimalNumber = true;
					}
					else if (candidateCharacterCode > EmptyCharacterCode)
					{
						yield return BuildToken((char)candidateCharacterCode, index - 1);
						specialMode.InNumber = false;
						candidateCharacterCode = EmptyCharacterCode;
					}
				}

				if (flags.AssignmentOperatorCandidate && builder.Length > 0)
				{
					yield return BuildToken(builder, index - 1);
				}
				
				if (previousFlags.AssignmentOperatorCandidate)
				{
					if (character == '=')
					{
						AppendCandidateCharacter(builder, ref candidateCharacterCode);

						if (builder.Length > 0)
						{
							builder.Append(character);
							yield return BuildToken(builder, index);
						}

						flags.OptionalParameterCandidate = false;

						characterYielded = true;
					}
					else
					{
						if (builder.Length > 0)
						{
							yield return BuildToken(builder, index - 2);
						}

						yield return BuildToken(previousFlags.Character, index - 1);

						candidateCharacterCode = EmptyCharacterCode;
					}
				}

				if ((specialMode.Flags & SpecialModeFlags.InQuotedIdentifier) != 0 && character == '"')
				{
					AppendCandidateCharacter(builder, ref candidateCharacterCode);

					if (builder.Length > 0)
					{
						builder.Append(character);
						yield return BuildToken(builder, index);
					}

					characterYielded = true;
					specialMode.Flags &= ~SpecialModeFlags.InQuotedIdentifier;
				}
				else if (specialMode.Flags == 0 && character == '"')
				{
					AppendCandidateCharacter(builder, ref candidateCharacterCode);

					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 1);
					}

					specialMode.Flags |= SpecialModeFlags.InQuotedIdentifier;
				}

				if (previousFlags.RelationalOperatorCandidate)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2);
					}

					if (character == '=' || (character == '>' && previousFlags.Character == '<') || (previousFlags.LabelMarkerCandidate && character == previousFlags.Character))
					{
						builder.Append(previousFlags.Character);
						builder.Append(character);
						yield return BuildToken(builder, index);
						characterYielded = true;
					}
					else
					{
						yield return BuildToken(previousFlags.Character, index - 1);
					}

					candidateCharacterCode = EmptyCharacterCode;

					flags.RelationalOperatorCandidate = false;
					flags.LabelMarkerCandidate = false;
					flags.OptionalParameterCandidate = false;
				}

				if (previousFlags.ConcatenationCandidate)
				{
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 2);
					}

					builder.Append(previousFlags.Character);
					var indexOffset = 1;
					if (character == '|')
					{
						builder.Append(character);
						indexOffset = 0;
						characterYielded = true;
						flags.ConcatenationCandidate = false;
					}

					yield return BuildToken(builder, index - indexOffset);

					candidateCharacterCode = EmptyCharacterCode;
				}

				if (specialMode.Flags == 0 && (isBlank || isSingleCharacterTerminal || character == AsteriskCharacter))
				{
					specialMode.InNumber = false;

					AppendCandidateCharacter(builder, ref candidateCharacterCode);
					
					if (builder.Length > 0)
					{
						yield return BuildToken(builder, index - 1);
					}

					if (!isBlank)
					{
						characterYielded = true;
						yield return BuildToken(character, index);
					}
				}
				
				if (flags.IsLineTerminator && (specialMode.Flags & SpecialModeFlags.InLineComment) != 0)
				{
					if (includeCommentBlocks)
					{
						builder.Append(character);
						yield return BuildToken(builder, index, CommentType.Line);
					}
					else
					{
						builder.Clear();
					}

					characterYielded = true;
					specialMode.Flags &= ~(SpecialModeFlags.InBlockComment | SpecialModeFlags.InLineComment);
					candidateCharacterCode = EmptyCharacterCode;
				}

				if (previousFlags.BlockCommentEndCandidate && (specialMode.Flags & SpecialModeFlags.InBlockComment) != 0)
				{
					if (character == '/')
					{
						specialMode.Flags &= ~(SpecialModeFlags.InBlockComment | SpecialModeFlags.InLineComment);

						if (includeCommentBlocks)
						{
							builder.Append(character);
							yield return BuildToken(builder, index, CommentType.Block);
						}
						else
						{
							builder.Clear();
						}

						characterYielded = true;
					}

					candidateCharacterCode = EmptyCharacterCode;
					flags.BlockCommentBeginCandidate = false;
				}

				if (!characterYielded)
				{
					var candidateMode = flags.ConcatenationCandidate || flags.OptionalParameterCandidate || flags.LineCommentCandidate || flags.BlockCommentBeginCandidate ||
										flags.RelationalOperatorCandidate || flags.IsDecimalCandidate || flags.ExponentCandidate || flags.AssignmentOperatorCandidate || flags.LabelMarkerCandidate;

					if (candidateMode && specialMode.Flags == 0)
					{
						candidateCharacterCode = characterCode;
					}
					else if (!isBlank || specialMode.Flags > 0)
					{
						builder.Append(character);
					}
				}
			}

			var commentType = (CommentType)(specialMode.Flags & (SpecialModeFlags.InLineComment | SpecialModeFlags.InBlockComment));
			if (commentType == CommentType.None || includeCommentBlocks)
			{
				var indexOffset = candidateCharacterCode == EmptyCharacterCode ? 0 : 1;
				if (builder.Length > 0)
				{
					yield return new OracleToken(builder.ToString(), index - builder.Length - indexOffset, commentType);
				}

				if (candidateCharacterCode != EmptyCharacterCode)
				{
					yield return new OracleToken(new String((char)candidateCharacterCode, 1), index - indexOffset, commentType);
				}
			}
		}

		private static bool IsQuotedStringClosingCharacter(char openingCharacter, char closingCharacterCandidate)
		{
			switch (openingCharacter)
			{
				case '<':
					return closingCharacterCandidate == '>';
				case '[':
					return closingCharacterCandidate == ']';
				case '(':
					return closingCharacterCandidate == ')';
				case '{':
					return closingCharacterCandidate == '}';
				default:
					return openingCharacter == closingCharacterCandidate;
			}
		}

		private static void AppendCandidateCharacter(StringBuilder builder, ref int candidateCharacterCode)
		{
			if (candidateCharacterCode == EmptyCharacterCode)
				return;

			builder.Append((char)candidateCharacterCode);
			candidateCharacterCode = EmptyCharacterCode;
		}

		private static OracleToken BuildToken(char character, int index)
		{
			return new OracleToken(character.ToString(CultureInfo.InvariantCulture), index - 1);	
		}

		private static OracleToken BuildToken(StringBuilder builder, int index, CommentType commentType = CommentType.None)
		{
			var value = builder.ToString();
			var token = new OracleToken(value, index - value.Length, commentType);

			builder.Clear();
			
			return token;
		}

		private static void FindFlags(SpecialMode specialMode, char character, RecognizedFlags flags)
		{
			flags.Reset();
			flags.Character = character;

			var specialModeFlags = specialMode.Flags;
			var inBlockComment = (specialModeFlags & SpecialModeFlags.InBlockComment) != 0;
			var notInComment = !inBlockComment && (specialModeFlags & SpecialModeFlags.InLineComment) == 0;
			var inString = (specialModeFlags & SpecialModeFlags.InString) != 0;
			var notInSpecialMode = specialModeFlags == 0;

			switch (character)
			{
				case '/':
					flags.BlockCommentBeginCandidate = notInComment;
					break;
				case AsteriskCharacter:
					flags.BlockCommentEndCandidate = inBlockComment;
					break;
				case '-':
					flags.LineCommentCandidate = notInComment;
					break;
				case '|':
					flags.ConcatenationCandidate = notInSpecialMode;
					break;
				case '=':
					flags.OptionalParameterCandidate = notInSpecialMode;
					break;
				case ':':
					flags.AssignmentOperatorCandidate = notInSpecialMode;
					break;
				case '>':
				case '<':
					flags.LabelMarkerCandidate = notInSpecialMode;
					flags.RelationalOperatorCandidate = notInSpecialMode;
					break;
				case '^':
				case '!':
					flags.RelationalOperatorCandidate = notInSpecialMode;
					break;
				case 'e':
				case 'E':
					flags.ExponentCandidate = specialMode.InNumber && notInSpecialMode;
					break;
				case 'd':
				case 'D':
				case 'f':
				case 'F':
					flags.IsDataTypePostFix = specialMode.InNumber && notInSpecialMode;
					break;
				case 'n':
				case 'N':
					flags.UnicodeCandidate = !inString;
					break;
				case 'q':
				case 'Q':
					flags.QuotedStringCandidate = !inString;
					break;
				case SingleQuoteCharacter:
					flags.StringEndCandidate = inString;
					break;
				case '.':
					flags.IsDecimalCandidate = notInSpecialMode;
					break;
				case ' ':
				case '\u00A0':
				case '\t':
					flags.IsSpace = true;
					break;
				case '\r':
				case '\n':
					flags.IsLineTerminator = true;
					break;
				default:
					flags.IsDigit = character >= 48 && character <= 57;
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
			public bool LabelMarkerCandidate;
			public bool QuotedStringCandidate;
			public bool StringEndCandidate;
			public bool ExponentCandidate;
			public bool UnicodeCandidate;
			public bool AssignmentOperatorCandidate;
			public bool IsDigit;
			public bool IsDataTypePostFix;

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
				UnicodeCandidate = false;
				StringEndCandidate = false;
				ExponentCandidate = false;
				AssignmentOperatorCandidate = false;
				LabelMarkerCandidate = false;
				IsDigit = false;
				IsDataTypePostFix = false;
			}

			public void CopyToPrevious(RecognizedFlags target)
			{
				target.Character = Character;
				target.IsSpace = IsSpace;
				target.IsLineTerminator = IsLineTerminator;
				target.IsDecimalCandidate = IsDecimalCandidate;
				target.LineCommentCandidate = LineCommentCandidate;
				target.BlockCommentBeginCandidate = BlockCommentBeginCandidate;
				target.BlockCommentEndCandidate = BlockCommentEndCandidate;
				target.ConcatenationCandidate = ConcatenationCandidate;
				target.OptionalParameterCandidate = OptionalParameterCandidate;
				target.RelationalOperatorCandidate = RelationalOperatorCandidate;
				target.QuotedStringCandidate = QuotedStringCandidate;
				target.UnicodeCandidate = UnicodeCandidate;
				target.StringEndCandidate = StringEndCandidate;
				target.ExponentCandidate = ExponentCandidate;
				target.AssignmentOperatorCandidate = AssignmentOperatorCandidate;
				target.LabelMarkerCandidate = LabelMarkerCandidate;
				target.IsDigit = IsDigit;
				target.IsDataTypePostFix = IsDataTypePostFix;
			}
		}

		[DebuggerDisplay("SpecialMode (InNumber={InNumber}; InDecimalNumber={InDecimalNumber}; InPostfixedNumber={InPostfixedNumber}; InExponent={InExponent})")]
		private struct SpecialMode
		{
			public SpecialModeFlags Flags;

			private bool _inNumber;

			public bool InPostfixedNumber;
			public bool InDecimalNumber;
			public bool InExponent;

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

		[Flags]
		private enum SpecialModeFlags
		{
			InLineComment = 1,
			InBlockComment = 2,
			InString = 4,
			InQuotedIdentifier = 8,
		}
	}
}
