using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock
	{
		public OracleQueryBlock()
		{
			ObjectReferences = new List<OracleObjectReference>();
			Columns = new List<OracleSelectListColumn>();
			AccessibleQueryBlocks = new List<OracleQueryBlock>();
			ColumnReferences = new List<OracleColumnReference>();
			FunctionReferences = new List<OracleFunctionReference>();
		}

		public string Alias { get; set; }

		public QueryBlockType Type { get; set; }

		public StatementDescriptionNode RootNode { get; set; }
		
		public OracleStatement Statement { get; set; }

		public ICollection<OracleObjectReference> ObjectReferences { get; private set; }

		public ICollection<OracleSelectListColumn> Columns { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }
		
		public ICollection<OracleFunctionReference> FunctionReferences { get; private set; }

		public IEnumerable<OracleColumnReference> AllColumnReferences { get { return Columns.SelectMany(c => c.ColumnReferences).Concat(ColumnReferences); } }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; private set; }
	}
}