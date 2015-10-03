using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleTableCollectionReference (OwnerNode={OwnerNode == null ? null : OwnerNode.Token.Value}; ObjectNode={ObjectNode == null ? null : ObjectNode.Token.Value})")]
	public class OracleTableCollectionReference : OracleDataObjectReference
	{
		private IReadOnlyList<OracleColumn> _columns;
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

		public OracleTableCollectionReference() : base(ReferenceType.TableCollection)
		{
			Placement = StatementPlacement.TableReference;
		}

		public override string Name => AliasNode?.Token.Value;

		protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(null, Name);
		}

		public override IReadOnlyList<OracleColumn> Columns => _columns ?? BuildColumns();

		private IReadOnlyList<OracleColumn> BuildColumns()
		{
			var columnBuilderVisitor = new OracleColumnBuilderVisitor();
			_rowSourceReference?.Accept(columnBuilderVisitor);

			return _columns = columnBuilderVisitor.Columns;
		}
	}
}
