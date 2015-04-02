using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	internal class DataDictionaryMapper
	{
		private readonly Dictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();
		private readonly OracleDatabaseModel _databaseModel;

		public DataDictionaryMapper(OracleDatabaseModel databaseModel)
		{
			_databaseModel = databaseModel;
		}

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> BuildDataDictionary()
		{
			_allObjects.Clear();

			var stopwatch = Stopwatch.StartNew();
			var schemaTypeMetadataSource = _databaseModel.ExecuteReader(DatabaseCommands.SelectTypesCommandText, MapSchemaType);
			var schemaTypeAndMateralizedViewMetadataSource = schemaTypeMetadataSource.Concat(_databaseModel.ExecuteReader(DatabaseCommands.SelectMaterializedViewCommandText, MapMaterializedView));

			foreach (var schemaObject in schemaTypeAndMateralizedViewMetadataSource)
			{
				AddSchemaObjectToDictionary(_allObjects, schemaObject);
			}

			Trace.WriteLine(String.Format("Fetch types and materialized views metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectAllObjectsCommandText, MapSchemaObject).ToArray();

			Trace.WriteLine(String.Format("Fetch all objects metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectTablesCommandText, MapTable).ToArray();

			Trace.WriteLine(String.Format("Fetch tables metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectPartitionsCommandText, MapPartitions).ToArray();

			Trace.WriteLine(String.Format("Fetch table partition metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectSubPartitionsCommandText, MapSubPartitions).ToArray();

			Trace.WriteLine(String.Format("Fetch table sub-partition metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectTablePartitionKeysCommandText, r => MapPartitionKeys(r, t => t.PartitionKeyColumns)).ToArray();

			Trace.WriteLine(String.Format("Fetch table partition key metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectTableSubPartitionKeysCommandText, r => MapPartitionKeys(r, t => t.SubPartitionKeyColumns)).ToArray();

			Trace.WriteLine(String.Format("Fetch table sub-partition key metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectSynonymTargetsCommandText, MapSynonymTarget).ToArray();

			Trace.WriteLine(String.Format("Fetch synonyms metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			var columnMetadataSource = _databaseModel.ExecuteReader(DatabaseCommands.SelectTableColumnsCommandText, MapTableColumn);

			foreach (var columnMetadata in columnMetadataSource)
			{
				OracleSchemaObject schemaObject;
				if (!_allObjects.TryGetValue(columnMetadata.Key, out schemaObject))
					continue;

				var dataObject = (OracleDataObject)schemaObject;
				dataObject.Columns.Add(columnMetadata.Value.Name, columnMetadata.Value);
			}

			Trace.WriteLine(String.Format("Fetch table column metadata in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			var constraintSource = _databaseModel.ExecuteReader(DatabaseCommands.SelectConstraintsCommandText, MapConstraintWithReferenceIdentifier)
				.Where(c => c.Key != null)
				.ToArray();

			var constraints = new Dictionary<OracleObjectIdentifier, OracleConstraint>();
			foreach (var constraintPair in constraintSource)
			{
				constraints[constraintPair.Key.FullyQualifiedName] = constraintPair.Key;
			}

			Trace.WriteLine(String.Format("Fetch constraint metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			var constraintColumns = _databaseModel.ExecuteReader(DatabaseCommands.SelectConstraintColumnsCommandText, MapConstraintColumn)
				.GroupBy(c => c.Key)
				.ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value).ToList());

			foreach (var constraintPair in constraintSource)
			{
				OracleConstraint constraint;
				if (!constraints.TryGetValue(constraintPair.Key.FullyQualifiedName, out constraint))
					continue;

				List<string> columns;
				if (constraintColumns.TryGetValue(constraintPair.Key.FullyQualifiedName, out columns))
				{
					constraint.Columns = columns.AsReadOnly();
				}

				var foreignKeyConstraint = constraintPair.Key as OracleForeignKeyConstraint;
				if (foreignKeyConstraint == null)
					continue;

				var referenceConstraint = (OracleUniqueConstraint)constraints[constraintPair.Value];
				foreignKeyConstraint.TargetObject = referenceConstraint.Owner;
				foreignKeyConstraint.ReferenceConstraint = referenceConstraint;
			}

			Trace.WriteLine(String.Format("Fetch column constraint metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectSequencesCommandText, MapSequence).ToArray();

			Trace.WriteLine(String.Format("Fetch sequence metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectTypeAttributesCommandText, MapTypeAttributes).ToArray();

			Trace.WriteLine(String.Format("Fetch type attribute metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			_databaseModel.ExecuteReader(DatabaseCommands.SelectCollectionTypeAttributesCommandText, MapCollectionTypeAttributes).ToArray();

			Trace.WriteLine(String.Format("Fetch collection attribute metadata finished in {0}. ", stopwatch.Elapsed));
			stopwatch.Restart();

			return new ReadOnlyDictionary<OracleObjectIdentifier, OracleSchemaObject>(_allObjects);
		}

		public ILookup<OracleProgramIdentifier, OracleProgramMetadata> GetUserFunctionMetadata()
		{
			var stopwatch = Stopwatch.StartNew();
			var metadata = GetFunctionMetadataCollection(DatabaseCommands.SelectUserFunctionMetadataCommandText, DatabaseCommands.SelectUserFunctionParameterMetadataCommandText, false);
			Trace.WriteLine(String.Format("GetUserFunctionMetadata finished in {0}. ", stopwatch.Elapsed));
			return metadata;
		}

		public ILookup<OracleProgramIdentifier, OracleProgramMetadata> GetBuiltInFunctionMetadata()
		{
			var stopwatch = Stopwatch.StartNew();
			var metadata = GetFunctionMetadataCollection(DatabaseCommands.SelectBuiltInFunctionMetadataCommandText, DatabaseCommands.SelectBuiltInFunctionParameterMetadataCommandText, true);
			Trace.WriteLine(String.Format("GetBuiltInFunctionMetadata finished in {0}. ", stopwatch.Elapsed));
			return metadata;
		}

		public ILookup<string, string> GetContextData()
		{
			return _databaseModel.ExecuteReader(DatabaseCommands.SelectContextDataCommandText, MapContextData).ToLookup(r => r.Key, r => r.Value);
		}

		public IEnumerable<string> GetSchemaNames()
		{
			return _databaseModel.ExecuteReader(DatabaseCommands.SelectAllSchemasCommandText, r => ((string)r["USERNAME"]));
		}

		public IEnumerable<string> GetCharacterSets()
		{
			return _databaseModel.ExecuteReader(DatabaseCommands.SelectCharacterSetsCommandText, r => ((string)r["VALUE"]));
		}

		public IEnumerable<KeyValuePair<int, string>> GetStatisticsKeys()
		{
			var command = _databaseModel.VersionMajor == OracleDatabaseModelBase.VersionMajorOracle12c
				? DatabaseCommands.SelectStatisticsKeysCommandText
				: DatabaseCommands.SelectStatisticsKeysOracle11CommandText;
			
			return _databaseModel.ExecuteReader(command, MapStatisticsKey);
		}

		public IDictionary<OracleObjectIdentifier, OracleDatabaseLink> GetDatabaseLinks()
		{
			return _databaseModel.ExecuteReader(DatabaseCommands.SelectDatabaseLinksCommandText, MapDatabaseLink)
				.ToDictionary(l => l.FullyQualifiedName, l => l);
		}

		public IEnumerable<KeyValuePair<string, string>> GetSystemParameters()
		{
			return _databaseModel.ExecuteReader(DatabaseCommands.SelectSystemParametersCommandText, MapParameter);
		}

		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> GetFunctionMetadataCollection(string selectFunctionMetadataCommandText, string selectParameterMetadataCommandText, bool isBuiltIn)
		{
			var functionMetadataSource = _databaseModel.ExecuteReader(selectFunctionMetadataCommandText, r => MapProgramMetadata(r, isBuiltIn));
			var functionMetadataLookup = functionMetadataSource.ToLookup(m => m.Identifier);

			var functionParameterMetadataSource = _databaseModel.ExecuteReader(selectParameterMetadataCommandText, MapProgramParameterMetadata);
			foreach (var functionIdentifierParameterMetadata in functionParameterMetadataSource)
			{
				var functionMetadata = functionMetadataLookup[functionIdentifierParameterMetadata.Key]
					.SingleOrDefault(m => m.Identifier.Overload == functionIdentifierParameterMetadata.Key.Overload);

				if (functionMetadata != null)
				{
					functionMetadata.AddParameter(functionIdentifierParameterMetadata.Value);
				}
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

		private static OracleProgramMetadata MapProgramMetadata(IDataRecord reader, bool isBuiltIn)
		{
			var identifier = CreateFunctionIdentifierFromReaderValues(reader["OWNER"], reader["PACKAGE_NAME"], reader["PROGRAM_NAME"], reader["OVERLOAD"]);
			var type = Convert.ToBoolean(reader["IS_FUNCTION"]) ? ProgramType.Function : ProgramType.Procedure;
			var isAnalytic = (string)reader["ANALYTIC"] == "YES";
			var isAggregate = (string)reader["AGGREGATE"] == "YES";
			var isPipelined = (string)reader["PIPELINED"] == "YES";
			var isOffloadable = (string)reader["OFFLOADABLE"] == "YES";
			var parallelSupport = (string)reader["PARALLEL"] == "YES";
			var isDeterministic = (string)reader["DETERMINISTIC"] == "YES";
			var metadataMinimumArguments = OracleReaderValueConvert.ToInt32(reader["MINARGS"]);
			var metadataMaximumArguments = OracleReaderValueConvert.ToInt32(reader["MAXARGS"]);
			var authId = (string)reader["AUTHID"] == "CURRENT_USER" ? AuthId.CurrentUser : AuthId.Definer;
			var displayType = (string)reader["DISP_TYPE"];

			return new OracleProgramMetadata(type, identifier, isAnalytic, isAggregate, isPipelined, isOffloadable, parallelSupport, isDeterministic, metadataMinimumArguments, metadataMaximumArguments, authId, displayType, isBuiltIn);
		}

		private KeyValuePair<OracleProgramIdentifier, OracleProgramParameterMetadata> MapProgramParameterMetadata(IDataRecord reader)
		{
			var identifier = CreateFunctionIdentifierFromReaderValues(reader["OWNER"], reader["PACKAGE_NAME"], reader["PROGRAM_NAME"], reader["OVERLOAD"]);

			var parameterName = OracleReaderValueConvert.ToString(reader["ARGUMENT_NAME"]);
			var position = Convert.ToInt32(reader["POSITION"]);
			var sequence = Convert.ToInt32(reader["SEQUENCE"]);
			var dataLevel = Convert.ToInt32(reader["DATA_LEVEL"]);
			var dataType = OracleReaderValueConvert.ToString(reader["DATA_TYPE"]);
			var typeOwner = OracleReaderValueConvert.ToString(reader["TYPE_OWNER"]);
			var typeName = OracleReaderValueConvert.ToString(reader["TYPE_NAME"]);
			var isOptional = (string)reader["DEFAULTED"] == "Y";
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
					throw new NotSupportedException(String.Format("Parameter direction '{0}' is not supported. ", directionRaw));
			}

			return new KeyValuePair<OracleProgramIdentifier, OracleProgramParameterMetadata>(
				identifier, new OracleProgramParameterMetadata(QualifyStringObject(parameterName), position, sequence, dataLevel, direction, dataType, OracleObjectIdentifier.Create(typeOwner, typeName), isOptional));
		}

		private static OracleProgramIdentifier CreateFunctionIdentifierFromReaderValues(object owner, object package, object name, object overload)
		{
			return OracleProgramIdentifier.CreateFromValues(owner == DBNull.Value ? null : QualifyStringObject(owner), package == DBNull.Value ? null : QualifyStringObject(package), QualifyStringObject(name), Convert.ToInt32(overload));
		}

		private OracleTypeObject MapTypeAttributes(IDataRecord reader)
		{
			var typeFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TYPE_NAME"]));
			OracleSchemaObject typeObject;
			if (!_allObjects.TryGetValue(typeFullyQualifiedName, out typeObject))
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
					IsInherited = (string)reader["INHERITED"] == "YES"
				};
			
			type.Attributes.Add(attribute);

			return type;
		}

		private OracleTypeCollection MapCollectionTypeAttributes(IDataRecord reader)
		{
			var typeFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TYPE_NAME"]));
			OracleSchemaObject typeObject;
			if (!_allObjects.TryGetValue(typeFullyQualifiedName, out typeObject))
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

		private OracleDatabaseLink MapDatabaseLink(IDataRecord reader)
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

		private KeyValuePair<string, string> MapParameter(IDataRecord reader)
		{
			return new KeyValuePair<string, string>((string)reader["NAME"], OracleReaderValueConvert.ToString(reader["VALUE"]));
		}

		private OracleSequence MapSequence(IDataRecord reader)
		{
			var sequenceFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["SEQUENCE_OWNER"]), QualifyStringObject(reader["SEQUENCE_NAME"]));
			OracleSchemaObject sequenceObject;
			if (!_allObjects.TryGetValue(sequenceFullyQualifiedName, out sequenceObject))
				return null;

			var sequence = (OracleSequence)sequenceObject;
			sequence.CurrentValue = Convert.ToDecimal(reader["LAST_NUMBER"]);
			sequence.MinimumValue = Convert.ToDecimal(reader["MIN_VALUE"]);
			sequence.MaximumValue = Convert.ToDecimal(reader["MAX_VALUE"]);
			sequence.Increment = Convert.ToDecimal(reader["INCREMENT_BY"]);
			sequence.CacheSize = Convert.ToDecimal(reader["CACHE_SIZE"]);
			sequence.CanCycle = (string)reader["CYCLE_FLAG"] == "Y";
			sequence.IsOrdered = (string)reader["ORDER_FLAG"] == "Y";

			return sequence;
		}

		private static KeyValuePair<OracleObjectIdentifier, string> MapConstraintColumn(IDataRecord reader)
		{
			var column = (string)reader["COLUMN_NAME"];
			return new KeyValuePair<OracleObjectIdentifier, string>(OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["CONSTRAINT_NAME"])), column[0] == '"' ? column : QualifyStringObject(column));
		}

		private KeyValuePair<OracleConstraint, OracleObjectIdentifier> MapConstraintWithReferenceIdentifier(IDataRecord reader)
		{
			var remoteConstraintIdentifier = OracleObjectIdentifier.Empty;
			var owner = QualifyStringObject(reader["OWNER"]);
			var ownerObjectFullyQualifiedName = OracleObjectIdentifier.Create(owner, QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject ownerObject;
			if (!_allObjects.TryGetValue(ownerObjectFullyQualifiedName, out ownerObject))
				return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(null, remoteConstraintIdentifier);

			var rely = OracleReaderValueConvert.ToString(reader["RELY"]);
			var constraint = OracleObjectFactory.CreateConstraint((string)reader["CONSTRAINT_TYPE"], owner, QualifyStringObject(reader["CONSTRAINT_NAME"]), (string)reader["STATUS"] == "ENABLED", (string)reader["VALIDATED"] == "VALIDATED", (string)reader["DEFERRABLE"] == "DEFERRABLE", rely == "RELY");
			constraint.Owner = ownerObject;
			((OracleDataObject)ownerObject).Constraints.Add(constraint);

			var foreignKeyConstraint = constraint as OracleForeignKeyConstraint;
			if (foreignKeyConstraint != null)
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

				foreignKeyConstraint.DeleteRule = cascadeAction;
				remoteConstraintIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(reader["R_OWNER"]), QualifyStringObject(reader["R_CONSTRAINT_NAME"]));
			}

			return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(constraint, remoteConstraintIdentifier);
		}

		private static KeyValuePair<OracleObjectIdentifier, OracleColumn> MapTableColumn(IDataRecord reader)
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

			return new KeyValuePair<OracleObjectIdentifier, OracleColumn>(
				OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TABLE_NAME"])),
				new OracleColumn
				{
					Name = QualifyStringObject(reader["COLUMN_NAME"]),
					DataType = dataType,
					Nullable = (string)reader["NULLABLE"] == "Y",
					CharacterSize = Convert.ToInt32(reader["CHAR_LENGTH"])
				});
		}

		private static void ResolveDataUnit(OracleDataType dataType, object characterUsedValue)
		{
			dataType.Unit = !dataType.FullyQualifiedName.HasOwner && dataType.FullyQualifiedName.NormalizedName.In("\"VARCHAR\"", "\"VARCHAR2\"")
				? (string)characterUsedValue == "C" ? DataUnit.Character : DataUnit.Byte
				: DataUnit.NotApplicable;
		}

		private OracleSchemaObject MapSynonymTarget(IDataRecord reader)
		{
			var synonymFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["SYNONYM_NAME"]));
			OracleSchemaObject synonymObject;
			if (!_allObjects.TryGetValue(synonymFullyQualifiedName, out synonymObject))
			{
				return null;
			}

			var objectFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["TABLE_OWNER"]), QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject schemaObject;
			if (!_allObjects.TryGetValue(objectFullyQualifiedName, out schemaObject))
			{
				return null;
			}

			var synonym = (OracleSynonym)synonymObject;
			synonym.SchemaObject = schemaObject;
			schemaObject.Synonyms.Add(synonym);

			return synonymObject;
		}

		private OracleTable MapTable(IDataRecord reader)
		{
			var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject schemaObject;
			if (!_allObjects.TryGetValue(tableFullyQualifiedName, out schemaObject))
			{
				return null;
			}

			var table = (OracleTable)schemaObject;
			table.Organization = (OrganizationType)Enum.Parse(typeof(OrganizationType), (string)reader["ORGANIZATION"]);
			return table;
		}
		
		private OraclePartition MapPartitions(IDataRecord reader)
		{
			var ownerTable = GetTableForPartition(reader);
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

		private OracleTable MapPartitionKeys(IDataRecord reader, Func<OracleTable, ICollection<string>> getKeyCollectionFunction)
		{
			var ownerTable = GetTableForPartition(reader);
			if (ownerTable == null)
			{
				return null;
			}

			getKeyCollectionFunction(ownerTable).Add(QualifyStringObject(reader["COLUMN_NAME"]));

			return ownerTable;
		}

		private OracleTable GetTableForPartition(IDataRecord reader)
		{
			var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["TABLE_OWNER"]), QualifyStringObject(reader["TABLE_NAME"]));
			OracleSchemaObject schemaObject;
			return _allObjects.TryGetValue(tableFullyQualifiedName, out schemaObject)
				? (OracleTable)schemaObject
				: null;
		}

		private OracleSubPartition MapSubPartitions(IDataRecord reader)
		{
			var ownerTable = GetTableForPartition(reader);
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

		private object MapSchemaObject(IDataRecord reader)
		{
			var objectTypeIdentifer = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["OBJECT_NAME"]));
			var objectType = (string)reader["OBJECT_TYPE"];
			var created = (DateTime)reader["CREATED"];
			var isValid = (string)reader["STATUS"] == "VALID";
			var lastDdl = (DateTime)reader["LAST_DDL_TIME"];
			var isTemporary = (string)reader["TEMPORARY"] == "Y";

			OracleSchemaObject schemaObject = null;
			switch (objectType)
			{
				case OracleSchemaObjectType.Table:
					if (_allObjects.TryGetValue(objectTypeIdentifer, out schemaObject))
					{
						goto case OracleSchemaObjectType.MaterializedView;
					}
					
					goto default;
				case OracleSchemaObjectType.MaterializedView:
				case OracleSchemaObjectType.Type:
					if (schemaObject == null && _allObjects.TryGetValue(objectTypeIdentifer, out schemaObject))
					{
						schemaObject.Created = created;
						schemaObject.IsTemporary = isTemporary;
						schemaObject.IsValid = isValid;
						schemaObject.LastDdl = lastDdl;
					}
					break;
				default:
					schemaObject = OracleObjectFactory.CreateSchemaObjectMetadata(objectType, objectTypeIdentifer.NormalizedOwner, objectTypeIdentifer.NormalizedName, isValid, created, lastDdl, isTemporary);
					AddSchemaObjectToDictionary(_allObjects, schemaObject);
					break;
			}

			return schemaObject;
		}

		private static OracleSchemaObject MapSchemaType(IDataRecord reader)
		{
			OracleTypeBase schemaType;
			var typeType = (string)reader["TYPECODE"];
			switch (typeType)
			{
				case OracleTypeBase.TypeCodeXml:
					schemaType = new OracleTypeObject().WithXmlTypeCode();
					break;
				case OracleTypeBase.TypeCodeObject:
					schemaType = new OracleTypeObject();
					break;
				case OracleTypeBase.TypeCodeCollection:
					schemaType = new OracleTypeCollection();
					break;
				default:
					throw new NotSupportedException(string.Format("Type '{0}' is not supported. ", typeType));
			}

			schemaType.FullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(reader["OWNER"]), QualifyStringObject(reader["TYPE_NAME"]));

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
					IsPrebuilt = (string)reader["OWNER"] == "YES",
					IsUpdatable = (string)reader["OWNER"] == "YES",
					LastRefresh = OracleReaderValueConvert.ToDateTime(reader["LAST_REFRESH"]),
					Next = OracleReaderValueConvert.ToString(reader["NEXT"]),
					Query = (string)reader["QUERY"],
					RefreshGroup = QualifyStringObject(reader["REFRESH_GROUP"]),
					RefreshMethod = (string)reader["REFRESH_METHOD"],
					RefreshMode = refreshModeRaw == "DEMAND" ? MaterializedViewRefreshMode.OnDemand : MaterializedViewRefreshMode.OnCommit,
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
					throw new NotSupportedException(String.Format("Type '{0}' is not supported. ", type));
			}
		}

		private static void AddSchemaObjectToDictionary(IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects, OracleSchemaObject schemaObject)
		{
			if (allObjects.ContainsKey(schemaObject.FullyQualifiedName))
			{
				Trace.WriteLine(string.Format("Object '{0}' ({1}) is already in the dictionary. ", schemaObject.FullyQualifiedName, schemaObject.Type));
			}
			else
			{
				allObjects.Add(schemaObject.FullyQualifiedName, schemaObject);
			}
		}

		internal static string QualifyStringObject(object stringValue)
		{
			return stringValue == DBNull.Value ? null : String.Format("{0}{1}{0}", "\"", stringValue);
		}
	}
}
