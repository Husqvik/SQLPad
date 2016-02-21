using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSequenceReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Sequence={ObjectNode.Token.Value})")]
	public class OracleSequenceReference : OracleObjectWithColumnsReference
	{
		private static readonly OracleColumn[] EmptyArray = new OracleColumn[0];

		public override string Name => ObjectNode.Token.Value;

		public override ReferenceType Type => ReferenceType.SchemaObject;

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitSequenceReference(this);
		}

		protected override IReadOnlyList<OracleColumn> BuildColumns()
		{
			return EmptyArray;
		}

		protected override IReadOnlyList<OracleColumn> BuildPseudocolumns()
		{
			return ((OracleSequence)SchemaObject).Columns;
		}
	}
}