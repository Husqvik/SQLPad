using System;
using System.Diagnostics;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleDataTypeReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Name={ObjectNode.Token.Value}; SchemaObject={SchemaObject})")]
	public class OracleDataTypeReference : OracleReference
	{
		public override string Name { get { throw new NotImplementedException(); } }
	}
}
