using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Column={ColumnNode.Token.Value})")]
	public class OracleColumnReference : OracleReference
	{
		public OracleColumnReference()
		{
			ColumnNodeObjectReferences = new HashSet<OracleObjectReference>();
		}

		public override string Name { get { return ColumnNode.Token.Value; } }

		public bool ReferencesAllColumns { get { return ColumnNode.Token.Value == "*"; } }

		public ColumnReferenceType Type { get; set; }
		
		public StatementDescriptionNode ColumnNode { get; set; }

		public ICollection<OracleObjectReference> ColumnNodeObjectReferences { get; private set; }

		public int ColumnNodeColumnReferences { get; set; }
		
		public OracleColumn ColumnDescription { get; set; }
	}

	public enum ColumnReferenceType
	{
		SelectList,
		WhereGroupHaving,
		Join,
		OrderBy
	}
}