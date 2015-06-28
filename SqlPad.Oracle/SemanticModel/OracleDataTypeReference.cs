using System;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleDataTypeReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Name={ObjectNode.Token.Value}; SchemaObject={SchemaObject})")]
	public class OracleDataTypeReference : OracleReference
	{
		public override string Name { get { throw new NotImplementedException(); } }

		public OracleDataType ResolvedDataType { get; set; }

		public StatementGrammarNode PrecisionNode { get; set; }
		
		public StatementGrammarNode ScaleNode { get; set; }
		
		public StatementGrammarNode LengthNode { get; set; }
	}
}
