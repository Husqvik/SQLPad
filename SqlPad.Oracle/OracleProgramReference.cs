using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleProgramReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Function={FunctionIdentifierNode.Token.Value})")]
	public class OracleProgramReference : OracleProgramReferenceBase
	{
		public override string Name { get { return FunctionIdentifierNode.Token.Value; } }

		public StatementDescriptionNode FunctionIdentifierNode { get; set; }
		
		public StatementDescriptionNode AnalyticClauseNode { get; set; }

		public OracleFunctionMetadata Metadata { get; set; }
	}

	[DebuggerDisplay("OracleTypeReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Type={ObjectNode.Token.Value})")]
	public class OracleTypeReference : OracleProgramReferenceBase
	{
		public override string Name { get { return ObjectNode.Token.Value; } }
	}

	[DebuggerDisplay("OracleSequenceReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Sequence={ObjectNode.Token.Value})")]
	public class OracleSequenceReference : OracleReference
	{
		public override string Name { get { return ObjectNode.Token.Value; } }
	}

	public abstract class OracleProgramReferenceBase : OracleReference
	{
		public OracleSchemaObject SchemaObject { get; set; }

		public StatementDescriptionNode ParameterListNode { get; set; }

		public ICollection<StatementDescriptionNode> ParameterNodes { get; set; }
	}
}
