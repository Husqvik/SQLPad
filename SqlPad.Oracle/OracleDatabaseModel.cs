using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : IDatabaseModel
	{
		private static readonly object LockObject = new object();
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private const string SqlFuntionMetadataFileName = "OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml";
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));
		private static bool _isRefreshing;

		public const string SchemaPublic = "\"PUBLIC\"";

		public OracleDatabaseModel(ConnectionStringSettings connectionString)
		{
			ConnectionString = connectionString;
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);

			string metadata;
			if (MetadataCache.TryLoadMetadata(SqlFuntionMetadataFileName, out metadata))
			{
				using (var reader = XmlReader.Create(new StringReader(metadata)))
				{
					BuiltInFunctionMetadata = (OracleFunctionMetadataCollection)Serializer.ReadObject(reader);
				}

				AllFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(DatabaseModelFake.Instance.AllFunctionMetadata.SqlFunctions).ToArray());
			}
			else
			{
				if (_isRefreshing)
					return;

				lock (LockObject)
				{
					if (_isRefreshing)
						return;

					_isRefreshing = true;

					Task.Factory.StartNew(GenerateSqlFunctionMetadata);
				}
			}
		}

		public OracleFunctionMetadataCollection BuiltInFunctionMetadata { get; private set; }

		public OracleFunctionMetadataCollection AllFunctionMetadata { get; private set; }

		public ConnectionStringSettings ConnectionString { get; private set; }

		public string CurrentSchema
		{
			get { return _oracleConnectionString.UserID; }
		}

		public ICollection<string> Schemas { get { return DatabaseModelFake.Instance.Schemas; } }
		public IDictionary<IObjectIdentifier, IDatabaseObject> Objects { get { return DatabaseModelFake.Instance.Objects; } }
		public IDictionary<IObjectIdentifier, IDatabaseObject> AllObjects { get { return DatabaseModelFake.Instance.AllObjects; } }

		public void Refresh()
		{
		}

		public SchemaObjectResult GetObject(OracleObjectIdentifier objectIdentifier)
		{
			OracleDataObject schemaObject = null;
			var schemaFound = false;

			if (String.IsNullOrEmpty(objectIdentifier.NormalizedOwner))
			{
				var currentSchemaObject = OracleObjectIdentifier.Create(CurrentSchema, objectIdentifier.NormalizedName);
				var publicSchemaObject = OracleObjectIdentifier.Create(SchemaPublic, objectIdentifier.NormalizedName);

				if (AllObjects.ContainsKey(currentSchemaObject))
					schemaObject = (OracleDataObject)AllObjects[currentSchemaObject];
				else if (AllObjects.ContainsKey(publicSchemaObject))
					schemaObject = (OracleDataObject)AllObjects[publicSchemaObject];
			}
			else
			{
				schemaFound = Schemas.Contains(objectIdentifier.NormalizedOwner);

				if (schemaFound && AllObjects.ContainsKey(objectIdentifier))
					schemaObject = (OracleDataObject)AllObjects[objectIdentifier];
			}

			return new SchemaObjectResult
			{
				SchemaFound = schemaFound,
				SchemaObject = schemaObject
			};
		}

		private OracleFunctionMetadataCollection GetAllFunctionMetadata()
		{
			const string getFunctionMetadataCommandText =
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

			const string getParameterMetadataCommandText =
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

			return GetFunctionMetadataCollection(getFunctionMetadataCommandText, getParameterMetadataCommandText);
		}

		private void GenerateSqlFunctionMetadata()
		{
			const string getFunctionMetadataCommandText =
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
        (ALL_PROCEDURES.OBJECT_TYPE = 'FUNCTION' OR (ALL_PROCEDURES.OBJECT_TYPE = 'PACKAGE' AND ALL_PROCEDURES.PROCEDURE_NAME IS NOT NULL))) PROCEDURES
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

			const string getParameterMetadataCommandText =
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

			BuiltInFunctionMetadata = GetFunctionMetadataCollection(getFunctionMetadataCommandText, getParameterMetadataCommandText);

			using (var writer = XmlWriter.Create(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
			{
				Serializer.WriteObject(writer, BuiltInFunctionMetadata);
			}

			var allFunctionMetadata = GetAllFunctionMetadata();

			var test = new OracleFunctionMetadataCollection(allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner == "husqvik".ToQuotedIdentifier()).ToArray());
			using (var writer = XmlWriter.Create(@"D:\TestFunctionCollection.xml"))
			{
				Serializer.WriteObject(writer, test);
			}

			_isRefreshing = false;
		}

		private OracleFunctionMetadataCollection GetFunctionMetadataCollection(string getFunctionMetadataCommandText, string getParameterMetadataCommandText)
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
						var values = new Object[14];
						while (reader.Read())
						{
							reader.GetValues(values);
							var functionMetadata = new OracleFunctionMetadata(values, true);
							functionMetadataDictionary.Add(functionMetadata.Identifier, functionMetadata);
						}
					}

					command.CommandText = getParameterMetadataCommandText;

					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							var identifier = OracleFunctionIdentifier.CreateFromReaderValues(reader[0], reader[1], reader[2], reader[3]);

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
	}
}
