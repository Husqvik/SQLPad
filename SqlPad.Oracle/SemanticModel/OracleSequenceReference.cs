using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSequenceReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Sequence={ObjectNode.Token.Value})")]
	public class OracleSequenceReference : OracleObjectWithColumnsReference
	{
		public override string Name => ObjectNode.Token.Value;

	    public override IReadOnlyList<OracleColumn> Columns => ((OracleSequence)SchemaObject).Columns;

	    public override ReferenceType Type => ReferenceType.SchemaObject;
	}
}