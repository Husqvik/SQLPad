using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSqlModelReference (Columns={Columns.Count})")]
	public class OracleSqlModelReference : OracleDataObjectReference
	{
		private readonly IReadOnlyList<OracleSelectListColumn> _sqlModelColumns;
		private IReadOnlyList<OracleColumn> _columns;

		public OracleReferenceContainer SourceReferenceContainer { get; }

		public OracleReferenceContainer DimensionReferenceContainer { get; }
		
		public OracleReferenceContainer MeasuresReferenceContainer { get; }

		public IReadOnlyCollection<OracleReferenceContainer> ChildContainers { get; private set; }

		public override IEnumerable<OracleDataObjectReference> IncludeInnerReferences => base.IncludeInnerReferences.Concat(SourceReferenceContainer.ObjectReferences);

		public OracleSqlModelReference(OracleStatementSemanticModel semanticModel, IReadOnlyList<OracleSelectListColumn> columns, IEnumerable<OracleDataObjectReference> sourceReferences)
			: base(ReferenceType.SqlModel)
		{
			_sqlModelColumns = columns;
			
			SourceReferenceContainer = new OracleReferenceContainer(semanticModel);
			foreach (var column in columns)
			{
				SourceReferenceContainer.ColumnReferences.AddRange(column.ColumnReferences);
				SourceReferenceContainer.ProgramReferences.AddRange(column.ProgramReferences);
				SourceReferenceContainer.TypeReferences.AddRange(column.TypeReferences);
			}
			
			SourceReferenceContainer.ObjectReferences.AddRange(sourceReferences);

			DimensionReferenceContainer = new OracleReferenceContainer(semanticModel);
			MeasuresReferenceContainer = new OracleReferenceContainer(semanticModel);

			ChildContainers =
				new List<OracleReferenceContainer>
				{
					SourceReferenceContainer,
					DimensionReferenceContainer, MeasuresReferenceContainer
				}.AsReadOnly();
		}

		public override IReadOnlyList<OracleColumn> Columns
		{
			get { return _columns ?? (_columns = _sqlModelColumns.Select(c => c.ColumnDescription).ToArray()); }
		}

		public StatementGrammarNode MeasureExpressionList { get; set; }
	}
}