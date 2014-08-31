using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Column={ColumnNode.Token.Value})")]
	public class OracleColumnReference : OracleReference
	{
		public OracleColumnReference()
		{
			ColumnNodeObjectReferences = new HashSet<OracleObjectWithColumnsReference>();
			ColumnNodeColumnReferences = new List<OracleColumn>();
		}

		public override string Name { get { return ColumnNode.Token.Value; } }

		public bool ReferencesAllColumns { get { return ColumnNode.Token.Value == "*"; } }

		public QueryBlockPlacement Placement { get; set; }
		
		public StatementGrammarNode ColumnNode { get; set; }

		public ICollection<OracleObjectWithColumnsReference> ColumnNodeObjectReferences { get; private set; }

		public ICollection<OracleColumn> ColumnNodeColumnReferences { get; set; }
		
		public OracleColumn ColumnDescription { get; set; }

		public OracleObjectWithColumnsReference ValidObjectReference
		{
			get
			{
				if (ColumnNodeObjectReferences.Count == 1)
					return ColumnNodeObjectReferences.First();

				return ObjectNodeObjectReferences.Count == 1
					? ObjectNodeObjectReferences.First()
					: null;
			}
		}
	}

	public enum QueryBlockPlacement
	{
		None,
		SelectList,
		From,
		Where,
		GroupBy,
		Having,
		Join,
		OrderBy
	}
}