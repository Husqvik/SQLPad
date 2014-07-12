using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		private readonly object _lockObject = new object();
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private const int RefreshInterval = 10;
		private readonly Timer _timer = new Timer(RefreshInterval * 60000);
		private const string SqlFuntionMetadataFileName = "OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml";
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));
		private bool _isRefreshing;
		private bool _canExecute = true;
		private bool _isExecuting;
		private Task _backgroundTask;
		private Task _statementExecutionTask;
		private OracleFunctionMetadataCollection _allFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private OracleFunctionMetadataCollection _builtInFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private ILookup<string, OracleFunctionMetadata> _nonPackageBuiltInFunctionMetadata = Enumerable.Empty<OracleFunctionMetadata>().ToLookup(m => m.Identifier.Name, m => m);
		private readonly ConnectionStringSettings _connectionString;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private Dictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();
		private readonly OracleConnection _userConnection;
		private OracleDataReader _dataReader;
		private DateTime _lastRefresh;

		internal static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();

		private OracleDatabaseModel(ConnectionStringSettings connectionString)
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

			_timer.Elapsed += (sender, args) => RefreshIfNeeded();
			_timer.Start();
		}

		public static OracleDatabaseModel GetDatabaseModel(ConnectionStringSettings connectionString)
		{
			OracleDatabaseModel databaseModel;
			if (!DatabaseModels.TryGetValue(connectionString.ConnectionString, out databaseModel))
			{
				DatabaseModels[connectionString.ConnectionString] = databaseModel = new OracleDatabaseModel(connectionString);
			}
			else
			{
				databaseModel = databaseModel.Clone();
			}

			return databaseModel;
		}

		private void ExecuteSynchronizedAction(Action action)
		{
			if (_isRefreshing)
				return;

			lock (_lockObject)
			{
				if (_isRefreshing)
					return;

				_isRefreshing = true;

				_backgroundTask = Task.Factory.StartNew(action);
			}
		}

		public OracleFunctionMetadataCollection BuiltInFunctionMetadata
		{
			get { return _builtInFunctionMetadata; }
			private set
			{
				_builtInFunctionMetadata = value;
				_nonPackageBuiltInFunctionMetadata = value.SqlFunctions.Where(f => String.IsNullOrEmpty(f.Identifier.Owner)).ToLookup(f => f.Identifier.Name, f => f);
			}
		}

		public override OracleFunctionMetadataCollection AllFunctionMetadata { get { return _allFunctionMetadata; } }

		protected override ILookup<string, OracleFunctionMetadata> NonPackageBuiltInFunctionMetadata { get { return _nonPackageBuiltInFunctionMetadata; } }

		public override ConnectionStringSettings ConnectionString { get { return _connectionString; } }

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set { _currentSchema = value; }
		}

		public override ICollection<string> Schemas { get { return _schemas; } }
		
		public override ICollection<string> AllSchemas { get { return _allSchemas; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _allObjects; } }

		public override void RefreshIfNeeded()
		{
			if (_lastRefresh.AddMinutes(RefreshInterval) < DateTime.Now)
			{
				Refresh();
			}
		}

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

		private const string GetObjectScriptCommand = "SELECT SYS.DBMS_METADATA.GET_DDL(OBJECT_TYPE => :OBJECT_TYPE, NAME => :NAME, SCHEMA => :SCHEMA) SCRIPT FROM SYS.DUAL";

		public override string GetObjectScript(OracleSchemaObject schemaObject)
		{
			return ExecuteReader(
				GetObjectScriptCommand,
				c => c.AddSimpleParameter("OBJECT_TYPE", schemaObject.Type.ToUpperInvariant())
					.AddSimpleParameter("NAME", schemaObject.FullyQualifiedName.Name.Trim('"'))
					.AddSimpleParameter("SCHEMA", schemaObject.FullyQualifiedName.Owner == SchemaPublic ? null : schemaObject.FullyQualifiedName.Owner.Trim('"')),
				r => (string)r[0])
				.FirstOrDefault();
		}

		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshFinished;

		public override bool CanExecute { get { return _canExecute; } }
		
		public override bool IsExecuting { get { return _isExecuting; } }

		public override bool CanFetch
		{
			get { return _dataReader != null && !_dataReader.IsClosed; }
		}

		public override void Dispose()
		{
			_timer.Dispose();

			if (_dataReader != null)
				_dataReader.Dispose();

			if (_statementExecutionTask != null)
				_statementExecutionTask.Dispose();

			if (_backgroundTask != null)
			{
				if (_isRefreshing)
				{
					RaiseEvent(RefreshFinished);
				}

				if (_backgroundTask.Status == TaskStatus.Running)
				{
					_backgroundTask.ContinueWith(t => t.Dispose());
				}
				else
				{
					_backgroundTask.Dispose();
				}
			}

			RefreshStarted = null;
			RefreshFinished = null;
			
			_userConnection.Dispose();

			DatabaseModels.Remove(_connectionString.ConnectionString);
		}

		public OracleDatabaseModel Clone()
		{
			var clone =
				new OracleDatabaseModel(ConnectionString)
				{
					_currentSchema = _currentSchema,
					_lastRefresh = _lastRefresh,
					_allObjects = _allObjects,
					_allFunctionMetadata = _allFunctionMetadata,
					_builtInFunctionMetadata = _builtInFunctionMetadata,
					_nonPackageBuiltInFunctionMetadata = _nonPackageBuiltInFunctionMetadata
				};

			return clone;
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

		private int ExecuteUserNonQuery(string commandText)
		{
			return ExecuteUserStatement(commandText, c => c.ExecuteNonQuery(), true);
		}

		private T ExecuteUserStatement<T>(string commandText, Func<OracleCommand, T> executeFunction, bool closeConnection = false)
		{
			using (var command = _userConnection.CreateCommand())
			{
				command.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = {0}", _currentSchema);

				lock (_lockObject)
				{
					try
					{
						_isExecuting = true;
						_userConnection.Open();

						command.ExecuteNonQuery();

						command.CommandText = commandText;

						return executeFunction(command);
					}
					catch
					{
						try
						{
							_userConnection.Close();
						}
						catch { }
						
						throw;
					}
					finally
					{
						if (closeConnection && _userConnection.State != ConnectionState.Closed)
						{
							_userConnection.Close();
						}

						_isExecuting = false;
					}
				}
			}
		}

		public override int ExecuteStatement(string statementText, bool returnDataset)
		{
			if (!CanExecute)
				throw new InvalidOperationException("Another statement is executing right now. ");

			if (_dataReader != null)
			{
				_dataReader.Dispose();
			}

			if (!returnDataset)
				return ExecuteUserStatement(statementText, c => c.ExecuteNonQuery());
			
			_dataReader = ExecuteUserStatement(statementText, c => c.ExecuteReader(CommandBehavior.CloseConnection));
			return 0;
		}

		/*public Task ExecuteStatementAsync(string statementText)
		{
			return _statementExecutionTask = Task.Factory.StartNew(c => ExecuteStatement((string)c), statementText);
		}*/

		public override ICollection<ColumnHeader> GetColumnHeaders()
		{
			CheckCanFetch();

			var columnTypes = new ColumnHeader[_dataReader.FieldCount];
			for (var i = 0; i < _dataReader.FieldCount; i++)
			{
				columnTypes[i] =
					new ColumnHeader
					{
						ColumnIndex = i,
						Name = _dataReader.GetName(i),
						DataType = _dataReader.GetFieldType(i),
						DatabaseDataType = _dataReader.GetDataTypeName(i),
						ValueConverterFunction = ValueConverterFunction
					};
			}

			return columnTypes;
		}

		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			CheckCanFetch();

			for (var i = 0; i < rowCount; i++)
			{
				if (_dataReader.Read())
				{
					var columnData = new object[_dataReader.FieldCount];
					_dataReader.GetValues(columnData);
					yield return columnData;
				}
				else
				{
					_dataReader.Close();
					break;
				}
			}
		}

		private void CheckCanFetch()
		{
			if (_dataReader == null || _dataReader.IsClosed)
				throw new InvalidOperationException("No data reader available. ");
		}

		private IEnumerable<T> ExecuteReader<T>(string commandText, Action<OracleCommand> configureCommandFunction, Func<OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = commandText;
					command.BindByName = true;

					if (configureCommandFunction != null)
					{
						configureCommandFunction(command);
					}

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
			RaiseEvent(RefreshStarted);
			_isRefreshing = true;

			var allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();

			var selectTypesCommandText = String.Format("SELECT OWNER, TYPE_NAME, TYPECODE, PREDEFINED, INCOMPLETE, FINAL, INSTANTIABLE, SUPERTYPE_OWNER, SUPERTYPE_NAME FROM SYS.ALL_TYPES WHERE TYPECODE IN ('{0}', '{1}', '{2}')", OracleTypeBase.ObjectType, OracleTypeBase.CollectionType, OracleTypeBase.XmlType);
			var schemaTypeMetadataSource = ExecuteReader(
				selectTypesCommandText, null,
				r =>
				{
					OracleTypeBase schemaType = null;
					var typeType = (string)r["TYPECODE"];
					switch (typeType)
					{
						case OracleTypeBase.XmlType:
						case OracleTypeBase.ObjectType:
							schemaType =
								new OracleObjectType
								{

								};
							break;
						case OracleTypeBase.CollectionType:
							schemaType =
								new OracleCollectionType
								{

								};
							break;
					}

					schemaType.FullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TYPE_NAME"]));

					return schemaType;
				});

			foreach (var schemaType in schemaTypeMetadataSource)
			{
				AddSchemaObjectToDictionary(allObjects, schemaType);
			}

			const string selectAllObjectsCommandText = "SELECT OWNER, OBJECT_NAME, SUBOBJECT_NAME, OBJECT_ID, DATA_OBJECT_ID, OBJECT_TYPE, CREATED, LAST_DDL_TIME, STATUS, TEMPORARY/*, EDITIONABLE, EDITION_NAME*/ FROM SYS.ALL_OBJECTS WHERE OBJECT_TYPE IN ('SYNONYM', 'VIEW', 'TABLE', 'SEQUENCE', 'FUNCTION', 'PACKAGE', 'TYPE')";
			ExecuteReader(
				selectAllObjectsCommandText, null,
				r =>
				{
					var objectTypeIdentifer = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["OBJECT_NAME"]));
					var objectType = (string)r["OBJECT_TYPE"];
					var created = (DateTime)r["CREATED"];
					var isValid = (string)r["STATUS"] == "VALID";
					var lastDdl = (DateTime)r["LAST_DDL_TIME"];
					var isTemporary = (string)r["TEMPORARY"] == "Y";
					
					OracleSchemaObject schemaObject;
					if (objectType == OracleSchemaObjectType.Type)
					{
						if (allObjects.TryGetValue(objectTypeIdentifer, out schemaObject))
						{
							schemaObject.Created = created;
							schemaObject.IsTemporary = isTemporary;
							schemaObject.IsValid = isValid;
							schemaObject.LastDdl = lastDdl;
						}
					}
					else
					{
						schemaObject = OracleObjectFactory.CreateSchemaObjectMetadata(objectType, objectTypeIdentifer.NormalizedOwner, objectTypeIdentifer.NormalizedName, isValid, created, lastDdl, isTemporary);
						AddSchemaObjectToDictionary(allObjects, schemaObject);
					}

					return schemaObject;
				})
				.ToArray();

			//var allObjects = dataObjectMetadataSource.ToDictionary(m => m.FullyQualifiedObjectName, m => m);

			const string selectTablesCommandText =
@"SELECT OWNER, TABLE_NAME, TABLESPACE_NAME, CLUSTER_NAME, STATUS, LOGGING, NUM_ROWS, BLOCKS, AVG_ROW_LEN, DEGREE, CACHE, SAMPLE_SIZE, LAST_ANALYZED, TEMPORARY, NESTED, ROW_MOVEMENT, COMPRESS_FOR,
CASE
	WHEN TEMPORARY = 'N' AND TABLESPACE_NAME IS NULL AND PARTITIONED = 'NO' AND IOT_TYPE IS NULL AND PCT_FREE = 0 THEN 'External'
	WHEN IOT_TYPE = 'IOT' THEN 'Index'
	ELSE 'Heap'
END ORGANIZATION
FROM ALL_TABLES";
			ExecuteReader(
				selectTablesCommandText, null,
				r =>
				{
					var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject schemaObject;
					if (!allObjects.TryGetValue(tableFullyQualifiedName, out schemaObject))
					{
						return null;
					}

					var table = (OracleTable)schemaObject;
					table.Organization = (OrganizationType)Enum.Parse(typeof(OrganizationType), (string)r["ORGANIZATION"]);
					return table;
				})
				.ToArray();

			const string selectSynonymTargetsCommandText = "SELECT OWNER, SYNONYM_NAME, TABLE_OWNER, TABLE_NAME FROM SYS.ALL_SYNONYMS";
			ExecuteReader(
				selectSynonymTargetsCommandText, null,
				r =>
				{
					var synonymFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["SYNONYM_NAME"]));
					OracleSchemaObject synonymObject;
					if (!allObjects.TryGetValue(synonymFullyQualifiedName, out synonymObject))
					{
						return null;
					}

					var objectFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["TABLE_OWNER"]), QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject schemaObject;
					if (!allObjects.TryGetValue(objectFullyQualifiedName, out schemaObject))
					{
						return null;
					}

					var synonym = (OracleSynonym)synonymObject;
					synonym.SchemaObject = schemaObject;
					schemaObject.Synonym = synonym;

					return synonymObject;
				}
				).ToArray();

			const string selectTableColumnsCommandText = "SELECT OWNER, TABLE_NAME, COLUMN_NAME, DATA_TYPE, DATA_TYPE_OWNER, DATA_LENGTH, CHAR_LENGTH, DATA_PRECISION, DATA_SCALE, CHAR_USED, NULLABLE, COLUMN_ID, NUM_DISTINCT, LOW_VALUE, HIGH_VALUE, NUM_NULLS, NUM_BUCKETS, LAST_ANALYZED, SAMPLE_SIZE, AVG_COL_LEN, HISTOGRAM FROM SYS.ALL_TAB_COLUMNS ORDER BY OWNER, TABLE_NAME, COLUMN_ID";
			var columnMetadataSource = ExecuteReader(
				selectTableColumnsCommandText, null,
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
				if (!allObjects.TryGetValue(columnMetadata.Key, out schemaObject))
					continue;

				var dataObject = (OracleDataObject)schemaObject;
				dataObject.Columns.Add(columnMetadata.Value.Name, columnMetadata.Value);
			}

			const string selectConstraintsCommandText = "SELECT OWNER, CONSTRAINT_NAME, CONSTRAINT_TYPE, TABLE_NAME, SEARCH_CONDITION, R_OWNER, R_CONSTRAINT_NAME, DELETE_RULE, STATUS, DEFERRABLE, VALIDATED, RELY, INDEX_OWNER, INDEX_NAME FROM SYS.ALL_CONSTRAINTS WHERE CONSTRAINT_TYPE IN ('C', 'R', 'P', 'U')";
			var constraintSource = ExecuteReader(
				selectConstraintsCommandText, null,
				r =>
				{
					var remoteConstraintIdentifier = OracleObjectIdentifier.Empty;
					var owner = QualifyStringObject(r["OWNER"]);
					var ownerObjectFullyQualifiedName = OracleObjectIdentifier.Create(owner, QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject ownerObject;
					if (!allObjects.TryGetValue(ownerObjectFullyQualifiedName, out ownerObject))
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

			const string selectConstraintColumnsCommandText = "SELECT OWNER, CONSTRAINT_NAME, COLUMN_NAME, POSITION FROM SYS.ALL_CONS_COLUMNS ORDER BY OWNER, CONSTRAINT_NAME, POSITION";
			var constraintColumns = ExecuteReader(
				selectConstraintColumnsCommandText, null,
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

			const string selectSequencesCommandText = "SELECT SEQUENCE_OWNER, SEQUENCE_NAME, MIN_VALUE, MAX_VALUE, INCREMENT_BY, CYCLE_FLAG, ORDER_FLAG, CACHE_SIZE, LAST_NUMBER FROM SYS.ALL_SEQUENCES";
			ExecuteReader(
				selectSequencesCommandText, null,
				r =>
				{
					var sequenceFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["SEQUENCE_OWNER"]), QualifyStringObject(r["SEQUENCE_NAME"]));
					OracleSchemaObject sequenceObject;
					if (!AllObjects.TryGetValue(sequenceFullyQualifiedName, out sequenceObject))
						return null;

					var sequence = (OracleSequence) sequenceObject;
					sequence.CurrentValue = Convert.ToDecimal(r["LAST_NUMBER"]);
					sequence.MinimumValue = Convert.ToDecimal(r["MIN_VALUE"]);
					sequence.MaximumValue = Convert.ToDecimal(r["MAX_VALUE"]);
					sequence.Increment = Convert.ToDecimal(r["INCREMENT_BY"]);
					sequence.CacheSize = Convert.ToDecimal(r["CACHE_SIZE"]);
					sequence.CanCycle = (string)r["CYCLE_FLAG"] == "Y";
					sequence.IsOrdered = (string)r["ORDER_FLAG"] == "Y";

					return sequence;
				})
				.ToArray();

			const string selectTypeAttributesCommandText = "SELECT OWNER, TYPE_NAME, ATTR_NAME, ATTR_TYPE_MOD, ATTR_TYPE_OWNER, ATTR_TYPE_NAME, LENGTH, PRECISION, SCALE, ATTR_NO, CHAR_USED FROM SYS.ALL_TYPE_ATTRS ORDER BY OWNER, TYPE_NAME, ATTR_NO";
			ExecuteReader(
				selectTypeAttributesCommandText, null,
				r =>
				{
					var typeFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TYPE_NAME"]));
					OracleSchemaObject typeObject;
					if (!AllObjects.TryGetValue(typeFullyQualifiedName, out typeObject))
						return null;

					var type = (OracleTypeBase)typeObject;
					// TODO:
					return type;
				})
				.ToArray();

			_allFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(GetUserFunctionMetadata().SqlFunctions));

			foreach (var functionMetadata in _allFunctionMetadata.SqlFunctions)
			{
				if (functionMetadata.IsPackageFunction)
				{
					OracleSchemaObject packageObject;
					if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Package), out packageObject))
					{
						((OraclePackage)packageObject).Functions.Add(functionMetadata);
					}
				}
				else
				{
					OracleSchemaObject functionObject;
					if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Name), out functionObject))
					{
						((OracleFunction)functionObject).Metadata = functionMetadata;
					}
				}
			}

			_allObjects = allObjects;
			//var ftmp = _allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner.Contains("CA_DEV")).ToArray();
			//var ftmp = _allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Package.Contains("DBMS_RANDOM")).ToArray();
			//var otmp = dataObjectMetadata.Where(o => o.Key.NormalizedName.Contains("DBMS_RANDOM")).ToArray();
			//var otmp = allObjects.Where(o => o.Key.NormalizedName.Contains("XMLTYPE")).ToArray();

			_lastRefresh = DateTime.Now;

			_isRefreshing = false;
			RaiseEvent(RefreshFinished);
		}

		private void RaiseEvent(EventHandler eventHandler)
		{
			if (eventHandler != null)
			{
				eventHandler(this, EventArgs.Empty);
			}
		}

		private static void AddSchemaObjectToDictionary(Dictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects, OracleSchemaObject schemaObject)
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

		private static string QualifyStringObject(object stringValue)
		{
			return String.Format("{0}{1}{0}", "\"", stringValue);
		}

		private void LoadSchemaNames()
		{
			const string selectAllSchemasCommandText = "SELECT USERNAME FROM SYS.ALL_USERS";
			var schemaSource = ExecuteReader(
				selectAllSchemasCommandText, null,
				r => ((string)r["USERNAME"]))
				.ToArray();

			_schemas = new HashSet<string>(schemaSource);
			_allSchemas = new HashSet<string>(schemaSource.Select(QualifyStringObject)) { SchemaPublic };
		}
	}
}
