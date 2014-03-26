using System.Collections.Generic;

namespace SqlPad
{
	public interface IStatement
	{
		ProcessingStatus ProcessingStatus { get; set; }
		ICollection<StatementDescriptionNode> NodeCollection { get; set; }
		SourcePosition SourcePosition { get; set; }
		StatementDescriptionNode GetNodeAtPosition(int offset);
		StatementDescriptionNode GetNearestTerminalToPosition(int offset);
	}
}