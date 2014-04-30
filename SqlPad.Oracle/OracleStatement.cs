using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (Count={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingStatus = ProcessingStatus.Success,
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};
	}
}
