using System.Collections.Generic;

namespace SqlPad
{
	public interface IValidationModel
	{
		StatementBase Statement { get; }

		IStatementSemanticModel SemanticModel { get; }

		IDictionary<StatementGrammarNode, INodeValidationData> ObjectNodeValidity { get; }

		IDictionary<StatementGrammarNode, INodeValidationData> ColumnNodeValidity { get; }
		
		IDictionary<StatementGrammarNode, INodeValidationData> ProgramNodeValidity { get; }
		
		IDictionary<StatementGrammarNode, INodeValidationData> IdentifierNodeValidity { get; }

		IEnumerable<KeyValuePair<StatementGrammarNode, INodeValidationData>> GetNodesWithSemanticErrors();
	}

	public interface INodeValidationData
	{
		StatementGrammarNode Node { get; }
		SemanticError SemanticError { get; }
		bool IsRecognized { get; }
		ICollection<string> ObjectNames { get; }

		string ToolTipText { get; }
	}

	public enum SemanticError
	{
		None,
		AmbiguousReference,
		InvalidParameterCount,
		MissingParenthesis,
		NoParenthesisFunction,
		InvalidIdentifier,
		AnalyticClauseNotSupported,
		ObjectStatusInvalid,
		ObjectCannotBeUsed
	}
}
