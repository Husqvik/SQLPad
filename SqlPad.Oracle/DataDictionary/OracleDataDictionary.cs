using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ProtoBuf.Meta;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.DataDictionary
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

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects => _allObjects ?? InitialDictionary;

	    public IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks => _databaseLinks ?? InitialDatabaseLinkDictionary;

	    public ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaFunctionMetadata => _nonSchemaFunctionMetadataLookup ?? BuildNonSchemaFunctionMetadata();

	    public ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata => _builtInPackageFunctionMetadata ?? BuildBuiltInPackageFunctionMetadata();

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

		public IReadOnlyCollection<string> CharacterSets => _characterSets ?? InitialCharacterSetCollection;

	    public IDictionary<int, string> StatisticsKeys => _statisticsKeys ?? InitialStatisticsKeys;

	    public IDictionary<string, string> SystemParameters => _systemParameters ?? InitialSystemParameters;

	    static OracleDataDictionary()
		{
			Serializer = TypeModel.Create();
			var oracleDataDictionaryType = Serializer.Add(typeof(OracleDataDictionary), false);
			oracleDataDictionaryType.AsReferenceDefault = true;
			oracleDataDictionaryType.UseConstructor = false;
			oracleDataDictionaryType.Add(nameof(Timestamp), nameof(_allObjects), nameof(_databaseLinks), nameof(_nonSchemaFunctionMetadata), nameof(_characterSets), nameof(_statisticsKeys), nameof(_systemParameters));
			
			var oracleObjectIdentifierType = Serializer.Add(typeof(OracleObjectIdentifier), false);
			oracleObjectIdentifierType.Add(nameof(OracleObjectIdentifier.Owner), nameof(OracleObjectIdentifier.Name), nameof(OracleObjectIdentifier.NormalizedOwner), nameof(OracleObjectIdentifier.NormalizedName));

			var oracleColumnType = Serializer.Add(typeof(OracleColumn), false);
			oracleColumnType.UseConstructor = false;
			oracleColumnType.Add(nameof(OracleColumn.Name), nameof(OracleColumn.DataType), nameof(OracleColumn.CharacterSize), nameof(OracleColumn.Nullable), nameof(OracleColumn.Virtual), nameof(OracleColumn.DefaultValue), nameof(OracleColumn.Hidden), nameof(OracleColumn.UserGenerated));

			var oracleObjectType = Serializer.Add(typeof(OracleObject), false);
			oracleObjectType.AsReferenceDefault = true;
			oracleObjectType.Add(nameof(OracleObject.FullyQualifiedName));
			oracleObjectType.AddSubType(101, typeof(OracleSchemaObject));
			oracleObjectType.AddSubType(102, typeof(OracleConstraint));
			oracleObjectType.AddSubType(103, typeof(OracleDatabaseLink));
			oracleObjectType.AddSubType(104, typeof(OracleDataType));
			oracleObjectType.AddSubType(105, typeof(OraclePartitionBase));

			var oracleSchemaObjectType = Serializer.Add(typeof(OracleSchemaObject), false);
			oracleSchemaObjectType.AsReferenceDefault = true;
			oracleSchemaObjectType.Add(nameof(OracleSchemaObject.Created), nameof(OracleSchemaObject.LastDdl), nameof(OracleSchemaObject.IsValid), nameof(OracleSchemaObject.IsTemporary), "_synonyms");

			var oracleDatabaseLinkType = Serializer.Add(typeof(OracleDatabaseLink), false);
			oracleDatabaseLinkType.AsReferenceDefault = true;
			oracleDatabaseLinkType.Add(nameof(OracleDatabaseLink.UserName), nameof(OracleDatabaseLink.Host), nameof(OracleDatabaseLink.Created));

			var oracleOracleDataTypeType = Serializer.Add(typeof(OracleDataType), false);
			oracleOracleDataTypeType.AsReferenceDefault = true;
			oracleOracleDataTypeType.Add(nameof(OracleDataType.Length), nameof(OracleDataType.Precision), nameof(OracleDataType.Scale), nameof(OracleDataType.Unit));

			var oracleConstraintType = Serializer.Add(typeof(OracleConstraint), false);
			oracleConstraintType.AsReferenceDefault = true;
			oracleConstraintType.Add(nameof(OracleConstraint.Owner), nameof(OracleConstraint.Columns), nameof(OracleConstraint.IsEnabled), nameof(OracleConstraint.IsDeferrable), nameof(OracleConstraint.IsValidated), nameof(OracleConstraint.IsRelied));

			oracleConstraintType.AddSubType(101, typeof(OracleCheckConstraint));
			oracleConstraintType.AddSubType(102, typeof(OracleUniqueConstraint));
			oracleConstraintType.AddSubType(103, typeof(OracleReferenceConstraint));

			Serializer.Add(typeof(OracleCheckConstraint), true).AsReferenceDefault = true;
			var oracleForeignKeyConstraintType = Serializer.Add(typeof(OracleReferenceConstraint), false);
			oracleForeignKeyConstraintType.AsReferenceDefault = true;
			oracleForeignKeyConstraintType.Add(nameof(OracleReferenceConstraint.TargetObject), nameof(OracleReferenceConstraint.ReferenceConstraint), nameof(OracleReferenceConstraint.DeleteRule));

			Serializer.Add(typeof(OraclePrimaryKeyConstraint), true).AsReferenceDefault = true;
			var oracleUniqueConstraintType = Serializer.Add(typeof(OracleUniqueConstraint), true);
			oracleUniqueConstraintType.AsReferenceDefault = true;
			oracleUniqueConstraintType.AddSubType(101, typeof(OraclePrimaryKeyConstraint));

			var oraclePartitionBaseType = Serializer.Add(typeof(OraclePartitionBase), false);
			oraclePartitionBaseType.AsReferenceDefault = true;
			oraclePartitionBaseType.Add(nameof(OraclePartitionBase.Name), nameof(OraclePartitionBase.Position));

			oraclePartitionBaseType.AddSubType(101, typeof(OraclePartition));
			oraclePartitionBaseType.AddSubType(102, typeof(OracleSubPartition));

			var oraclePartitionType = Serializer.Add(typeof(OraclePartition), false);
			oraclePartitionType.AsReferenceDefault = true;
			oraclePartitionType.Add("_subPartitions");

			var oracleSubPartitionType = Serializer.Add(typeof(OracleSubPartition), false);
			oracleSubPartitionType.AsReferenceDefault = true;

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
			oracleDataObjectType.Add(nameof(OracleDataObject.Organization), nameof(OracleDataObject.Constraints), nameof(OracleDataObject.Columns));

			var oracleSynonymType = Serializer.Add(typeof(OracleSynonym), false);
			oracleSynonymType.AsReferenceDefault = true;
			oracleSynonymType.Add(nameof(OracleSynonym.SchemaObject));

			var oracleSequenceType = Serializer.Add(typeof(OracleSequence), false);
			oracleSequenceType.AsReferenceDefault = true;
			oracleSequenceType.Add(nameof(OracleSequence.CurrentValue), nameof(OracleSequence.Increment), nameof(OracleSequence.MinimumValue), nameof(OracleSequence.MaximumValue), nameof(OracleSequence.CacheSize), nameof(OracleSequence.IsOrdered), nameof(OracleSequence.CanCycle));

			var oraclePackageType = Serializer.Add(typeof(OraclePackage), false);
			oraclePackageType.AsReferenceDefault = true;
			oraclePackageType.Add("_functions");

			var oracleFunctionType = Serializer.Add(typeof(OracleFunction), false);
			oracleFunctionType.AsReferenceDefault = true;
			oracleFunctionType.Add(nameof(OracleFunction.Metadata));

			var oracleProcedureType = Serializer.Add(typeof(OracleProcedure), false);
			oracleProcedureType.AsReferenceDefault = true;
			oracleProcedureType.Add(nameof(OracleProcedure.Metadata));

			oracleTypeBaseType.AddSubType(101, typeof(OracleTypeObject));
			oracleTypeBaseType.AddSubType(102, typeof(OracleTypeCollection));
			var oracleObjectTypeType = Serializer.Add(typeof(OracleTypeObject), false);
			oracleObjectTypeType.AsReferenceDefault = true;
			oracleObjectTypeType.Add("_typeCode", nameof(OracleTypeObject.Attributes));

			var oracleObjectTypeAttributeType = Serializer.Add(typeof(OracleTypeAttribute), false);
			oracleObjectTypeAttributeType.AsReferenceDefault = true;
			oracleObjectTypeAttributeType.Add(nameof(OracleTypeAttribute.Name), nameof(OracleTypeAttribute.DataType), nameof(OracleTypeAttribute.IsInherited));
			
			var oracleCollectionType = Serializer.Add(typeof(OracleTypeCollection), false);
			oracleCollectionType.AsReferenceDefault = true;
			oracleCollectionType.Add(nameof(OracleTypeCollection.ElementDataType), nameof(OracleTypeCollection.CollectionType), nameof(OracleTypeCollection.UpperBound));

			oracleDataObjectType.AddSubType(101, typeof(OracleTable));
			oracleDataObjectType.AddSubType(102, typeof(OracleView));
			var oracleTableType = Serializer.Add(typeof(OracleTable), false);
			oracleTableType.AsReferenceDefault = true;
			oracleTableType.Add(nameof(OracleTable.IsInternal), nameof(OracleTable.Partitions), nameof(OracleTable.PartitionKeyColumns), nameof(OracleTable.SubPartitionKeyColumns));

			oracleTableType.AddSubType(101, typeof(OracleMaterializedView));
			var oracleMaterializedViewType = Serializer.Add(typeof(OracleMaterializedView), false);
			oracleMaterializedViewType.AsReferenceDefault = true;
			oracleMaterializedViewType.Add(nameof(OracleMaterializedView.TableName), nameof(OracleMaterializedView.IsUpdatable), nameof(OracleMaterializedView.IsPrebuilt), nameof(OracleMaterializedView.RefreshMode), nameof(OracleMaterializedView.RefreshType), nameof(OracleMaterializedView.RefreshMethod), nameof(OracleMaterializedView.RefreshGroup), nameof(OracleMaterializedView.LastRefresh), nameof(OracleMaterializedView.StartWith), nameof(OracleMaterializedView.Next), nameof(OracleMaterializedView.Query));

			var oracleViewType = Serializer.Add(typeof(OracleView), false);
			oracleViewType.AsReferenceDefault = true;
			oracleViewType.Add(nameof(OracleView.StatementText));

			var oracleFunctionIdentifierType = Serializer.Add(typeof(OracleProgramIdentifier), false);
			oracleFunctionIdentifierType.UseConstructor = false;
			oracleFunctionIdentifierType.Add(nameof(OracleProgramIdentifier.Owner), nameof(OracleProgramIdentifier.Name), nameof(OracleProgramIdentifier.Package), nameof(OracleProgramIdentifier.Overload));

			var oracleFunctionMetadataType = Serializer.Add(typeof(OracleProgramMetadata), false);
			oracleFunctionMetadataType.AsReferenceDefault = true;
			oracleFunctionMetadataType.UseConstructor = false;
			oracleFunctionMetadataType.Add("_parameters", nameof(OracleProgramMetadata.Identifier), nameof(OracleProgramMetadata.IsAnalytic), nameof(OracleProgramMetadata.IsAggregate), nameof(OracleProgramMetadata.IsPipelined), nameof(OracleProgramMetadata.IsOffloadable), nameof(OracleProgramMetadata.ParallelSupport), nameof(OracleProgramMetadata.IsDeterministic), "_metadataMinimumArguments", "_metadataMaximumArguments", nameof(OracleProgramMetadata.AuthId), nameof(OracleProgramMetadata.DisplayType), nameof(OracleProgramMetadata.IsBuiltIn), nameof(OracleProgramMetadata.Owner), nameof(OracleProgramMetadata.Type));

			var oracleFunctionParameterMetadataType = Serializer.Add(typeof(OracleProgramParameterMetadata), false);
			oracleFunctionMetadataType.AsReferenceDefault = true;
			oracleFunctionParameterMetadataType.UseConstructor = false;
			oracleFunctionParameterMetadataType.Add(nameof(OracleProgramParameterMetadata.Name), nameof(OracleProgramParameterMetadata.Position), nameof(OracleProgramParameterMetadata.Sequence), nameof(OracleProgramParameterMetadata.DataLevel), nameof(OracleProgramParameterMetadata.DataType), nameof(OracleProgramParameterMetadata.CustomDataType), nameof(OracleProgramParameterMetadata.Direction), nameof(OracleProgramParameterMetadata.IsOptional));
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
