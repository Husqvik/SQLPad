using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("TextSegment (IndextStart={IndextStart}; Length={Length}; DisplayOptions={DisplayOptions}; Text={Text})")]
	public struct TextSegment
	{
		public static readonly char[] Separators = { ' ', '\t', '\r', '\n', '\u00A0' };

		public static readonly TextSegment Empty = new TextSegment();

		public int IndextStart { get; set; }
		public int Length { get; set; }
		public string Text { get; set; }
		public DisplayOptions DisplayOptions { get; set; }
	}

	public enum DisplayOptions
	{
		None,
		Definition,
		Usage
	}
}