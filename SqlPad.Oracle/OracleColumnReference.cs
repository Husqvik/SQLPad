using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Column={ColumnNode.Token.Value})")]
	public class OracleColumnReference
	{
		public OracleColumnReference()
		{
			QueryBlocks = new HashSet<OracleQueryBlock>();
			ObjectNodeObjectReferences = new HashSet<OracleObjectReference>();
			ColumnNodeObjectReferences = new HashSet<OracleObjectReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null); }
		}

		public string Name { get { return ColumnNode.Token.Value; } }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public string ObjectName { get { return ObjectNode == null ? null : ObjectNode.Token.Value; } }

		public string ObjectTableName { get { return ObjectNode == null ? null : ObjectName.ToQuotedIdentifier(); } }

		public bool ReferencesAllColumns { get { return ColumnNode.Token.Value == "*"; } }

		public ColumnReferenceType Type { get; set; }
		
		public OracleQueryBlock Owner { get; set; }

		public OracleSelectListColumn SelectListColumn { get; set; }
		
		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode ObjectNode { get; set; }

		public StatementDescriptionNode ColumnNode { get; set; }

		public ICollection<OracleQueryBlock> QueryBlocks { get; set; }

		public ICollection<OracleObjectReference> ObjectNodeObjectReferences { get; set; }
		
		public ICollection<OracleObjectReference> ColumnNodeObjectReferences { get; set; }

		public int ColumnNodeColumnReferences { get; set; }
	}

	public enum ColumnReferenceType
	{
		SelectList,
		WhereGroupHaving,
		Join
	}
}