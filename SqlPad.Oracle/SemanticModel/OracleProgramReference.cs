using System;
using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleProgramReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Function={FunctionIdentifierNode.Token.Value}; Metadata={Metadata})")]
	public class OracleProgramReference : OracleProgramReferenceBase
	{
		public override string Name => FunctionIdentifierNode.Token.Value;

	    public StatementGrammarNode FunctionIdentifierNode { get; set; }
		
		public StatementGrammarNode AnalyticClauseNode { get; set; }
		
		public override OracleProgramMetadata Metadata { get; set; }

		public override void Accept(OracleReferenceVisitor visitor)
		{
			visitor.VisitProgramReference(this);
		}
	}

	[DebuggerDisplay("OracleTypeReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Type={ObjectNode.Token.Value}; Metadata={Metadata})")]
	public class OracleTypeReference : OracleProgramReferenceBase
	{
		public override string Name => ObjectNode.Token.Value;

	    public override OracleProgramMetadata Metadata
		{
			get { return ((OracleTypeBase)SchemaObject.GetTargetSchemaObject()).GetConstructorMetadata(); }
			set { throw new NotSupportedException("Metadata cannot be set. It is inferred from type attributes"); }
		}

		public override void Accept(OracleReferenceVisitor visitor)
		{
			visitor.VisitTypeReference(this);
		}
	}

	public abstract class OracleProgramReferenceBase : OracleReference
	{
		public StatementGrammarNode ParameterListNode { get; set; }

		public IReadOnlyList<ProgramParameterReference> ParameterReferences { get; set; }

		public abstract OracleProgramMetadata Metadata { get; set; }
	}

	public struct ProgramParameterReference
	{
		public StatementGrammarNode OptionalIdentifierTerminal { get; set; }

		public StatementGrammarNode ParameterNode { get; set; }
	}
}
