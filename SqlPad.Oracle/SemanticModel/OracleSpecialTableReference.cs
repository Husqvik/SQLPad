using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSpecialTableReference (Alias={Name})")]
	public class OracleSpecialTableReference : OracleDataObjectReference
	{
		private IReadOnlyList<OracleColumn> _columns; 
		private readonly IEnumerable<OracleSelectListColumn> _selectListColumns; 

		public OracleSpecialTableReference(ReferenceType referenceType, IEnumerable<OracleColumn> columns)
			: base(referenceType)
		{
			_columns = columns.ToArray();
		}

		public OracleSpecialTableReference(ReferenceType referenceType, IEnumerable<OracleSelectListColumn> columns)
			: base(referenceType)
		{
			_selectListColumns = columns;
		}

		public override string Name => AliasNode?.Token.Value;

		protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(null, Name);
		}

		public override IReadOnlyList<OracleColumn> Columns => _columns ?? (_columns = _selectListColumns.Select(c => c.ColumnDescription).ToArray());
	}
}