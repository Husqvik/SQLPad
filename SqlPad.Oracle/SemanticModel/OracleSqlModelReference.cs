using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSqlModelReference (Columns={_columns.Count})")]
	public class OracleSqlModelReference : OracleDataObjectReference
	{
		private IReadOnlyList<OracleColumn> _columns;

		public IReadOnlyList<OracleSelectListColumn> ColumnDefinitions { get; }

		public OracleReferenceContainer SourceReferenceContainer { get; }

		public OracleReferenceContainer DimensionReferenceContainer { get; }
		
		public OracleReferenceContainer MeasuresReferenceContainer { get; }

		public IReadOnlyCollection<OracleReferenceContainer> ChildContainers { get; private set; }

		public override IEnumerable<OracleDataObjectReference> IncludeInnerReferences => base.IncludeInnerReferences.Concat(SourceReferenceContainer.ObjectReferences);

		public override IReadOnlyList<OracleColumn> Columns
		{
			get { return _columns ?? (_columns = ColumnDefinitions.Select(c => c.ColumnDescription).ToArray()); }
		}

		public StatementGrammarNode MeasureExpressionList { get; }

		public OracleSqlModelReference(OracleStatementSemanticModel semanticModel, IReadOnlyList<OracleSelectListColumn> columns, IEnumerable<OracleDataObjectReference> sourceReferences, StatementGrammarNode measureExpressionList)
			: base(ReferenceType.SqlModel)
		{
			ColumnDefinitions = columns;

			MeasureExpressionList = measureExpressionList;

			SourceReferenceContainer = new OracleReferenceContainer(semanticModel);
			foreach (var column in columns)
			{
				TransferReferences(column.ColumnReferences, SourceReferenceContainer.ColumnReferences);
				TransferReferences(column.ProgramReferences, SourceReferenceContainer.ProgramReferences);
				TransferReferences(column.TypeReferences, SourceReferenceContainer.TypeReferences);
			}
			
			SourceReferenceContainer.ObjectReferences.AddRange(sourceReferences);

			DimensionReferenceContainer = new OracleReferenceContainer(semanticModel);
			MeasuresReferenceContainer = new OracleReferenceContainer(semanticModel);

			ChildContainers = new[] { SourceReferenceContainer, DimensionReferenceContainer, MeasuresReferenceContainer };
		}

		private void TransferReferences<T>(IEnumerable<T> sourceReferences, ICollection<T> targetList) where T : OracleReference
		{
			foreach (var sourceReference in sourceReferences)
			{
				targetList.Add(sourceReference);
				sourceReference.Container = SourceReferenceContainer;
			}
		}
	}
}