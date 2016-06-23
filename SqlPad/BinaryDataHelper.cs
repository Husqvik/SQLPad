using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public static class BinaryDataHelper
	{
		private const byte DefaultBytesPerLine = 16;

		public static Task<string> FormatBinaryDataAsync(byte[] data, CancellationToken cancellationToken, byte bytesPerLine = DefaultBytesPerLine)
		{
			return Task.Run(() => FormatBinaryDataInternal(data, cancellationToken, bytesPerLine), cancellationToken);
		}

		public static int GetContentHashCode(this byte[] bytes)
		{
			unchecked
			{
				var hashCode = 0;
				foreach (var b in bytes)
				{
					hashCode = (hashCode * 397) ^ b;
				}

				return hashCode;
			}
		}

		private static string FormatBinaryDataInternal(ICollection<byte> data, CancellationToken cancellationToken, byte bytesPerLine)
		{
			var lineCount = data.Count / bytesPerLine;
			if (data.Count % bytesPerLine > 0)
				lineCount++;

			var addressLength = data.Count.ToString("X").Length;
			var lineAddressMask = $"{{0:X{addressLength}}}-{{1:X{addressLength}}}|";

			var lineTerminatorLength = Environment.NewLine.Length;
			var builderCapacity = (4 * bytesPerLine + lineAddressMask.Length + lineTerminatorLength) * lineCount;
			var builder = new StringBuilder(builderCapacity);

			var sourceBytes = data.GetEnumerator();

			for (var i = 0; i < lineCount; ++i)
			{
				cancellationToken.ThrowIfCancellationRequested();

				NextHexLine(builder, lineAddressMask, bytesPerLine, i, sourceBytes);
			}

			return builder.ToString();
		}

		private static void NextHexLine(StringBuilder builder, string lineAddressMask, byte bytesPerLine, int lineIndex, IEnumerator<byte> sourceBytes)
		{
			var hexadecimalPart = new StringBuilder(3 * bytesPerLine);
			var textPart = new StringBuilder(bytesPerLine);
			var i = 0;
			while (i < bytesPerLine && sourceBytes.MoveNext())
			{
				hexadecimalPart.Append($"{sourceBytes.Current:X2}{(i < bytesPerLine - 1 ? " " : "|")}");

				var character = Convert.ToChar(sourceBytes.Current);
				textPart.Append(Char.IsControl(character) ? '.' : character);

				++i;
			}
			
			if (i == 0)
			{
				return;
			}

			var characterIndex = lineIndex * bytesPerLine;
			builder.Append(String.Format(lineAddressMask, characterIndex, characterIndex + i - 1));

			if (i < bytesPerLine - 1)
			{
				var emptyCharacters = bytesPerLine - i;
				hexadecimalPart.Append(new String(' ', emptyCharacters * 3 - 1));
				hexadecimalPart.Append("|");
				textPart.Append(new String(' ', emptyCharacters));
			}

			builder.Append(hexadecimalPart);
			builder.Append(textPart);
			builder.Append(Environment.NewLine);
		}
	}
}
