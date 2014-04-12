using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (Count={NodeCollection.Count})")]
	public class OracleStatement : StatementBase
	{
		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingStatus = ProcessingStatus.Success,
				NodeCollection = new StatementDescriptionNode[0],
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};
	}
}
