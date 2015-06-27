using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	public abstract class OracleReferenceVisitor
	{
		public abstract void VisitColumnReference(OracleColumnReference columnReference);

		public abstract void VisitProgramReference(OracleProgramReference programReference);

		public abstract void VisitTypeReference(OracleTypeReference typeReference);
	}

	public class OracleColumnBuilderVisitor : OracleReferenceVisitor
	{
		private readonly List<OracleColumn> _columns = new List<OracleColumn>();

		public IReadOnlyList<OracleColumn> Columns { get { return _columns.AsReadOnly(); } }

		public override void VisitColumnReference(OracleColumnReference columnReference)
		{
			if (columnReference.ValidObjectReference == null)
			{
				return;
			}
			
			var matchedColumns = columnReference.ValidObjectReference.Columns.Where(c => String.Equals(columnReference.NormalizedName, c.Name)).ToArray();
			if (matchedColumns.Length != 1)
			{
				return;
			}
			
			var dataType = matchedColumns[0].DataType;
			if (dataType.IsDynamicCollection)
			{
				_columns.Add(OracleColumn.BuildColumnValueColumn(OracleDataType.Empty));
			}
			else
			{
				var semanticModel = columnReference.Owner.SemanticModel;
				if (semanticModel.HasDatabaseModel)
				{
					var schemaObject = semanticModel.DatabaseModel.GetFirstSchemaObject<OracleTypeCollection>(dataType.FullyQualifiedName);
					if (schemaObject != null)
					{
						_columns.Add(OracleColumn.BuildColumnValueColumn(((OracleTypeCollection)schemaObject).ElementDataType));
					}
				}
			}
		}

		public override void VisitProgramReference(OracleProgramReference programReference)
		{
			var programMetadata = programReference.Metadata;
			var semanticModel = programReference.Owner.SemanticModel;
			if (programMetadata == null || programMetadata.Parameters.Count <= 1 || !semanticModel.HasDatabaseModel ||
			    (!String.Equals(programMetadata.Parameters[0].DataType, OracleTypeCollection.OracleCollectionTypeNestedTable) && !String.Equals(programMetadata.Parameters[0].DataType, OracleTypeCollection.OracleCollectionTypeVarryingArray)))
			{
				return;
			}
			
			var returnParameter = programMetadata.Parameters.SingleOrDefault(p => p.Direction == ParameterDirection.ReturnValue && p.DataLevel == 1 && p.Position == 1);
			if (returnParameter == null)
			{
				return;
			}

			OracleSchemaObject schemaObject;
			if (String.Equals(returnParameter.DataType, OracleTypeBase.TypeCodeObject))
			{
				if (semanticModel.DatabaseModel.AllObjects.TryGetValue(returnParameter.CustomDataType, out schemaObject))
				{
					var attributeColumns = ((OracleTypeObject)schemaObject).Attributes
						.Select(a =>
							new OracleColumn
							{
								DataType = a.DataType,
								Nullable = true,
								Name = a.Name
							});

					_columns.AddRange(attributeColumns);
				}
			}
			else if (semanticModel.DatabaseModel.AllObjects.TryGetValue(programMetadata.Parameters[0].CustomDataType, out schemaObject))
			{
				_columns.Add(OracleColumn.BuildColumnValueColumn(((OracleTypeCollection)schemaObject).ElementDataType));
			}
		}

		public override void VisitTypeReference(OracleTypeReference typeReference)
		{
			var collectionType = typeReference.SchemaObject.GetTargetSchemaObject() as OracleTypeCollection;
			if (collectionType != null)
			{
				_columns.Add(OracleColumn.BuildColumnValueColumn(collectionType.ElementDataType));
			}
		}
	}
}
