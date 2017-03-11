using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	public interface IOracleReferenceVisitor
	{
		void VisitDataObjectReference(OracleDataObjectReference dataObjectReference);

		void VisitColumnReference(OracleColumnReference columnReference);

		void VisitProgramReference(OracleProgramReference programReference);

		void VisitTypeReference(OracleTypeReference typeReference);

		void VisitSequenceReference(OracleSequenceReference sequenceReference);

		void VisitTableCollectionReference(OracleTableCollectionReference tableCollectionReference);

		void VisitPartitionReference(OraclePartitionReference partitionReference);

		void VisitDataTypeReference(OracleDataTypeReference dataTypeReference);

		void VisitPlSqlVariableReference(OraclePlSqlVariableReference variableReference);

		void VisitPlSqlExceptionReference(OraclePlSqlExceptionReference exceptionReference);
	}

	public class OracleColumnBuilderVisitor : IOracleReferenceVisitor
	{
		private readonly List<OracleColumn> _columns = new List<OracleColumn>();

		public IReadOnlyList<OracleColumn> Columns => _columns.AsReadOnly();

		public void VisitColumnReference(OracleColumnReference columnReference)
		{
			var matchedColumns = columnReference.ValidObjectReference?.Columns.Where(c => String.Equals(columnReference.NormalizedName, c.Name)).ToArray();
			if (matchedColumns?.Length != 1)
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

		public void VisitProgramReference(OracleProgramReference programReference)
		{
			var programMetadata = programReference.Metadata;
			var semanticModel = programReference.Owner.SemanticModel;
			if (programMetadata == null || !semanticModel.HasDatabaseModel)
			{
				return;
			}

			if (programReference.ParameterReferences.Count == 2 && programMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramCast)
			{
				var dataTypeNode = programReference.ParameterReferences[1].ParameterNode;
				var dataTypeReference = programReference.Container.DataTypeReferences.SingleOrDefault(dt => dt.RootNode == dataTypeNode);
				if (dataTypeReference?.SchemaObject.GetTargetSchemaObject() is OracleTypeCollection collectionType)
				{
					_columns.Add(OracleColumn.BuildColumnValueColumn(collectionType.ElementDataType));
				}
			}

			if (programMetadata.Parameters.Count <= 1)
			{
				return;
			}

			var dataTypeType = programMetadata.Parameters[0].DataType;
			if (!String.Equals(dataTypeType, OracleTypeCollection.OracleCollectionTypeNestedTable) && !String.Equals(dataTypeType, OracleTypeCollection.OracleCollectionTypeVarryingArray))
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

		public void VisitTypeReference(OracleTypeReference typeReference)
		{
			if (typeReference.SchemaObject.GetTargetSchemaObject() is OracleTypeCollection collectionType)
			{
				_columns.Add(OracleColumn.BuildColumnValueColumn(collectionType.ElementDataType));
			}
		}

		public void VisitSequenceReference(OracleSequenceReference sequenceReference)
		{
			throw new NotSupportedException();
		}

		public void VisitTableCollectionReference(OracleTableCollectionReference tableCollectionReference)
		{
			throw new NotSupportedException();
		}

		public void VisitPartitionReference(OraclePartitionReference partitionReference)
		{
			throw new NotSupportedException();
		}

		public void VisitDataObjectReference(OracleDataObjectReference dataObjectReference)
		{
			throw new NotSupportedException();
		}

		public void VisitDataTypeReference(OracleDataTypeReference dataTypeReference)
		{
			throw new NotSupportedException();
		}

		public void VisitPlSqlVariableReference(OraclePlSqlVariableReference variableReference)
		{
			throw new NotSupportedException();
		}

		public void VisitPlSqlExceptionReference(OraclePlSqlExceptionReference exceptionReference)
		{
			throw new NotSupportedException();
		}
	}
}
