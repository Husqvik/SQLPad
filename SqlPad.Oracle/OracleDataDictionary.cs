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

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> InitialDictionary = BuildEmptyReadOnlyDictionary<OracleObjectIdentifier, OracleSchemaObject>();
		private static readonly IDictionary<OracleObjectIdentifier, OracleDatabaseLink> InitialDatabaseLinkDictionary = BuildEmptyReadOnlyDictionary<OracleObjectIdentifier, OracleDatabaseLink>();
		private static readonly IDictionary<int, string> InitialStatisticsKeys = BuildEmptyReadOnlyDictionary<int, string>();
		private static readonly IDictionary<string, string> InitialSystemParameters = BuildEmptyReadOnlyDictionary<string, string>();
		private static readonly HashSet<string> InitialCharacterSetCollection = new HashSet<string>();

		public static readonly OracleDataDictionary EmptyDictionary = new OracleDataDictionary();

		private readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects;
		private readonly IDictionary<OracleObjectIdentifier, OracleDatabaseLink> _databaseLinks;
		private readonly ICollection<OracleProgramMetadata> _nonSchemaFunctionMetadata;
		private readonly IDictionary<int, string> _statisticsKeys;
		private readonly IDictionary<string, string> _systemParameters;
		private readonly HashSet<string> _characterSets;

		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> _nonSchemaFunctionMetadataLookup;
		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> _builtInPackageFunctionMetadata;

		public DateTime Timestamp { get; private set; }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects
		{
			get { return _allObjects ?? InitialDictionary; }
		}

		public IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks
		{
			get { return _databaseLinks ?? InitialDatabaseLinkDictionary; }
		}

		public ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaFunctionMetadata
		{
			get { return _nonSchemaFunctionMetadataLookup ?? BuildNonSchemaFunctionMetadata(); }
		}

		public ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata
		{
			get { return _builtInPackageFunctionMetadata ?? BuildBuiltInPackageFunctionMetadata(); }
		}

		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuildBuiltInPackageFunctionMetadata()
		{
			OracleSchemaObject standardPackage;
			var functions = AllObjects.TryGetValue(OracleDatabaseModelBase.BuiltInFunctionPackageIdentifier, out standardPackage)
				? ((OraclePackage)standardPackage).Functions
				: new List<OracleProgramMetadata>();

			return _builtInPackageFunctionMetadata = functions.ToLookup(m => m.Identifier);
		}

		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuildNonSchemaFunctionMetadata()
		{
			var nonSchemaFunctionMetadata = _nonSchemaFunctionMetadata ?? Enumerable.Empty<OracleProgramMetadata>();
			return _nonSchemaFunctionMetadataLookup = nonSchemaFunctionMetadata.ToLookup(m => m.Identifier);
		}

		public ICollection<string> CharacterSets
		{
			get { return _characterSets ?? InitialCharacterSetCollection; }
		}

		public IDictionary<int, string> StatisticsKeys
		{
			get { return _statisticsKeys ?? InitialStatisticsKeys; }
		}

		public IDictionary<string, string> SystemParameters
		{
			get { return _systemParameters ?? InitialSystemParameters; }
		}

		static OracleDataDictionary()
		{
			Serializer = TypeModel.Create();
			var oracleDataDictionaryType = Serializer.Add(typeof(OracleDataDictionary), false);
			oracleDataDictionaryType.AsReferenceDefault = true;
			oracleDataDictionaryType.UseConstructor = false;
			oracleDataDictionaryType.Add("Timestamp", "_allObjects", "_databaseLinks", "_nonSchemaFunctionMetadata", "_characterSets", "_statisticsKeys", "_systemParameters");
			
			var oracleObjectIdentifierType = Serializer.Add(typeof(OracleObjectIdentifier), false);
			oracleObjectIdentifierType.Add("Owner", "Name", "NormalizedOwner", "NormalizedName");

			var oracleColumnType = Serializer.Add(typeof(OracleColumn), false);
			oracleColumnType.Add("Name", "DataType", "CharacterSize", "Nullable");

			var oracleObjectType = Serializer.Add(typeof(OracleObject), false);
			oracleObjectType.AsReferenceDefault = true;
			oracleObjectType.Add("FullyQualifiedName");
			oracleObjectType.AddSubType(101, typeof(OracleSchemaObject));
			oracleObjectType.AddSubType(102, typeof(OracleConstraint));
			oracleObjectType.AddSubType(103, typeof(OracleDatabaseLink));
			oracleObjectType.AddSubType(104, typeof(OracleDataType));

			var oracleSchemaObjectType = Serializer.Add(typeof(OracleSchemaObject), false);
			oracleSchemaObjectType.AsReferenceDefault = true;
			oracleSchemaObjectType.Add("Created", "LastDdl", "IsValid", "IsTemporary", "_synonyms");

			var oracleDatabaseLinkType = Serializer.Add(typeof(OracleDatabaseLink), false);
			oracleDatabaseLinkType.AsReferenceDefault = true;
			oracleDatabaseLinkType.Add("UserName", "Host", "Created");

			var oracleOracleDataTypeType = Serializer.Add(typeof(OracleDataType), false);
			oracleOracleDataTypeType.AsReferenceDefault = true;
			oracleOracleDataTypeType.Add("Length", "Precision", "Scale", "Unit");

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
			oracleSchemaObjectType.AddSubType(107, typeof(OracleProcedure));

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
			oraclePackageType.Add("_functions");

			var oracleFunctionType = Serializer.Add(typeof(OracleFunction), false);
			oracleFunctionType.AsReferenceDefault = true;
			oracleFunctionType.Add("Metadata");

			var oracleProcedureType = Serializer.Add(typeof(OracleProcedure), false);
			oracleProcedureType.AsReferenceDefault = true;
			oracleProcedureType.Add("Metadata");

			oracleTypeBaseType.AddSubType(101, typeof(OracleTypeObject));
			oracleTypeBaseType.AddSubType(102, typeof(OracleTypeCollection));
			var oracleObjectTypeType = Serializer.Add(typeof(OracleTypeObject), false);
			oracleObjectTypeType.AsReferenceDefault = true;
			oracleObjectTypeType.Add("_typeCode", "Attributes");

			var oracleObjectTypeAttributeType = Serializer.Add(typeof(OracleTypeAttribute), false);
			oracleObjectTypeAttributeType.AsReferenceDefault = true;
			oracleObjectTypeAttributeType.Add("Name", "DataType", "IsInherited");
			
			var oracleCollectionType = Serializer.Add(typeof(OracleTypeCollection), false);
			oracleCollectionType.AsReferenceDefault = true;
			oracleCollectionType.Add("ElementDataType", "CollectionType", "UpperBound");

			oracleDataObjectType.AddSubType(101, typeof(OracleTable));
			oracleDataObjectType.AddSubType(102, typeof(OracleView));
			var oracleTableType = Serializer.Add(typeof(OracleTable), false);
			oracleTableType.AsReferenceDefault = true;
			oracleTableType.Add("IsInternal");

			oracleTableType.AddSubType(101, typeof(OracleMaterializedView));
			var oracleMaterializedViewType = Serializer.Add(typeof(OracleMaterializedView), false);
			oracleMaterializedViewType.AsReferenceDefault = true;
			oracleMaterializedViewType.Add("TableName", "IsUpdatable", "IsPrebuilt", "RefreshMode", "RefreshType", "RefreshMethod", "RefreshGroup", "LastRefresh", "StartWith", "Next", "Query");

			var oracleViewType = Serializer.Add(typeof(OracleView), false);
			oracleViewType.AsReferenceDefault = true;
			oracleViewType.Add("StatementText");

			var oracleFunctionIdentifierType = Serializer.Add(typeof(OracleProgramIdentifier), false);
			oracleFunctionIdentifierType.UseConstructor = false;
			oracleFunctionIdentifierType.Add("Owner", "Name", "Package", "Overload");

			var oracleFunctionMetadataType = Serializer.Add(typeof(OracleProgramMetadata), false);
			oracleFunctionMetadataType.AsReferenceDefault = true;
			oracleFunctionMetadataType.UseConstructor = false;
			oracleFunctionMetadataType.Add("_parameters", "Identifier", "IsAnalytic", "IsAggregate", "IsPipelined", "IsOffloadable", "ParallelSupport", "IsDeterministic", "_metadataMinimumArguments", "_metadataMaximumArguments", "AuthId", "DisplayType", "IsBuiltIn", "Owner", "Type");

			var oracleFunctionParameterMetadataType = Serializer.Add(typeof(OracleProgramParameterMetadata), false);
			oracleFunctionMetadataType.AsReferenceDefault = true;
			oracleFunctionParameterMetadataType.UseConstructor = false;
			oracleFunctionParameterMetadataType.Add("Name", "Position", "Sequence", "DataLevel", "DataType", "CustomDataType", "Direction", "IsOptional");
		}

		public OracleDataDictionary(IDictionary<OracleObjectIdentifier, OracleSchemaObject> schemaObjects, IDictionary<OracleObjectIdentifier, OracleDatabaseLink> databaseLinks, IEnumerable<OracleProgramMetadata> nonSchemaFunctionMetadata, IEnumerable<string> characterSets, IDictionary<int, string> statisticsKeys, IDictionary<string, string> systemParameters, DateTime timestamp)
		{
			_allObjects = new ReadOnlyDictionary<OracleObjectIdentifier, OracleSchemaObject>(schemaObjects);
			_databaseLinks = new ReadOnlyDictionary<OracleObjectIdentifier, OracleDatabaseLink>(databaseLinks);
			_nonSchemaFunctionMetadata = new List<OracleProgramMetadata>(nonSchemaFunctionMetadata);
			_nonSchemaFunctionMetadataLookup = _nonSchemaFunctionMetadata.ToLookup(m => m.Identifier);
			_characterSets = new HashSet<string>(characterSets);
			_statisticsKeys = new ReadOnlyDictionary<int, string>(statisticsKeys);
			_systemParameters = new ReadOnlyDictionary<string, string>(systemParameters);

			Timestamp = timestamp;
		}

		private OracleDataDictionary()
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

		private static IDictionary<TKey, TValue> BuildEmptyReadOnlyDictionary<TKey, TValue>()
		{
			return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>());
		}
	}
}
