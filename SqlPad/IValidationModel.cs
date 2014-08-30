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

		string SemanticErrorType { get; }

		bool IsRecognized { get; }

		ICollection<string> ObjectNames { get; }

		string ToolTipText { get; }
	}
}
