using System.Collections.Generic;

namespace SqlPad
{
	public interface IValidationModel
	{
		StatementBase Statement { get; }

		IDictionary<StatementDescriptionNode, INodeValidationData> ObjectNodeValidity { get; }

		IDictionary<StatementDescriptionNode, INodeValidationData> ColumnNodeValidity { get; }
		
		IDictionary<StatementDescriptionNode, INodeValidationData> FunctionNodeValidity { get; }

		IEnumerable<KeyValuePair<StatementDescriptionNode, SemanticError>> GetNodesWithSemanticErrors();
	}

	public interface INodeValidationData
	{
		StatementDescriptionNode Node { get; }
		SemanticError SemanticError { get; }
		bool IsRecognized { get; }
		ICollection<string> ObjectNames { get; }
	}

	public enum SemanticError
	{
		None,
		AmbiguousReference,
		InvalidParameterCount,
		MissingParenthesis
	}
}
