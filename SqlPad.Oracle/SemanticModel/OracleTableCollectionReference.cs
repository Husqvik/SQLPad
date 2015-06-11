using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleTableCollectionReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; ObjectIdentifier={ObjectNode == null ? null : ObjectNode.Token.Value})")]
	public class OracleTableCollectionReference : OracleDataObjectReference
	{
		private IReadOnlyList<OracleColumn> _columns;
		
		public OracleReference RowSourceReference { get; private set; }

		public OracleTableCollectionReference(OracleReference rowSourceReference) : base(ReferenceType.TableCollection)
		{
			RowSourceReference = rowSourceReference;
			OwnerNode = rowSourceReference.OwnerNode;
			ObjectNode = rowSourceReference.ObjectNode;
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
			var programReference = RowSourceReference as OracleProgramReference;
			var programMetadata = programReference == null ? null : programReference.Metadata;

			var schemaObject = SchemaObject.GetTargetSchemaObject();
			var collectionType = schemaObject as OracleTypeCollection;
			if (collectionType != null)
			{
				columns.Add(OracleDatabaseModelBase.BuildColumnValueColumn(collectionType.ElementDataType));
			}
			else if (programMetadata != null && programMetadata.Parameters.Count > 1 &&
			         (programMetadata.Parameters[0].DataType == OracleTypeCollection.OracleCollectionTypeNestedTable || programMetadata.Parameters[0].DataType == OracleTypeCollection.OracleCollectionTypeVarryingArray))
			{
				var returnParameter = programMetadata.Parameters.SingleOrDefault(p => p.Direction == ParameterDirection.ReturnValue && p.DataLevel == 1 && p.Position == 1);
				if (returnParameter != null)
				{
					if (returnParameter.DataType == OracleTypeBase.TypeCodeObject)
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
						columns.Add(OracleDatabaseModelBase.BuildColumnValueColumn(((OracleTypeCollection)schemaObject).ElementDataType));
					}
				}
			}

			return _columns = columns.AsReadOnly();
		}
	}
}