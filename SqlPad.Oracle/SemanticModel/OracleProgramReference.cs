using System;
using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleProgramReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Program={ProgramIdentifierNode.Token.Value}; Metadata={Metadata})")]
	public class OracleProgramReference : OracleProgramReferenceBase
	{
		public override string Name => ProgramIdentifierNode.Token.Value;

	    public StatementGrammarNode ProgramIdentifierNode { get; set; }
		
		public StatementGrammarNode AnalyticClauseNode { get; set; }
		
		public override OracleProgramMetadata Metadata { get; set; }

		protected override IEnumerable<StatementGrammarNode> GetAdditionalIdentifierTerminals()
		{
			if (ProgramIdentifierNode != null)
			{
				yield return ProgramIdentifierNode;
			}
		}

		public override void Accept(IOracleReferenceVisitor visitor)
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

		public override void Accept(IOracleReferenceVisitor visitor)
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

	public class ProgramParameterReference
	{
		public static readonly ProgramParameterReference[] EmptyArray = new ProgramParameterReference[0];

		public StatementGrammarNode ParameterNode { get; set; }

		public StatementGrammarNode ValueNode { get; set; }

		public StatementGrammarNode OptionalIdentifierTerminal { get; set; }
	}
}
