using System;
using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleDataTypeReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Name={ObjectNode.Token.Value}; SchemaObject={SchemaObject})")]
	public class OracleDataTypeReference : OracleReference
	{
		public override string Name { get { throw new NotSupportedException(); } }

		public OracleDataType ResolvedDataType { get; set; }

		public StatementGrammarNode PrecisionNode { get; set; }
		
		public StatementGrammarNode ScaleNode { get; set; }
		
		public StatementGrammarNode LengthNode { get; set; }

		public ICollection<OraclePlSqlType> PlSqlTypes { get; } = new List<OraclePlSqlType>();

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitDataTypeReference(this);
		}
	}
}
