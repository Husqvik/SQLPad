using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleTableCollectionReference (OwnerNode={OwnerNode == null ? null : OwnerNode.Token.Value}; ObjectNode={ObjectNode == null ? null : ObjectNode.Token.Value})")]
	public class OracleTableCollectionReference : OracleDataObjectReference
	{
		private OracleReference _rowSourceReference;

		public OracleReference RowSourceReference
		{
			get { return _rowSourceReference; }
			set
			{
				_rowSourceReference = value;
				OwnerNode = _rowSourceReference.OwnerNode;
				ObjectNode = _rowSourceReference.ObjectNode;
				Owner = _rowSourceReference.Owner;
			}
		}

		public override string Name => AliasNode?.Token.Value;

		public override IEnumerable<OracleDataObjectReference> IncludeInnerReferences
		{
			get
			{
				yield return this;

				var columnSourceReference = _rowSourceReference as OracleColumnReference;
				var innerObjectReferences = columnSourceReference?.ValidObjectReference as OracleDataObjectReference;
				if (innerObjectReferences != null)
				{
					yield return innerObjectReferences;
				}
			}
		}

		public OracleTableCollectionReference(OracleReferenceContainer referenceContainer) : base(ReferenceType.TableCollection)
		{
			referenceContainer.ObjectReferences.Add(this);
			Container = referenceContainer;
			Placement = StatementPlacement.TableReference;
		}

		protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(null, Name);
		}

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitTableCollectionReference(this);
		}

		protected override IReadOnlyList<OracleColumn> BuildColumns()
		{
			var columnBuilderVisitor = new OracleColumnBuilderVisitor();
			_rowSourceReference?.Accept(columnBuilderVisitor);

			return columnBuilderVisitor.Columns;
		}
	}
}
