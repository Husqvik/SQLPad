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
			ColumnNodeColumnReferences = new HashSet<OracleColumn>();
		}

		public override string Name { get { return ColumnNode.Token.Value; } }

		public bool ReferencesAllColumns { get { return ColumnNode.Token.Value == "*"; } }

		public QueryBlockPlacement Placement { get; set; }
		
		public StatementDescriptionNode ColumnNode { get; set; }

		public ICollection<OracleObjectReference> ColumnNodeObjectReferences { get; private set; }

		public ICollection<OracleColumn> ColumnNodeColumnReferences { get; set; }
		
		public OracleColumn ColumnDescription { get; set; }
	}

	public enum QueryBlockPlacement
	{
		SelectList,
		From,
		Where,
		GroupBy,
		Having,
		Join,
		OrderBy
	}
}