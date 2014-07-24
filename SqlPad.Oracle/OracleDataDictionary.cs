using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ProtoBuf.Meta;

namespace SqlPad.Oracle
{
	public class OracleDataDictionary
	{
		private static readonly RuntimeTypeModel Serializer;

		private readonly Dictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();
		private IDictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjectsReadOnly;
		
		public DateTime Timestamp { get; private set; }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects
		{
			get
			{
				return _allObjectsReadOnly
				       ?? (_allObjectsReadOnly = new ReadOnlyDictionary<OracleObjectIdentifier, OracleSchemaObject>(_allObjects));
			}
		}

		static OracleDataDictionary()
		{
			Serializer = TypeModel.Create();
			var oracleDataDictionaryType = Serializer.Add(typeof(OracleDataDictionary), false);
			oracleDataDictionaryType.UseConstructor = false;
			oracleDataDictionaryType.Add("Timestamp", "_allObjects");
			
			var oracleObjectIdentifierType = Serializer.Add(typeof(OracleObjectIdentifier), false);
			oracleObjectIdentifierType.Add("Owner", "Name", "NormalizedOwner", "NormalizedName");

			var oracleColumnType = Serializer.Add(typeof(OracleColumn), false);
			oracleColumnType.Add("Name", "Type", "Precision", "Scale", "Size", "CharacterSize", "Nullable", "Unit");

			var oracleObjectType = Serializer.Add(typeof(OracleObject), false);
			oracleObjectType.AsReferenceDefault = true;
			oracleObjectType.Add("FullyQualifiedName");
			oracleObjectType.AddSubType(101, typeof(OracleSchemaObject));
			oracleObjectType.AddSubType(102, typeof(OracleConstraint));

			var oracleSchemaObjectType = Serializer.Add(typeof(OracleSchemaObject), false);
			oracleSchemaObjectType.AsReferenceDefault = true;
			oracleSchemaObjectType.Add("Created", "LastDdl", "IsValid", "IsTemporary", "Synonym");

			var oracleConstraintType = Serializer.Add(typeof(OracleConstraint), false);
			oracleConstraintType.AsReferenceDefault = true;
			oracleConstraintType.Add("Owner", "Columns", "IsEnabled", "IsDeferrable", "IsValidated", "IsRelied");

			oracleConstraintType.AddSubType(101, typeof(OracleCheckConstraint));
			oracleConstraintType.AddSubType(102, typeof(OracleUniqueConstraint));
			oracleConstraintType.AddSubType(103, typeof(OracleForeignKeyConstraint));

			Serializer.Add(typeof(OracleCheckConstraint), true).AsReferenceDefault = true;
			var oracleForeignKeyConstraintType = Serializer.Add(typeof(OracleForeignKeyConstraint), false);
			oracleForeignKeyConstraintType.AsReferenceDefault = true;
			oracleForeignKeyConstraintType.Add("TargetObject", "ReferenceConstraint", "DeleteRule");

			Serializer.Add(typeof(OraclePrimaryKeyConstraint), true).AsReferenceDefault = true;
			var oracleUniqueConstraintType = Serializer.Add(typeof(OracleUniqueConstraint), true);
			oracleUniqueConstraintType.AsReferenceDefault = true;
			oracleUniqueConstraintType.AddSubType(101, typeof(OraclePrimaryKeyConstraint));

			oracleSchemaObjectType.AddSubType(101, typeof(OracleTypeBase));
			oracleSchemaObjectType.AddSubType(102, typeof(OracleDataObject));
			oracleSchemaObjectType.AddSubType(103, typeof(OracleSynonym));
			oracleSchemaObjectType.AddSubType(104, typeof(OracleSequence));
			oracleSchemaObjectType.AddSubType(105, typeof(OraclePackage));
			oracleSchemaObjectType.AddSubType(106, typeof(OracleFunction));

			var oracleTypeBaseType = Serializer.Add(typeof(OracleTypeBase), true);
			oracleTypeBaseType.AsReferenceDefault = true;

			var oracleDataObjectType = Serializer.Add(typeof(OracleDataObject), false);
			oracleDataObjectType.AsReferenceDefault = true;
			oracleDataObjectType.Add("Organization", "Constraints", "Columns");

			var oracleSynonymType = Serializer.Add(typeof(OracleSynonym), false);
			oracleSynonymType.AsReferenceDefault = true;
			oracleSynonymType.Add("SchemaObject");

			var oracleSequenceType = Serializer.Add(typeof(OracleSequence), false);
			oracleSequenceType.AsReferenceDefault = true;
			oracleSequenceType.Add("CurrentValue", "Increment", "MinimumValue", "MaximumValue", "CacheSize", "IsOrdered", "CanCycle");

			var oraclePackageType = Serializer.Add(typeof(OraclePackage), false);
			oraclePackageType.AsReferenceDefault = true;
			oraclePackageType.Add("Functions");

			var oracleFunctionType = Serializer.Add(typeof(OracleFunction), false);
			oracleFunctionType.AsReferenceDefault = true;
			oracleFunctionType.Add("Metadata");

			oracleTypeBaseType.AddSubType(101, typeof(OracleObjectType));
			oracleTypeBaseType.AddSubType(102, typeof(OracleCollectionType));
			Serializer.Add(typeof(OracleObjectType), true).AsReferenceDefault = true;
			Serializer.Add(typeof(OracleCollectionType), true).AsReferenceDefault = true;

			oracleDataObjectType.AddSubType(101, typeof(OracleTable));
			oracleDataObjectType.AddSubType(102, typeof(OracleView));
			var oracleTableType = Serializer.Add(typeof(OracleTable), false);
			oracleTableType.AsReferenceDefault = true;
			oracleTableType.Add("IsInternal");

			var oracleViewType = Serializer.Add(typeof(OracleView), false);
			oracleViewType.AsReferenceDefault = true;
			oracleViewType.Add("StatementText");

			var oracleFunctionIdentifierType = Serializer.Add(typeof(OracleFunctionIdentifier), false);
			oracleFunctionIdentifierType.UseConstructor = false;
			oracleFunctionIdentifierType.Add("Owner", "Name", "Package", "Overload");

			var oracleFunctionMetadataType = Serializer.Add(typeof(OracleFunctionMetadata), false);
			oracleFunctionMetadataType.UseConstructor = false;
			oracleFunctionMetadataType.Add("Identifier", "DataType", "IsAnalytic", "IsAggregate", "IsPipelined", "IsOffloadable", "ParallelSupport", "IsDeterministic", "_metadataMinimumArguments", "_metadataMaximumArguments", "AuthId", "DisplayType", "IsBuiltIn", "Parameters");

			var oracleFunctionParameterMetadataType = Serializer.Add(typeof(OracleFunctionParameterMetadata), false);
			oracleFunctionParameterMetadataType.UseConstructor = false;
			oracleFunctionParameterMetadataType.Add("Name", "Position", "DataType", "Direction", "IsOptional");
		}

		public OracleDataDictionary(IEnumerable<KeyValuePair<OracleObjectIdentifier, OracleSchemaObject>> schemaObjects, DateTime timestamp)
		{
			_allObjects = schemaObjects.ToDictionary(schemaObject => schemaObject.Key, schemaObject => schemaObject.Value);

			Timestamp = timestamp;
		}

		protected OracleDataDictionary()
		{
		}

		public void Serialize(Stream stream)
		{
			Serializer.Serialize(stream, this);
		}

		public static OracleDataDictionary Deserialize(Stream stream)
		{
			return (OracleDataDictionary)Serializer.Deserialize(stream, null, typeof(OracleDataDictionary));
		}
	}
}
