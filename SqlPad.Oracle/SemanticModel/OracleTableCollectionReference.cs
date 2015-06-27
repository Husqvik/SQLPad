using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

		public override string Name { get { return AliasNode == null ? null : AliasNode.Token.Value; } }

		protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(null, Name);
		}

		public override IReadOnlyList<OracleColumn> Columns
		{
			get { return _columns ?? BuildColumns(); }
		}

		private IReadOnlyList<OracleColumn> BuildColumns()
		{
			var columns = new List<OracleColumn>();
			var columnReference = _rowSourceReference as OracleColumnReference;
			var programReference = _rowSourceReference as OracleProgramReference;
			var programMetadata = programReference == null ? null : programReference.Metadata;

			var schemaObject = _rowSourceReference.SchemaObject.GetTargetSchemaObject();
			var collectionType = schemaObject as OracleTypeCollection;
			if (collectionType != null)
			{
				columns.Add(OracleColumn.BuildColumnValueColumn(collectionType.ElementDataType));
			}
			else if (columnReference != null)
			{
				if (columnReference.ValidObjectReference != null)
				{
					var matchedColumns = columnReference.ValidObjectReference.Columns.Where(c => String.Equals(columnReference.NormalizedName, c.Name)).ToArray();
					if (matchedColumns.Length == 1 && Owner.SemanticModel.HasDatabaseModel)
					{
						var dataType = matchedColumns[0].DataType;
						if (dataType.IsDynamic)
						{
							columns.Add(OracleColumn.BuildColumnValueColumn(OracleDataType.Empty));
						}
						else
						{
							schemaObject = Owner.SemanticModel.DatabaseModel.GetFirstSchemaObject<OracleTypeCollection>(dataType.FullyQualifiedName);
							if (schemaObject != null)
							{
								columns.Add(OracleColumn.BuildColumnValueColumn(((OracleTypeCollection)schemaObject).ElementDataType));
							}
						}
					}
				}
			}
			else if (programMetadata != null && programMetadata.Parameters.Count > 1 && Owner.SemanticModel.HasDatabaseModel &&
			         (programMetadata.Parameters[0].DataType == OracleTypeCollection.OracleCollectionTypeNestedTable || String.Equals(programMetadata.Parameters[0].DataType, OracleTypeCollection.OracleCollectionTypeVarryingArray)))
			{
				var returnParameter = programMetadata.Parameters.SingleOrDefault(p => p.Direction == ParameterDirection.ReturnValue && p.DataLevel == 1 && p.Position == 1);
				if (returnParameter != null)
				{
					if (String.Equals(returnParameter.DataType, OracleTypeBase.TypeCodeObject))
					{
						if (Owner.SemanticModel.DatabaseModel.AllObjects.TryGetValue(returnParameter.CustomDataType, out schemaObject))
						{
							var attributeColumns = ((OracleTypeObject)schemaObject).Attributes
								.Select(a =>
									new OracleColumn
									{
										DataType = a.DataType,
										Nullable = true,
										Name = a.Name
									});

							columns.AddRange(attributeColumns);
						}
					}
					else if (Owner.SemanticModel.DatabaseModel.AllObjects.TryGetValue(programMetadata.Parameters[0].CustomDataType, out schemaObject))
					{
						columns.Add(OracleColumn.BuildColumnValueColumn(((OracleTypeCollection)schemaObject).ElementDataType));
					}
				}
			}

			return _columns = columns.AsReadOnly();
		}
	}
}