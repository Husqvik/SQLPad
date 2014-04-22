using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleFunctionReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Function={FunctionIdentifierNode.Token.Value})")]
	public class OracleFunctionReference : OracleReference
	{
		public OracleFunctionReference()
		{
			ParameterNodes = new HashSet<StatementDescriptionNode>();
		}

		public override string Name { get { return FunctionIdentifierNode.Token.Value; } }

		public StatementDescriptionNode FunctionIdentifierNode { get; set; }
		
		public StatementDescriptionNode ParameterListNode { get; set; }
		
		public StatementDescriptionNode AnalyticClauseNode { get; set; }
		
		public ICollection<StatementDescriptionNode> ParameterNodes { get; set; }

		public OracleSqlFunctionMetadata FunctionMetadata { get; set; }
		
		public StatementDescriptionNode RootNode { get; set; }
	}
}
