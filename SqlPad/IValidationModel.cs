using System.Collections.Generic;

namespace SqlPad
{
	public interface IValidationModel
	{
		IDictionary<StatementDescriptionNode, INodeValidationData> TableNodeValidity { get; }

		IDictionary<StatementDescriptionNode, INodeValidationData> ColumnNodeValidity { get; }
	}

	public interface INodeValidationData
	{
		StatementDescriptionNode Node { get; }
		SemanticError SemanticError { get; }
		bool IsRecognized { get; }
		ICollection<string> TableNames { get; }
	}

	public enum SemanticError
	{
		None,
		AmbiguousReference
	}
}