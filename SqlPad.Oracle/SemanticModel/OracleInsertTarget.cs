using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle.SemanticModel
{
	public class OracleInsertTarget : OracleReferenceContainer
	{
		public OracleInsertTarget(OracleStatementSemanticModel semanticModel) : base(semanticModel)
		{
		}

		public StatementGrammarNode RootNode { get; set; }

		public StatementGrammarNode TargetNode { get; set; }

		public StatementGrammarNode ColumnListNode { get; set; }

		public StatementGrammarNode ValueList { get; set; }

		public IReadOnlyList<StatementGrammarNode> ValueExpressions { get; set; }

		public IReadOnlyDictionary<StatementGrammarNode, string> Columns { get; set; }

		public OracleQueryBlock RowSource { get; set; }

		public OracleDataObjectReference DataObjectReference => ObjectReferences.Count == 1 ? ObjectReferences.First() : null;
	}
}