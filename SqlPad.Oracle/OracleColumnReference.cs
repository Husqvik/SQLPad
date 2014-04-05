using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={TableNode == null ? null : TableNode.Token.Value}; Column={ColumnNode.Token.Value})")]
	public class OracleColumnReference
	{
		public OracleColumnReference()
		{
			QueryBlocks = new HashSet<OracleQueryBlock>();
			TableNodeReferences = new HashSet<OracleTableReference>();
			ColumnNodeReferences = new HashSet<OracleTableReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, TableNode, null); }
		}

		public string Name { get { return ColumnNode.Token.Value; } }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public string TableName { get { return TableNode == null ? null : TableNode.Token.Value; } }

		public string NormalizedTableName { get { return TableNode == null ? null : TableName.ToQuotedIdentifier(); } }

		public bool ReferencesAllColumns { get { return ColumnNode.Token.Value == "*"; } }

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNode { get; set; }

		public StatementDescriptionNode ColumnNode { get; set; }

		public ICollection<OracleQueryBlock> QueryBlocks { get; set; }

		public ICollection<OracleTableReference> TableNodeReferences { get; set; }
		
		public ICollection<OracleTableReference> ColumnNodeReferences { get; set; }
	}
}