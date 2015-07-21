using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSpecialTableReference (Alias={Name})")]
	public class OracleSpecialTableReference : OracleDataObjectReference
	{
	    public OracleSpecialTableReference(ReferenceType referenceType, IEnumerable<OracleColumn> columns)
			: base(referenceType)
		{
			Columns = new List<OracleColumn>(columns).AsReadOnly();
		}

		public override string Name => AliasNode?.Token.Value;

	    protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(null, Name);
		}

		public override IReadOnlyList<OracleColumn> Columns { get; }
	}
}