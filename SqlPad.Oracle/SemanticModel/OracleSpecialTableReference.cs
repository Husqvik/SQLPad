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

		public StatementGrammarNode ColumnsClause { get; }

		public OracleSpecialTableReference(OracleReferenceContainer referenceContainer, ReferenceType referenceType, IEnumerable<OracleSelectListColumn> columns, StatementGrammarNode columnsClause)
			: base(referenceType)
		{
			referenceContainer.ObjectReferences.Add(this);
			Container = referenceContainer;
			ColumnDefinitions = columns.ToArray();
			ColumnsClause = columnsClause;
		}

		public IReadOnlyList<OracleSelectListColumn> ColumnDefinitions { get; }

		public override string Name => AliasNode?.Token.Value;

		protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(null, Name);
		}

		public override IReadOnlyList<OracleColumn> Columns => _columns ?? (_columns = ColumnDefinitions.Select(c => c.ColumnDescription).ToArray());
	}
}