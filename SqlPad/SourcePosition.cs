using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("SourcePosition (IndexStart={IndexStart}, IndexEnd={IndexEnd})")]
	public struct SourcePosition
	{
		public int IndexStart { get; set; }
		public int IndexEnd { get; set; }
		public int Length { get { return IndexEnd - IndexStart + 1; } }
	}
}