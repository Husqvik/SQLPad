using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;
using ParameterDirection = SqlPad.Oracle.DataDictionary.ParameterDirection;

namespace SqlPad.Oracle.DatabaseConnection
{
	internal class OracleDataDictionaryMapper
	{
		private readonly OracleDatabaseModel _databaseModel;
		private readonly Action<string> _actionStatusChanged;

		public const int LongFetchSize = 4096;

		public OracleDataDictionaryMapper(OracleDatabaseModel databaseModel, Action<string> actionStatusChanged)
		{
			_actionStatusChanged = actionStatusChanged;
			_databaseModel = databaseModel;
		}

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> BuildDataDictionary()
		{
			var allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();
			var databaseVersion = _databaseModel.Version;

			NotifyStatus("Types and materialized views... ");

			var stopwatch = Stopwatch.StartNew();
			var schemaTypeMetadataSource = _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectTypesCommandText, MapSchemaType);
			var schemaTypeAndMateralizedViewMetadataSource = schemaTypeMetadataSource.Concat(_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectMaterializedViewCommandText, MapMaterializedView));

			foreach (var schemaObject in schemaTypeAndMateralizedViewMetadataSource)
			{
				AddObjectToDictionary(allObjects, schemaObject, schemaObject.Type);
			}

			Trace.WriteLine($"Fetch types and materialized views metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Other schema objects... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectAllObjectsCommandText, r => MapSchemaObject(r, allObjects)).Count();

			Trace.WriteLine($"Fetch all objects metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Table metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectTablesCommandText, r => MapTable(r, allObjects)).Count();

			Trace.WriteLine($"Fetch table metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Partition metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectPartitionsCommandText, r => MapPartitions(r, allObjects)).Count();

			Trace.WriteLine($"Fetch partition metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Sub-partition metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectSubPartitionsCommandText, r => MapSubPartitions(r, allObjects)).Count();

			Trace.WriteLine($"Fetch sub-partition metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Partition key metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectTablePartitionKeysCommandText, r => MapPartitionKeys(r, allObjects, t => t.PartitionKeyColumns)).Count();

			Trace.WriteLine($"Fetch partition key metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Sub-partition key metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectTableSubPartitionKeysCommandText, r => MapPartitionKeys(r, allObjects, t => t.SubPartitionKeyColumns)).Count();

			Trace.WriteLine($"Fetch sub-partition key metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Synonym metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectSynonymTargetsCommandText, r => MapSynonymTarget(r, allObjects)).Count();

			Trace.WriteLine($"Fetch synonyms metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Table column metadata... ");

			stopwatch.Restart();

			var columnMetadataSource = _databaseModel.ExecuteReader(() => String.Format(OracleDatabaseCommands.SelectTableColumnsCommandTextBase, databaseVersion.Major >= 12 ? ", HIDDEN_COLUMN, USER_GENERATED" : null), r => MapTableColumn(r, databaseVersion));

			foreach (var columnMetadata in columnMetadataSource)
			{
				OracleSchemaObject schemaObject;
				if (!allObjects.TryGetValue(columnMetadata.Key, out schemaObject))
					continue;

				var dataObject = (OracleDataObject)schemaObject;
				dataObject.Columns.Add(columnMetadata.Value.Name, columnMetadata.Value);
			}

			Trace.WriteLine($"Fetch table column metadata in {stopwatch.Elapsed}. ");

			NotifyStatus("Constraint metadata... ");

			stopwatch.Restart();

			var constraintSource = _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectConstraintsCommandText, r => MapConstraintWithReferenceIdentifier(r, allObjects))
				.Where(c => c.Key != null)
				.ToArray();

			var constraints = new Dictionary<OracleObjectIdentifier, OracleConstraint>();
			foreach (var constraintPair in constraintSource)
			{
				AddObjectToDictionary(constraints, constraintPair.Key, "CONSTRAINT");
			}

			Trace.WriteLine($"Fetch constraint metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Constraint column metadata... ");

			stopwatch.Restart();

			var constraintColumns = _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectConstraintColumnsCommandText, MapConstraintColumn)
				.GroupBy(c => c.Key)
				.ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value).ToList());

			foreach (var constraintPair in constraintSource)
			{
				OracleConstraint constraint;
				if (!constraints.TryGetValue(constraintPair.Key.FullyQualifiedName, out constraint))
				{
					continue;
				}

				List<string> columns;
				if (constraintColumns.TryGetValue(constraintPair.Key.FullyQualifiedName, out columns))
				{
					constraint.Columns = columns.AsReadOnly();
				}

				var referenceConstraint = constraintPair.Key as OracleReferenceConstraint;
				if (referenceConstraint == null)
				{
					continue;
				}

				var referencedUniqueConstraint = (OracleUniqueConstraint)constraints[constraintPair.Value];
				referenceConstraint.TargetObject = referencedUniqueConstraint.OwnerObject;
				referenceConstraint.ReferenceConstraint = referencedUniqueConstraint;
			}

			Trace.WriteLine($"Fetch column constraint metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Sequence metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectSequencesCommandText, r => MapSequence(r, allObjects)).Count();

			Trace.WriteLine($"Fetch sequence metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Object type attribute metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectTypeAttributesCommandText, r => MapTypeAttributes(r, allObjects)).Count();

			Trace.WriteLine($"Fetch object type attribute metadata finished in {stopwatch.Elapsed}. ");

			NotifyStatus("Collection type attribute metadata... ");

			stopwatch.Restart();

			_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectCollectionTypeAttributesCommandText, r => MapCollectionTypeAttributes(r, allObjects)).Count();

			Trace.WriteLine($"Fetch collection type attribute metadata finished in {stopwatch.Elapsed}. ");

			return new ReadOnlyDictionary<OracleObjectIdentifier, OracleSchemaObject>(allObjects);
		}

		private void NotifyStatus(string message)
		{
			_actionStatusChanged?.Invoke(message);
		}

		public ILookup<OracleProgramIdentifier, OracleProgramMetadata> GetUserFunctionMetadata()
		{
			NotifyStatus("User function metadata... ");
			var stopwatch = Stopwatch.StartNew();
			var metadata = GetFunctionMetadataCollection(OracleDatabaseCommands.SelectUserFunctionMetadataCommandText, OracleDatabaseCommands.SelectUserFunctionParameterMetadataCommandText, false);
			Trace.WriteLine($"GetUserFunctionMetadata finished in {stopwatch.Elapsed}. ");
			return metadata;
		}

		public ILookup<OracleProgramIdentifier, OracleProgramMetadata> GetBuiltInFunctionMetadata()
		{
			NotifyStatus("Built-in function metadata... ");
			var stopwatch = Stopwatch.StartNew();
			var metadata = GetFunctionMetadataCollection(OracleDatabaseCommands.SelectBuiltInProgramMetadataCommandText, OracleDatabaseCommands.SelectBuiltInFunctionParameterMetadataCommandText, true);
			Trace.WriteLine($"GetBuiltInFunctionMetadata finished in {stopwatch.Elapsed}. ");
			return metadata;
		}

		public Task<ILookup<string, string>> GetContextData(CancellationToken cancellationToken)
		{
			var result = _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectContextDataCommandText, MapContextData).ToLookup(r => r.Key, r => r.Value);
			return Task.FromResult(result);
		}

		public Task<IReadOnlyList<string>> GetWeekdayNames(CancellationToken cancellationToken)
		{
			var weekdays = (IReadOnlyList<string>)_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectWeekdayNamesCommandText, r => (string)r["WEEKDAY"]).ToArray();
			return Task.FromResult(weekdays);
		}

		public IEnumerable<OracleSchema> GetSchemaNames()
		{
			var databaseVersion = _databaseModel.Version;
			return _databaseModel.ExecuteReader(() => String.Format(OracleDatabaseCommands.SelectAllSchemasCommandTextBase, databaseVersion.Major >= 12 ? ", COMMON, ORACLE_MAINTAINED" : null), r => MapSchema(r, databaseVersion));
		}

		private static OracleSchema MapSchema(IDataRecord reader, Version version)
		{
			var schema =
				new OracleSchema
				{
					Name = QualifyStringObject((string)reader["USERNAME"]),
					Created = (DateTime)reader["CREATED"]
				};

			if (version.Major >= 12)
			{
				schema.IsOracleMaintained = String.Equals((string)reader["ORACLE_MAINTAINED"], "Y");
				schema.IsCommon = String.Equals((string)reader["COMMON"], "YES");
			}

			return schema;
		}

		public IEnumerable<string> GetCharacterSets()
		{
			return _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectCharacterSetsCommandText, r => ((string)r["VALUE"]));
		}

		public IEnumerable<KeyValuePair<int, string>> GetStatisticsKeys()
		{
			var command = _databaseModel.Version.Major >= OracleDatabaseModelBase.VersionMajorOracle12C
				? OracleDatabaseCommands.SelectStatisticsKeysCommandText
				: OracleDatabaseCommands.SelectStatisticsKeysOracle11CommandText;

			return _databaseModel.ExecuteReader(() => command, MapStatisticsKey);
		}

		public IDictionary<OracleObjectIdentifier, OracleDatabaseLink> GetDatabaseLinks()
		{
			return _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectDatabaseLinksCommandText, MapDatabaseLink)
				.ToDictionary(l => l.FullyQualifiedName, l => l);
		}

		public IEnumerable<KeyValuePair<string, string>> GetSystemParameters()
		{
			return _databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectSystemParametersCommandText, MapParameter);
		}

		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> GetFunctionMetadataCollection(string selectFunctionMetadataCommandText, string selectParameterMetadataCommandText, bool isBuiltIn)
		{
			var metadataDictionary = new Dictionary<int, OracleProgramMetadata>();
			var functionMetadataSource = _databaseModel.ExecuteReader(() => selectFunctionMetadataCommandText, r => MapProgramMetadata(r, isBuiltIn, metadataDictionary));
			var functionMetadataLookup = functionMetadataSource.ToLookup(m => m.Identifier);

			var functionParameterMetadataSource = _databaseModel.ExecuteReader(() => selectParameterMetadataCommandText, MapProgramParameterMetadata);
			foreach (var functionIdentifierParameterMetadata in functionParameterMetadataSource)
			{
				var functionMetadata = functionMetadataLookup[functionIdentifierParameterMetadata.Key]
					.SingleOrDefault(m => m.Identifier.Overload == functionIdentifierParameterMetadata.Key.Overload);

				functionMetadata?.AddParameter(functionIdentifierParameterMetadata.Value);
			}

			if (metadataDictionary.Count > 0)
			{
				_databaseModel.ExecuteReader(() => OracleDatabaseCommands.SelectSqlFunctionParameterMetadataCommandText, r => MapSqlParameterMetadata(r, metadataDictionary)).Count();
			}

			return functionMetadataLookup;
		}

		private static KeyValuePair<int, string> MapStatisticsKey(IDataRecord reader)
		{
			return new KeyValuePair<int, string>(Convert.ToInt32(reader["STATISTIC#"]), (string)reader["DISPLAY_NAME"]);
		}

		private static KeyValuePair<string, string> MapContextData(IDataRecord reader)
		{
			return new KeyValuePair<string, string>((string)reader["NAMESPACE"], (string)reader["ATTRIBUTE"]);
		}

		private static OracleProgramParameterMetadata MapSqlParameterMetadata(IDataRecord reader, IReadOnlyDictionary<int, OracleProgramMetadata> metadataDictionary)
		{
			var functionId = Convert.ToInt32(reader["FUNCTION_ID"]);
			OracleProgramMetadata metadata;
			if (!metadataDictionary.TryGetValue(functionId, out metadata))
			{
				return null;
			}

			var position = Convert.ToInt32(reader["POSITION"]);
			var dataType = (string)reader["DATATYPE"];

			var parameterMetadata = new OracleProgramParameterMetadata(null, position, position, 0, ParameterDirection.Input, dataType, OracleObjectIdentifier.Empty, false);
			metadata.AddParameter(parameterMetadata);

			return parameterMetadata;
		}

		private static OracleProgramMetadata MapProgramMetadata(IDataRecord reader, bool isBuiltIn, IDictionary<int, OracleProgramMetadata> metadataDictionary)
		{
			var functionId = OracleReaderValueConvert.ToInt32(reader["FUNCTION_ID"]);
			var identifier = CreateFunctionIdentifierFromReaderValues(reader["OWNER"], reader["PACKAGE_NAME"], reader["PROGRAM_NAME"], reader["OVERLOAD"]);
			var type = Convert.ToBoolean(reader["IS_FUNCTION"]) ? ProgramType.Function : ProgramType.Procedure;
			var isAnalytic = String.Equals((string)reader["ANALYTIC"], "YES");
			var isAggregate = String.Equals((string)reader["AGGREGATE"], "YES");
			var isPipelined = String.Equals((string)reader["PIPELINED"], "YES");
			var isOffloadable = String.Equals((string)reader["OFFLOADABLE"], "YES");
			var parallelSupport = String.Equals((string)reader["PARALLEL"], "YES");
			var isDeterministic = String.Equals((string)reader["DETERMINISTIC"], "YES");
			var metadataMinimumArguments = OracleReaderValueConvert.ToInt32(reader["MINARGS"]);
			var metadataMaximumArguments = OracleReaderValueConvert.ToInt32(reader["MAXARGS"]);
			var authId = String.Equals((string)reader["AUTHID"], "CURRENT_USER") ? AuthId.CurrentUser : AuthId.Definer;
			var displayType = (string)reader["DISP_TYPE"];

			var metadata = new OracleProgramMetadata(type, identifier, isAnalytic, isAggregate, isPipelined, isOffloadable, parallelSupport, isDeterministic, metadataMinimumArguments, metadataMaximumArguments, authId, displayType, isBuiltIn);
			if (functionId.HasValue)
			{
				metadataDictionary.Add(functionId.Value, metadata);
			}

			return metadata;
		}

		private static KeyValuePair<OracleProgramIdentifier, OracleProgramParameterMetadata> MapProgramParameterMetadata(IDataRecord reader)
		{
			var identifier = CreateFunctionIdentifierFromReaderValues(reader["OWNER"], reader["PACKAGE_NAME"], reader["PROGRAM_NAME"], reader["OVERLOAD"]);

			var parameterName = OracleReaderValueConvert.ToString(reader["ARGUMENT_NAME"]);
			var position = Convert.ToInt32(reader["POSITION"]);
			var sequence = Convert.ToInt32(reader["SEQUENCE"]);
			var dataLevel = Convert.ToInt32(reader["DATA_LEVEL"]);
			var dataType = OracleReaderValueConvert.ToString(reader["DATA_TYPE"]);
			var typeOwner = OracleReaderValueConvert.ToString(reader["TYPE_OWNER"]);
			var typeName = OracleReaderValueConvert.ToString(reader["TYPE_NAME"]);
			var isOptional = String.Equals((string)reader["DEFAULTED"], "Y");
			var directionRaw = (string)reader["IN_OUT"];
			ParameterDirection direction;
			switch (directionRaw)
			{
				case "IN":
					direction = ParameterDirection.Input;
					break;
				case "OUT":
					direction = String.IsNullOrEmpty(parameterName) ? ParameterDirection.ReturnValue : ParameterDirection.Output;
					break;
				case "IN/OUT":
					direction = ParameterDirection.InputOutput;
					break;
				default:
					throw new NotSupportedException($"Parameter direction '{directionRaw}' is not supported. ");
			}

			return new KeyValuePair<OracleProgramIdentifier, OracleProgramParameterMetadata>(
				identifier, new OracleProgramParameterMetadata(QualifyStringObject(parameterName), position, sequence, dataLevel, direction, dataType, OracleObjectIdentifier.Create(typeOwner, typeName), isOptional));
		}

		private static OracleProgramIdentifier CreateFunctionIdentifierFromReaderValues(object owner, object package, object name, object overload)
		{
			return OracleProgramIdentifier.CreateFromValues(owner == DBNull.Value ? null : QualifyStringObject(owner), package == DBNull.Value ? null : QualifyStringObject(package), QualifyStringObject(name), Convert.ToInt32(overload));
		}

		private static OracleTypeObject MapTypeAttributes(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var typeFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TYPE_NAME"]));
			OracleSchemaObject typeObject;
			if (!allObjects.TryGetValue(typeFullyQualifiedName, out typeObject))
				return null;

			var type = (OracleTypeObject)typeObject;
			var attributeTypeIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(reader["ATTR_TYPE_OWNER"]), QualifyStringObject(reader["ATTR_TYPE_NAME"]));

			var dataType =
				new OracleDataType
				{
					FullyQualifiedName = attributeTypeIdentifier,
					Length = OracleReaderValueConvert.ToInt32(reader["LENGTH"]),
					Precision = OracleReaderValueConvert.ToInt32(reader["PRECISION"]),
					Scale = OracleReaderValueConvert.ToInt32(reader["SCALE"])
				};

			ResolveDataUnit(dataType, reader["CHAR_USED"]);

			var attribute =
				new OracleTypeAttribute
				{
					Name = QualifyStringObject(reader["ATTR_NAME"]),
					DataType = dataType,
					IsInherited = String.Equals((string)reader["INHERITED"], "YES")
				};
			
			type.Attributes.Add(attribute);

			return type;
		}

		private static OracleTypeCollection MapCollectionTypeAttributes(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var typeFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TYPE_NAME"]));
			OracleSchemaObject typeObject;
			if (!allObjects.TryGetValue(typeFullyQualifiedName, out typeObject))
				return null;

			var collectionType = (OracleTypeCollection)typeObject;
			var elementTypeIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(reader["ELEM_TYPE_OWNER"]), QualifyStringObject(reader["ELEM_TYPE_NAME"]));

			var dataType =
				new OracleDataType
				{
					FullyQualifiedName = elementTypeIdentifier,
					Length = OracleReaderValueConvert.ToInt32(reader["LENGTH"]),
					Precision = OracleReaderValueConvert.ToInt32(reader["PRECISION"]),
					Scale = OracleReaderValueConvert.ToInt32(reader["SCALE"])
				};

			ResolveDataUnit(dataType, reader["CHARACTER_SET_NAME"]);

			collectionType.ElementDataType = dataType;
			collectionType.CollectionType = (string)reader["COLL_TYPE"] == OracleTypeCollection.OracleCollectionTypeNestedTable ? OracleCollectionType.Table : OracleCollectionType.VarryingArray;
			collectionType.UpperBound = OracleReaderValueConvert.ToInt32(reader["UPPER_BOUND"]);

			return collectionType;
		}

		private static OracleDatabaseLink MapDatabaseLink(IDataRecord reader)
		{
			var databaseLinkFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["DB_LINK"]));
			return
				new OracleDatabaseLink
				{
					FullyQualifiedName = databaseLinkFullyQualifiedName,
					Created = (DateTime)reader["CREATED"],
					Host = (string)reader["HOST"],
					UserName = OracleReaderValueConvert.ToString(reader["USERNAME"])
				};
		}

		private static KeyValuePair<string, string> MapParameter(IDataRecord reader)
		{
			return new KeyValuePair<string, string>((string)reader["NAME"], OracleReaderValueConvert.ToString(reader["VALUE"]));
		}

		private static OracleSequence MapSequence(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var sequenceFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["SEQUENCE_OWNER"]), QualifyStringObject(reader["SEQUENCE_NAME"]));
			OracleSchemaObject sequenceObject;
			if (!allObjects.TryGetValue(sequenceFullyQualifiedName, out sequenceObject))
				return null;

			var sequence = (OracleSequence)sequenceObject;
			sequence.CurrentValue = Convert.ToDecimal(reader["LAST_NUMBER"]);
			sequence.MinimumValue = Convert.ToDecimal(reader["MIN_VALUE"]);
			sequence.MaximumValue = Convert.ToDecimal(reader["MAX_VALUE"]);
			sequence.Increment = Convert.ToDecimal(reader["INCREMENT_BY"]);
			sequence.CacheSize = Convert.ToDecimal(reader["CACHE_SIZE"]);
			sequence.CanCycle = String.Equals((string)reader["CYCLE_FLAG"], "Y");
			sequence.IsOrdered = String.Equals((string)reader["ORDER_FLAG"], "Y");

			return sequence;
		}

		private static KeyValuePair<OracleObjectIdentifier, string> MapConstraintColumn(IDataRecord reader)
		{
			var column = (string)reader["COLUMN_NAME"];
			return new KeyValuePair<OracleObjectIdentifier, string>(OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["CONSTRAINT_NAME"])), column[0] == '"' ? column : QualifyStringObject(column));
		}

		private static KeyValuePair<OracleConstraint, OracleObjectIdentifier> MapConstraintWithReferenceIdentifier(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var remoteConstraintIdentifier = OracleObjectIdentifier.Empty;
			var owner = QualifyStringObject(reader["OWNER"]);
			var ownerObjectFullyQualifiedName = OracleObjectIdentifier.Create(owner, QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject ownerObject;
			if (!allObjects.TryGetValue(ownerObjectFullyQualifiedName, out ownerObject))
			{
				return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(null, remoteConstraintIdentifier);
			}

			var rely = OracleReaderValueConvert.ToString(reader["RELY"]);
			var constraint = OracleObjectFactory.CreateConstraint((string)reader["CONSTRAINT_TYPE"], owner, QualifyStringObject(reader["CONSTRAINT_NAME"]), (string)reader["STATUS"] == "ENABLED", (string)reader["VALIDATED"] == "VALIDATED", (string)reader["DEFERRABLE"] == "DEFERRABLE", rely == "RELY");
			var dataObject = (OracleDataObject)ownerObject;
			constraint.OwnerObject = dataObject;
			dataObject.Constraints.Add(constraint);

			var referenceConstraint = constraint as OracleReferenceConstraint;
			if (referenceConstraint != null)
			{
				var cascadeAction = DeleteRule.None;
				switch ((string)reader["DELETE_RULE"])
				{
					case "CASCADE":
						cascadeAction = DeleteRule.Cascade;
						break;
					case "SET NULL":
						cascadeAction = DeleteRule.SetNull;
						break;
					case "NO ACTION":
						break;
				}

				referenceConstraint.DeleteRule = cascadeAction;
				remoteConstraintIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(reader["R_OWNER"]), QualifyStringObject(reader["R_CONSTRAINT_NAME"]));
			}

			return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(constraint, remoteConstraintIdentifier);
		}

		private static KeyValuePair<OracleObjectIdentifier, OracleColumn> MapTableColumn(IDataRecord reader, Version version)
		{
			var dataTypeIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(reader["DATA_TYPE_OWNER"]), QualifyStringObject(reader["DATA_TYPE"]));
			var dataType =
				new OracleDataType
				{
					FullyQualifiedName = dataTypeIdentifier,
					Length = Convert.ToInt32(reader["DATA_LENGTH"]),
					Precision = OracleReaderValueConvert.ToInt32(reader["DATA_PRECISION"]),
					Scale = OracleReaderValueConvert.ToInt32(reader["DATA_SCALE"])
				};

			ResolveDataUnit(dataType, reader["CHAR_USED"]);

			var column =
				new OracleColumn
				{
					Name = QualifyStringObject(reader["COLUMN_NAME"]),
					DataType = dataType,
					Nullable = String.Equals((string)reader["NULLABLE"], "Y"),
					Virtual = String.Equals((string)reader["VIRTUAL_COLUMN"], "YES"),
					DefaultValue = OracleReaderValueConvert.ToString(reader["DATA_DEFAULT"]),
					CharacterSize = Convert.ToInt32(reader["CHAR_LENGTH"]),
					UserGenerated = true
				};

			if (version.Major >= 12)
			{
				column.Hidden = String.Equals((string)reader["HIDDEN_COLUMN"], "YES");
				column.UserGenerated = String.Equals((string)reader["USER_GENERATED"], "YES");
			}
			
			return new KeyValuePair<OracleObjectIdentifier, OracleColumn>(
				OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TABLE_NAME"])), column);
		}

		private static void ResolveDataUnit(OracleDataType dataType, object characterUsedValue)
		{
			dataType.Unit = !dataType.FullyQualifiedName.HasOwner && dataType.FullyQualifiedName.NormalizedName.In("\"VARCHAR\"", "\"VARCHAR2\"")
				? String.Equals((string)characterUsedValue, "C") ? DataUnit.Character : DataUnit.Byte
				: DataUnit.NotApplicable;
		}

		private static OracleSchemaObject MapSynonymTarget(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var synonymFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["SYNONYM_NAME"]));
			OracleSchemaObject synonymObject;
			if (!allObjects.TryGetValue(synonymFullyQualifiedName, out synonymObject))
			{
				return null;
			}

			var objectFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["TABLE_OWNER"]), QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject schemaObject;
			if (!allObjects.TryGetValue(objectFullyQualifiedName, out schemaObject))
			{
				return null;
			}

			var synonym = (OracleSynonym)synonymObject;
			synonym.SchemaObject = schemaObject;
			schemaObject.Synonyms.Add(synonym);

			return synonymObject;
		}

		private static OracleTable MapTable(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject schemaObject;
			if (!allObjects.TryGetValue(tableFullyQualifiedName, out schemaObject))
			{
				return null;
			}

			var table = (OracleTable)schemaObject;
			table.Organization = (OrganizationType)Enum.Parse(typeof(OrganizationType), (string)reader["ORGANIZATION"]);
			return table;
		}

		private static OraclePartition MapPartitions(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var ownerTable = GetTableForPartition(reader, allObjects);
			if (ownerTable == null)
			{
				return null;
			}

			var partition =
				new OraclePartition
				{
					Name = QualifyStringObject(reader["PARTITION_NAME"]),
					Position = Convert.ToInt32(reader["PARTITION_POSITION"])
				};

			ownerTable.Partitions.Add(partition.Name, partition);

			return partition;
		}

		private static OracleTable MapPartitionKeys(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects, Func<OracleTable, ICollection<string>> getKeyCollectionFunction)
		{
			var ownerTable = GetTableForPartition(reader, allObjects);
			if (ownerTable == null)
			{
				return null;
			}

			getKeyCollectionFunction(ownerTable).Add(QualifyStringObject(reader["COLUMN_NAME"]));

			return ownerTable;
		}

		private static OracleTable GetTableForPartition(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["TABLE_OWNER"]), QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject schemaObject;
			return allObjects.TryGetValue(tableFullyQualifiedName, out schemaObject)
				? (OracleTable)schemaObject
				: null;
		}

		private OracleSubPartition MapSubPartitions(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var ownerTable = GetTableForPartition(reader, allObjects);
			if (ownerTable == null)
			{
				return null;
			}

			var subPartition =
				new OracleSubPartition
				{
					Name = QualifyStringObject(reader["SUBPARTITION_NAME"]),
					Position = Convert.ToInt32(reader["SUBPARTITION_POSITION"])
				};

			var ownerPartition = ownerTable.Partitions[QualifyStringObject(reader["PARTITION_NAME"])];
			ownerPartition.SubPartitions.Add(subPartition.Name, subPartition);

			return subPartition;
		}

		private static object MapSchemaObject(IDataRecord reader, IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects)
		{
			var objectTypeIdentifer = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["OBJECT_NAME"]));
			var objectType = (string)reader["OBJECT_TYPE"];
			var created = (DateTime)reader["CREATED"];
			var isValid = (string)reader["STATUS"] == "VALID";
			var lastDdl = (DateTime)reader["LAST_DDL_TIME"];
			var isTemporary = String.Equals((string)reader["TEMPORARY"], "Y");

			OracleSchemaObject schemaObject = null;
			switch (objectType)
			{
				case OracleObjectType.Table:
					if (allObjects.TryGetValue(objectTypeIdentifer, out schemaObject))
					{
						goto case OracleObjectType.MaterializedView;
					}
					
					goto default;
				case OracleObjectType.MaterializedView:
				case OracleObjectType.Type:
					if (schemaObject == null && allObjects.TryGetValue(objectTypeIdentifer, out schemaObject))
					{
						schemaObject.Created = created;
						schemaObject.IsTemporary = isTemporary;
						schemaObject.IsValid = isValid;
						schemaObject.LastDdl = lastDdl;
					}
					break;
				default:
					schemaObject = OracleObjectFactory.CreateSchemaObjectMetadata(objectType, objectTypeIdentifer.NormalizedOwner, objectTypeIdentifer.NormalizedName, isValid, created, lastDdl, isTemporary);
					AddObjectToDictionary(allObjects, schemaObject, schemaObject.Type);
					break;
			}

			return schemaObject;
		}

		private static OracleSchemaObject MapSchemaType(IDataRecord reader)
		{
			var owner = QualifyStringObject(reader["OWNER"]);

			OracleTypeBase schemaType;
			var typeType = (string)reader["TYPECODE"];
			switch (typeType)
			{
				case OracleTypeBase.TypeCodeXml:
					schemaType = new OracleTypeObject().WithTypeCode(OracleTypeBase.TypeCodeXml);
					break;
				case OracleTypeBase.TypeCodeAnyData:
					schemaType = new OracleTypeObject().WithTypeCode(OracleTypeBase.TypeCodeAnyData);
					break;
				case OracleTypeBase.TypeCodeObject:
					schemaType = new OracleTypeObject();
					break;
				case OracleTypeBase.TypeCodeCollection:
					schemaType = new OracleTypeCollection();
					break;
				default:
					if (!String.IsNullOrEmpty(owner))
					{
						throw new NotSupportedException($"Type '{typeType}' is not supported. ");
					}
					
					schemaType = new OracleTypeObject().WithTypeCode(typeType);
					break;
			}

			schemaType.FullyQualifiedName = OracleObjectIdentifier.Create(owner, QualifyStringObject(reader["TYPE_NAME"]));

			return schemaType;
		}

		private static OracleSchemaObject MapMaterializedView(IDataRecord reader)
		{
			var refreshModeRaw = (string)reader["REFRESH_MODE"];

			var materializedView =
				new OracleMaterializedView
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["NAME"])),
					TableName = QualifyStringObject(reader["TABLE_NAME"]),
					IsPrebuilt = String.Equals((string)reader["OWNER"], "YES"),
					IsUpdatable = String.Equals((string)reader["OWNER"], "YES"),
					LastRefresh = OracleReaderValueConvert.ToDateTime(reader["LAST_REFRESH"]),
					Next = OracleReaderValueConvert.ToString(reader["NEXT"]),
					Query = (string)reader["QUERY"],
					RefreshGroup = QualifyStringObject(reader["REFRESH_GROUP"]),
					RefreshMethod = (string)reader["REFRESH_METHOD"],
					RefreshMode = String.Equals(refreshModeRaw, "DEMAND") ? MaterializedViewRefreshMode.OnDemand : MaterializedViewRefreshMode.OnCommit,
					RefreshType = MapMaterializedViewRefreshType((string)reader["TYPE"]),
					StartWith = OracleReaderValueConvert.ToDateTime(reader["START_WITH"])
				};

			return materializedView;
		}

		private static MaterializedViewRefreshType MapMaterializedViewRefreshType(string type)
		{
			switch (type)
			{
				case "FAST":
					return MaterializedViewRefreshType.Fast;
				case "COMPLETE":
					return MaterializedViewRefreshType.Complete;
				case "FORCE":
					return MaterializedViewRefreshType.Force;
				default:
					throw new NotSupportedException($"Type '{type}' is not supported. ");
			}
		}

		private static void AddObjectToDictionary<T>(IDictionary<OracleObjectIdentifier, T> allObjects, T schemaObject, string objectType) where T : OracleObject
		{
			if (allObjects.ContainsKey(schemaObject.FullyQualifiedName))
			{
				Trace.WriteLine($"Object '{schemaObject.FullyQualifiedName}' ({objectType}) is already in the dictionary. ");
			}
			else
			{
				allObjects.Add(schemaObject.FullyQualifiedName, schemaObject);
			}
		}

		internal static string QualifyStringObject(object stringValue)
		{
			return stringValue == DBNull.Value || Equals(stringValue, String.Empty) ? null : String.Format("{0}{1}{0}", "\"", stringValue);
		}
	}
}
