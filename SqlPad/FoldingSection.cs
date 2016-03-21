using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("FoldingSection (Placeholder={Placeholder}; Range={FoldingStart + \"-\" + FoldingEnd})")]
	public class FoldingSection
	{
		public int FoldingStart { get; set; }

		public int FoldingEnd { get; set; }
		
		public string Placeholder { get; set; }
		
		public bool IsNested { get; set; }
	}
}
