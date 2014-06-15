using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		private static readonly object LockObject = new object();
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private const string SqlFuntionMetadataFileName = "OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml";
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));
		private static bool _isRefreshing;
		private static Task _backgroundTask;
		private OracleFunctionMetadataCollection _allFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private readonly ConnectionStringSettings _connectionString;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private Dictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();
		private OracleConnection _userConnection;

		public OracleDatabaseModel(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);
			_currentSchema = _oracleConnectionString.UserID;

			string metadata;
			if (MetadataCache.TryLoadMetadata(SqlFuntionMetadataFileName, out metadata))
			{
				using (var reader = XmlReader.Create(new StringReader(metadata)))
				{
					BuiltInFunctionMetadata = (OracleFunctionMetadataCollection)Serializer.ReadObject(reader);
				}
			}
			else
			{
				ExecuteSynchronizedAction(GenerateBuiltInFunctionMetadata);
			}

			LoadSchemaNames();

			_userConnection = new OracleConnection(connectionString.ConnectionString);
		}

		private static void ExecuteSynchronizedAction(Action action)
		{
			if (_isRefreshing)
				return;

			lock (LockObject)
			{
				if (_isRefreshing)
					return;

				_isRefreshing = true;

				_backgroundTask = Task.Factory.StartNew(action);
			}
		}

		public OracleFunctionMetadataCollection BuiltInFunctionMetadata { get; private set; }

		public override OracleFunctionMetadataCollection AllFunctionMetadata { get { return _allFunctionMetadata; } }

		public override ConnectionStringSettings ConnectionString { get { return _connectionString; } }

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set
			{
				SwitchDefaultSchema(value.ToQuotedIdentifier());

				_currentSchema = value;
			}
		}

		private void SwitchDefaultSchema(string newSchema)
		{
			if (_currentSchema == newSchema)
				return;

			ExecuteUserNonQuery(String.Format("ALTER SESSION SET CURRENT_SCHEMA = {0}", newSchema));
		}

		public override ICollection<string> Schemas { get { return _schemas; } }
		
		public override ICollection<string> AllSchemas { get { return _allSchemas; } }
		//public IDictionary<OracleObjectIdentifier, OracleSchemaObject> Objects { get { return OracleTestDatabaseModel.Instance.Objects; } }
		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _allObjects; } }

		public override void Refresh()
		{
			if (_backgroundTask == null)
			{
				ExecuteSynchronizedAction(LoadSchemaObjectMetadata);
			}
			else
			{
				var currentTask = _backgroundTask;
				_backgroundTask = Task.Factory.StartNew(() =>
				{
					currentTask.Wait();
					LoadSchemaObjectMetadata();
				});
			}
		}

		public override event EventHandler RefreshStarted = delegate { };

		public override event EventHandler RefreshFinished = delegate { };

		public override void Dispose()
		{
			_userConnection.Dispose();

			if (_backgroundTask != null)
				_backgroundTask.Dispose();
		}

		private OracleFunctionMetadataCollection GetUserFunctionMetadata()
		{
			const string getUserFunctionMetadataCommandText =
@"SELECT
    OWNER,
    PACKAGE_NAME,
    FUNCTION_NAME,
	NVL(OVERLOAD, 0) OVERLOAD,
    AGGREGATE ANALYTIC,
    AGGREGATE,
    PIPELINED,
    'NO' OFFLOADABLE,
    PARALLEL,
    DETERMINISTIC,
    NULL MINARGS,
    NULL MAXARGS,
    AUTHID,
    'NORMAL' DISP_TYPE
FROM
    (SELECT DISTINCT
        OWNER,
        CASE WHEN OBJECT_TYPE = 'PACKAGE' THEN OBJECT_NAME END PACKAGE_NAME,
        CASE WHEN OBJECT_TYPE = 'FUNCTION' THEN OBJECT_NAME ELSE PROCEDURE_NAME END FUNCTION_NAME,
		OVERLOAD,
        AGGREGATE,
        PIPELINED,
        PARALLEL,
        DETERMINISTIC,
        AUTHID
    FROM
        ALL_PROCEDURES
    WHERE
        NOT (OWNER = 'SYS' AND OBJECT_NAME = 'STANDARD') AND
        (ALL_PROCEDURES.OBJECT_TYPE = 'FUNCTION' OR (ALL_PROCEDURES.OBJECT_TYPE = 'PACKAGE' AND ALL_PROCEDURES.PROCEDURE_NAME IS NOT NULL))
	AND EXISTS
        (SELECT
            NULL
        FROM
            ALL_ARGUMENTS
        WHERE
            ALL_PROCEDURES.OBJECT_ID = OBJECT_ID AND NVL(ALL_PROCEDURES.PROCEDURE_NAME, ALL_PROCEDURES.OBJECT_NAME) = OBJECT_NAME AND NVL(OVERLOAD, 0) = NVL(ALL_PROCEDURES.OVERLOAD, 0) AND
            POSITION = 0 AND ARGUMENT_NAME IS NULL
        )
	)
ORDER BY
	OWNER,
    PACKAGE_NAME,
    FUNCTION_NAME";

			const string getUserFunctionParameterMetadataCommandText =
@"SELECT
    OWNER,
    PACKAGE_NAME,
    OBJECT_NAME FUNCTION_NAME,
    NVL(OVERLOAD, 0) OVERLOAD,
    ARGUMENT_NAME,
    POSITION,
    DATA_TYPE,
    DEFAULTED,
    IN_OUT
FROM
    ALL_ARGUMENTS
WHERE
    NOT (OWNER = 'SYS' AND OBJECT_NAME = 'STANDARD')
ORDER BY
    OWNER,
    PACKAGE_NAME,
    POSITION";

			return GetFunctionMetadataCollection(getUserFunctionMetadataCommandText, getUserFunctionParameterMetadataCommandText, false);
		}

		private void GenerateBuiltInFunctionMetadata()
		{
			const string getBuiltInFunctionMetadataCommandText =
@"SELECT
    OWNER,
    PACKAGE_NAME,
    NVL(SQL_FUNCTION_METADATA.FUNCTION_NAME, PROCEDURES.FUNCTION_NAME) FUNCTION_NAME,
	NVL(OVERLOAD, 0) OVERLOAD,
    NVL(ANALYTIC, PROCEDURES.AGGREGATE) ANALYTIC,
    NVL(SQL_FUNCTION_METADATA.AGGREGATE, PROCEDURES.AGGREGATE) AGGREGATE,
    NVL(PIPELINED, 'NO') PIPELINED,
    NVL(OFFLOADABLE, 'NO') OFFLOADABLE,
    NVL(PARALLEL, 'NO') PARALLEL,
    NVL(DETERMINISTIC, 'NO') DETERMINISTIC,
    MINARGS,
    MAXARGS,
    NVL(AUTHID, 'CURRENT_USER') AUTHID,
    NVL(DISP_TYPE, 'NORMAL') DISP_TYPE
FROM
    (SELECT DISTINCT
		OWNER,
        OBJECT_NAME PACKAGE_NAME,
        PROCEDURE_NAME FUNCTION_NAME,
		OVERLOAD,
        AGGREGATE,
        PIPELINED,
        PARALLEL,
        DETERMINISTIC,
        AUTHID
    FROM
        ALL_PROCEDURES
    WHERE
        OWNER = 'SYS' AND OBJECT_NAME = 'STANDARD' AND PROCEDURE_NAME NOT LIKE '%SYS$%' AND
        (ALL_PROCEDURES.OBJECT_TYPE = 'FUNCTION' OR (ALL_PROCEDURES.OBJECT_TYPE = 'PACKAGE' AND ALL_PROCEDURES.PROCEDURE_NAME IS NOT NULL))
		AND EXISTS
			(SELECT
				NULL
			FROM
				ALL_ARGUMENTS
			WHERE
				ALL_PROCEDURES.OBJECT_ID = ALL_ARGUMENTS.OBJECT_ID AND ALL_PROCEDURES.PROCEDURE_NAME = ALL_ARGUMENTS.OBJECT_NAME AND NVL(ALL_ARGUMENTS.OVERLOAD, 0) = NVL(ALL_PROCEDURES.OVERLOAD, 0) AND
				POSITION = 0 AND ARGUMENT_NAME IS NULL
			)
	) PROCEDURES
FULL JOIN
    (SELECT
        NAME FUNCTION_NAME,
        NVL(MAX(NULLIF(ANALYTIC, 'NO')), 'NO') ANALYTIC,
        NVL(MAX(NULLIF(AGGREGATE, 'NO')), 'NO') AGGREGATE,
        OFFLOADABLE,
        MIN(MINARGS) MINARGS,
        MAX(MAXARGS) MAXARGS,
        DISP_TYPE
    FROM
        V$SQLFN_METADATA
    WHERE
        DISP_TYPE NOT IN ('REL-OP', 'ARITHMATIC')
    GROUP BY
        NAME,
        OFFLOADABLE,
        DISP_TYPE) SQL_FUNCTION_METADATA
ON PROCEDURES.FUNCTION_NAME = SQL_FUNCTION_METADATA.FUNCTION_NAME
ORDER BY
    FUNCTION_NAME";

			const string getBuiltInFunctionParameterMetadataCommandText =
@"SELECT
	OWNER,
    PACKAGE_NAME,
	OBJECT_NAME FUNCTION_NAME,
	NVL(OVERLOAD, 0) OVERLOAD,
	ARGUMENT_NAME,
	POSITION,
	DATA_TYPE,
	DEFAULTED,
	IN_OUT
FROM
	ALL_ARGUMENTS
WHERE
	OWNER = 'SYS'
	AND PACKAGE_NAME = 'STANDARD'
	AND OBJECT_NAME NOT LIKE '%SYS$%'
	AND DATA_TYPE IS NOT NULL
ORDER BY
    POSITION";

			BuiltInFunctionMetadata = GetFunctionMetadataCollection(getBuiltInFunctionMetadataCommandText, getBuiltInFunctionParameterMetadataCommandText, true);

			using (var writer = XmlWriter.Create(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
			{
				Serializer.WriteObject(writer, BuiltInFunctionMetadata);
			}

			/*var allFunctionMetadata = GetUserFunctionMetadata();

			var test = new OracleFunctionMetadataCollection(allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner == "husqvik".ToQuotedIdentifier()).ToArray());
			using (var writer = XmlWriter.Create(@"D:\TestFunctionCollection.xml"))
			{
				Serializer.WriteObject(writer, test);
			}*/

			_isRefreshing = false;
		}

		private OracleFunctionMetadataCollection GetFunctionMetadataCollection(string getFunctionMetadataCommandText, string getParameterMetadataCommandText, bool isBuiltIn)
		{
			var functionMetadataDictionary = new Dictionary<OracleFunctionIdentifier, OracleFunctionMetadata>();

			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = getFunctionMetadataCommandText;

					connection.Open();

					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							var identifier = CreateFunctionIdentifierFromReaderValues(reader["OWNER"], reader["PACKAGE_NAME"], reader["FUNCTION_NAME"], reader["OVERLOAD"]);
							var isAnalytic = (string)reader["ANALYTIC"] == "YES";
							var isAggregate = (string)reader["AGGREGATE"] == "YES";
							var isPipelined = (string)reader["PIPELINED"] == "YES";
							var isOffloadable = (string)reader["OFFLOADABLE"] == "YES";
							var parallelSupport = (string)reader["PARALLEL"] == "YES";
							var isDeterministic = (string)reader["DETERMINISTIC"] == "YES";
							var minimumArgumentsRaw = reader["MINARGS"];
							var metadataMinimumArguments = minimumArgumentsRaw == DBNull.Value ? null : (int?)Convert.ToInt32(minimumArgumentsRaw);
							var maximumArgumentsRaw = reader["MAXARGS"];
							var metadataMaximumArguments = maximumArgumentsRaw == DBNull.Value ? null : (int?)Convert.ToInt32(maximumArgumentsRaw);
							var authId = (string)reader["AUTHID"] == "CURRENT_USER" ? AuthId.CurrentUser : AuthId.Definer;
							var displayType = (string)reader["DISP_TYPE"];

							var functionMetadata = new OracleFunctionMetadata(identifier, isAnalytic, isAggregate, isPipelined, isOffloadable, parallelSupport, isDeterministic, metadataMinimumArguments, metadataMaximumArguments, authId, displayType, isBuiltIn);
							functionMetadataDictionary.Add(functionMetadata.Identifier, functionMetadata);
						}
					}

					command.CommandText = getParameterMetadataCommandText;

					using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
					{
						while (reader.Read())
						{
							var identifier = CreateFunctionIdentifierFromReaderValues(reader[0], reader[1], reader[2], reader[3]);

							if (!functionMetadataDictionary.ContainsKey(identifier))
								continue;

							var metadata = functionMetadataDictionary[identifier];

							var parameterNameRaw = reader[4];
							var parameterName = parameterNameRaw == DBNull.Value ? null : (string)parameterNameRaw;
							var position = Convert.ToInt32(reader[5]);
							var dataTypeRaw = reader[6];
							var dataType = dataTypeRaw == DBNull.Value ? null : (string)dataTypeRaw;
							var isOptional = (string)reader[7] == "Y";
							var directionRaw = (string)reader[8];
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

							var parameterMetadata = new OracleFunctionParameterMetadata(parameterName, position, direction, dataType, isOptional);
							metadata.Parameters.Add(parameterMetadata);
						}
					}
				}
			}

			return new OracleFunctionMetadataCollection(functionMetadataDictionary.Values);
		}

		internal static OracleFunctionIdentifier CreateFunctionIdentifierFromReaderValues(object owner, object package, object name, object overload)
		{
			return OracleFunctionIdentifier.CreateFromValues(owner == DBNull.Value ? null : QualifyStringObject(owner), package == DBNull.Value ? null : QualifyStringObject(package), QualifyStringObject(name), Convert.ToInt32(overload));
		}

		private void ExecuteUserNonQuery(string commandText)
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = commandText;

				try
				{
					_userConnection.Open();
					command.ExecuteNonQuery();
				}
				finally
				{
					_userConnection.Close();
				}
			}
		}

		private IEnumerable<T> ExecuteReader<T>(string commandText, Func<OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = commandText;

					connection.Open();

					using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
					{
						while (reader.Read())
						{
							yield return formatFunction(reader);
						}
					}
				}
			}
		}

		private void LoadSchemaObjectMetadata()
		{
			RefreshStarted(this, EventArgs.Empty);

			const string selectAllObjectsCommandText = "SELECT OWNER, OBJECT_NAME, SUBOBJECT_NAME, OBJECT_ID, DATA_OBJECT_ID, OBJECT_TYPE, CREATED, LAST_DDL_TIME, STATUS, TEMPORARY, EDITIONABLE, EDITION_NAME FROM ALL_OBJECTS WHERE OBJECT_TYPE IN ('SYNONYM', 'VIEW', 'TABLE')";
			var dataObjectMetadataSource = ExecuteReader(
				selectAllObjectsCommandText,
				r => OracleObjectFactory.CreateDataObjectMetadata((string)r["OBJECT_TYPE"], QualifyStringObject(r["OWNER"]), QualifyStringObject(r["OBJECT_NAME"]), (string)r["STATUS"] == "VALID", (DateTime)r["CREATED"], (DateTime)r["LAST_DDL_TIME"], (string)r["TEMPORARY"] == "Y"));

			var dataObjectMetadata = dataObjectMetadataSource.ToDictionary(m => m.FullyQualifiedName, m => m);

			const string selectTablesCommandText =
@"SELECT OWNER, TABLE_NAME, TABLESPACE_NAME, CLUSTER_NAME, STATUS, LOGGING, NUM_ROWS, BLOCKS, AVG_ROW_LEN, DEGREE, CACHE, SAMPLE_SIZE, LAST_ANALYZED, TEMPORARY, NESTED, ROW_MOVEMENT, COMPRESS_FOR,
CASE
	WHEN TEMPORARY = 'N' AND TABLESPACE_NAME IS NULL AND PARTITIONED = 'NO' AND IOT_TYPE IS NULL AND PCT_FREE = 0 THEN 'External'
	WHEN IOT_TYPE = 'IOT' THEN 'Index'
	ELSE 'Heap'
END ORGANIZATION
FROM ALL_TABLES";
			ExecuteReader(
				selectTablesCommandText,
				r =>
				{
					var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject schemaObject;
					if (!dataObjectMetadata.TryGetValue(tableFullyQualifiedName, out schemaObject))
					{
						return null;
					}

					var table = (OracleTable)schemaObject;
					table.Organization = (OrganizationType)Enum.Parse(typeof(OrganizationType), (string)r["ORGANIZATION"]);
					return table;
				})
				.ToArray();

			const string selectSynonymTargetsCommandText = "SELECT OWNER, SYNONYM_NAME, TABLE_OWNER, TABLE_NAME FROM ALL_SYNONYMS";
			ExecuteReader(
				selectSynonymTargetsCommandText,
				r =>
				{
					var synonymFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["SYNONYM_NAME"]));
					OracleSchemaObject synonym;
					dataObjectMetadata.TryGetValue(synonymFullyQualifiedName, out synonym);

					var objectFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["TABLE_OWNER"]), QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject schemaObject;
					dataObjectMetadata.TryGetValue(objectFullyQualifiedName, out schemaObject);

					((OracleSynonym)synonym).SchemaObject = schemaObject;
					return synonym;
				}
				).ToArray();

			const string selectTableColumnsCommandText = "SELECT OWNER, TABLE_NAME, COLUMN_NAME, DATA_TYPE, DATA_TYPE_OWNER, DATA_LENGTH, CHAR_LENGTH, DATA_PRECISION, DATA_SCALE, CHAR_USED, NULLABLE, COLUMN_ID, NUM_DISTINCT, LOW_VALUE, HIGH_VALUE, NUM_NULLS, NUM_BUCKETS, LAST_ANALYZED, SAMPLE_SIZE, AVG_COL_LEN, HISTOGRAM FROM ALL_TAB_COLUMNS ORDER BY OWNER, TABLE_NAME, COLUMN_ID";
			var columnMetadataSource = ExecuteReader(
				selectTableColumnsCommandText,
				r =>
				{
					var dataTypeOwnerRaw = r["DATA_TYPE_OWNER"];
					var dataTypeOwner = dataTypeOwnerRaw == DBNull.Value ? null : String.Format("{0}.", dataTypeOwnerRaw);
					var type = String.Format("{0}{1}", dataTypeOwner, r["DATA_TYPE"]);
					var precisionRaw = r["DATA_PRECISION"];
					var scaleRaw = r["DATA_SCALE"];
					return new KeyValuePair<OracleObjectIdentifier, OracleColumn>(
						OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TABLE_NAME"])),
						new OracleColumn
						{
							Name = QualifyStringObject(r["COLUMN_NAME"]),
							Nullable = (string)r["NULLABLE"] == "Y",
							Type = type,
							Size = Convert.ToInt32(r["DATA_LENGTH"]),
							CharacterSize = Convert.ToInt32(r["CHAR_LENGTH"]),
							Precision = precisionRaw == DBNull.Value ? null : (int?)Convert.ToInt32(precisionRaw),
							Scale = scaleRaw == DBNull.Value ? null : (int?)Convert.ToInt32(scaleRaw),
							Unit = type.In("VARCHAR", "VARCHAR2")
								? (string)r["CHAR_USED"] == "C" ? DataUnit.Character : DataUnit.Byte
								: DataUnit.NotApplicable
						});
				});

			foreach (var columnMetadata in columnMetadataSource)
			{
				OracleSchemaObject schemaObject;
				if (!dataObjectMetadata.TryGetValue(columnMetadata.Key, out schemaObject))
					continue;

				var dataObject = (OracleDataObject)schemaObject;
				dataObject.Columns.Add(columnMetadata.Value.Name, columnMetadata.Value);
			}

			const string selectConstraintsCommandText = "SELECT OWNER, CONSTRAINT_NAME, CONSTRAINT_TYPE, TABLE_NAME, SEARCH_CONDITION, R_OWNER, R_CONSTRAINT_NAME, DELETE_RULE, STATUS, DEFERRABLE, VALIDATED, RELY, INDEX_OWNER, INDEX_NAME FROM ALL_CONSTRAINTS WHERE CONSTRAINT_TYPE IN ('C', 'R', 'P', 'U')";
			var constraintSource = ExecuteReader(
				selectConstraintsCommandText,
				r =>
				{
					var remoteConstraintIdentifier = OracleObjectIdentifier.Empty;
					var owner = QualifyStringObject(r["OWNER"]);
					var ownerObjectFullyQualifiedName = OracleObjectIdentifier.Create(owner, QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject ownerObject;
					if (!dataObjectMetadata.TryGetValue(ownerObjectFullyQualifiedName, out ownerObject))
						return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(null, remoteConstraintIdentifier); ;

					var relyRaw = r["RELY"];
					var constraint = OracleObjectFactory.CreateConstraint((string)r["CONSTRAINT_TYPE"], owner, QualifyStringObject(r["CONSTRAINT_NAME"]), (string)r["STATUS"] == "ENABLED", (string)r["VALIDATED"] == "VALIDATED", (string)r["DEFERRABLE"] == "DEFERRABLE", relyRaw != DBNull.Value && (string)relyRaw == "RELY");
					constraint.Owner = ownerObject;
					((OracleDataObject)ownerObject).Constraints.Add(constraint);

					var foreignKeyConstraint = constraint as OracleForeignKeyConstraint;
					if (foreignKeyConstraint != null)
					{
						var cascadeAction = DeleteRule.None;
						switch ((string)r["DELETE_RULE"])
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
						remoteConstraintIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(r["R_OWNER"]), QualifyStringObject(r["R_CONSTRAINT_NAME"]));
					}
					
					return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(constraint, remoteConstraintIdentifier);
				})
				.Where(c => c.Key != null)
				.ToArray();

			var constraints = new Dictionary<OracleObjectIdentifier, OracleConstraint>();
			foreach (var constraintPair in constraintSource)
			{
				constraints[constraintPair.Key.FullyQualifiedName] = constraintPair.Key;
			}

			const string selectConstraintColumnsCommandText = "SELECT OWNER, CONSTRAINT_NAME, COLUMN_NAME, POSITION FROM ALL_CONS_COLUMNS ORDER BY OWNER, CONSTRAINT_NAME, POSITION";
			var constraintColumns = ExecuteReader(
				selectConstraintColumnsCommandText,
				r =>
				{
					var column = (string)r["COLUMN_NAME"];
					return new KeyValuePair<OracleObjectIdentifier, string>(OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["CONSTRAINT_NAME"])), column[0] == '"' ? column : QualifyStringObject(column));
				})
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

			_allFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(GetUserFunctionMetadata().SqlFunctions));

			_allObjects = dataObjectMetadata;
			//var tmp = dataObjectMetadata.Values.Where(o => o.FullyQualifiedName.NormalizedOwner == "\"CA_DEV\"").ToArray();
			var ftmp = _allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner.Contains("CA_DEV")).ToArray();
			//var types = tmp.OfType<OracleDataObject>().SelectMany(o => o.Columns.Values).Select(c => c.FullTypeName).Distinct().ToArray();

			//var accs = tmp.Where(o => o.Name.Contains("Accounts")).ToArray();

			RefreshFinished(this, EventArgs.Empty);
		}

		private static string QualifyStringObject(object stringValue)
		{
			return String.Format("{0}{1}{0}", "\"", stringValue);
		}

		private void LoadSchemaNames()
		{
			const string selectAllSchemasCommandText = "SELECT USERNAME FROM ALL_USERS";
			var schemaSource = ExecuteReader(
				selectAllSchemasCommandText,
				r => ((string)r["USERNAME"]))
				.ToArray();

			_schemas = new HashSet<string>(schemaSource);
			_allSchemas = new HashSet<string>(schemaSource.Select(QualifyStringObject)) { SchemaPublic };
		}

		private static class OracleObjectFactory
		{
			public static OracleSchemaObject CreateDataObjectMetadata(string objectType, string owner, string name, bool isValid, DateTime created, DateTime lastDdl, bool isTemporary)
			{
				var dataObject = CreateObjectMetadata(objectType);
				dataObject.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);
				dataObject.IsValid = isValid;
				dataObject.Created = created;
				dataObject.LastDdl = lastDdl;
				dataObject.IsTemporary = isTemporary;

				return dataObject;
			}

			public static OracleConstraint CreateConstraint(string constraintType, string owner, string name, bool isEnabled, bool isValidated, bool isDeferrable, bool isRelied)
			{
				var constraint = CreateConstraint(constraintType);
				constraint.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);
				constraint.IsEnabled = isEnabled;
				constraint.IsValidated = isValidated;
				constraint.IsDeferrable = isDeferrable;
				constraint.IsRelied = isRelied;

				return constraint;
			}

			private static OracleConstraint CreateConstraint(string constraintType)
			{
				switch (constraintType)
				{
					case "P":
						return new OraclePrimaryKeyConstraint();
					case "U":
						return new OracleUniqueConstraint();
					case "R":
						return new OracleForeignKeyConstraint();
					case "C":
						return new OracleCheckConstraint();
					default:
						throw new InvalidOperationException(String.Format("Constraint type '{0}' not supported. ", constraintType));
				}
			}

			private static OracleSchemaObject CreateObjectMetadata(string objectType)
			{
				switch (objectType)
				{
					case "TABLE":
						return new OracleTable();
					case "VIEW":
						return new OracleView();
					case "SYNONYM":
						return new OracleSynonym();
					default:
						throw new InvalidOperationException(String.Format("Object type '{0}' not supported. ", objectType));
				}
			}
		}
	}

	public struct SchemaObjectResult<TObject> where TObject : OracleObject
	{
		public static readonly SchemaObjectResult<TObject> EmptyResult = new SchemaObjectResult<TObject>();

		public bool SchemaFound { get; set; }

		public TObject SchemaObject { get; set; }

		public OracleSynonym Synonym { get; set; }

		public OracleObjectIdentifier FullyQualifiedName { get; set; }
	}
}
