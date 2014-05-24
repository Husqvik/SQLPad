using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("SourcePosition (IndexStart={IndexStart}, IndexEnd={IndexEnd})")]
	public struct SourcePosition
	{
		public static SourcePosition Empty = new SourcePosition { IndexStart = -1, IndexEnd = -1 };

		public int IndexStart { get; set; }
		public int IndexEnd { get; set; }
		public int Length { get { return IndexEnd - IndexStart + 1; } }

		public bool ContainsIndex(int index)
		{
			return IndexStart <= index && index <= IndexEnd + 1;
		}
	}
}