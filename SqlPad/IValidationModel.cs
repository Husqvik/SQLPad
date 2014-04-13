using System.Collections.Generic;

namespace SqlPad
{
	public interface IValidationModel
	{
		IDictionary<StatementDescriptionNode, bool> TableNodeValidity { get; }

		IDictionary<StatementDescriptionNode, IColumnValidationData> ColumnNodeValidity { get; }
	}

	public interface IColumnValidationData
	{
		StatementDescriptionNode ColumnNode { get; }
		SemanticError SemanticError { get; }
		bool IsRecognized { get; }
		ICollection<string> TableNames { get; }
	}

	public enum SemanticError
	{
		None,
		AmbiguousTableReference
	}
}