using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock
	{
		public OracleQueryBlock()
		{
			TableReferences = new List<OracleTableReference>();
			Columns = new List<OracleSelectListColumn>();
			AccessibleQueryBlocks = new List<OracleQueryBlock>();
			ColumnReferences = new List<OracleColumnReference>();
		}

		public string Alias { get; set; }

		public QueryBlockType Type { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public ICollection<OracleTableReference> TableReferences { get; private set; }

		public ICollection<OracleSelectListColumn> Columns { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; private set; }
	}
}