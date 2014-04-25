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
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleSqlFunctionMetadataCollection));
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
					SqlFunctionMetadata = (OracleSqlFunctionMetadataCollection)Serializer.ReadObject(reader);
				}
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

		public OracleSqlFunctionMetadataCollection SqlFunctionMetadata { get; private set; }

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

		private void GenerateSqlFunctionMetadata()
		{
			var sqlFunctionMetadata = new List<OracleSqlFunctionMetadata>();

			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText =
						@"SELECT
		FUNC_ID,
	    NAME,
		CASE WHEN DATATYPE = 'UNKNOWN' THEN NULL ELSE DATATYPE END DATATYPE,
		VERSION,
	    ANALYTIC,
	    AGGREGATE,
	    OFFLOADABLE,
	    MINARGS,
	    MAXARGS,
		DISP_TYPE,
		DESCR
	FROM
	    V$SQLFN_METADATA";

					connection.Open();

					using (var reader = command.ExecuteReader())
					{
						var values = new Object[11];
						while (reader.Read())
						{
							reader.GetValues(values);
							sqlFunctionMetadata.Add(new OracleSqlFunctionMetadata(values));
						}
					}
				}
			}

			SqlFunctionMetadata = new OracleSqlFunctionMetadataCollection(sqlFunctionMetadata.AsReadOnly());

			using (var writer = XmlWriter.Create(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
			{
				Serializer.WriteObject(writer, SqlFunctionMetadata);
			}

			_isRefreshing = false;
		}
	}

	[DataContract]
	public class OracleSqlFunctionMetadataCollection
	{
		internal OracleSqlFunctionMetadataCollection(ICollection<OracleSqlFunctionMetadata> metadata)
		{
			SqlFunctions = metadata;
			Timestamp = DateTime.UtcNow;
		}

		[DataMember]
		public ICollection<OracleSqlFunctionMetadata> SqlFunctions { get; private set; }

		public string Rdbms
		{
			get { return "Oracle"; }
		}

		public string Version
		{
			get { return "12.1.0.1.0"; }
		}

		[DataMember]
		public DateTime Timestamp { get; private set; }

		public OracleSqlFunctionMetadata GetSqlFunctionMetadata(string normalizedName, bool hasAnalyticClause)
		{
			var functionMetadataCollection = SqlFunctions.Where(m => m.Name == normalizedName).ToArray();
			var functionMetadata = functionMetadataCollection.FirstOrDefault(m => m.IsAnalytic == hasAnalyticClause)
			                       ?? functionMetadataCollection.FirstOrDefault();

			return functionMetadata;
		}
	}

	[DataContract]
	[DebuggerDisplay("OracleSqlFunctionMetadata (Name={Name}; DataType={DataType}; IsAnalytic={IsAnalytic}; IsAggregate={IsAggregate}; MinimumArguments={MinimumArguments}; MaximumArguments={MaximumArguments})")]
	public class OracleSqlFunctionMetadata
	{
		public const string DisplayTypeParenthesis = "PARENTHESIS";

		internal OracleSqlFunctionMetadata(IList<object> values)
		{
			FunctionId = Convert.ToInt32(values[0]);
			Name = ((string)values[1]).ToQuotedIdentifier();
			DataType = values[2] == DBNull.Value ? null : (string)values[2];
			Version = (string)values[3];
			IsAnalytic = (string)values[4] == "YES";
			IsAggregate = (string)values[5] == "YES";
			IsOffloadable = (string)values[6] == "YES";
			MinimumArguments = Convert.ToInt32(values[7]);
			MaximumArguments = Convert.ToInt32(values[8]);
			DisplayType = (string)values[9];
			Description = (string)values[10];
		}

		[DataMember]
		public int FunctionId { get; private set; }

		[DataMember]
		public string Name { get; private set; }

		[DataMember]
		public string DataType { get; private set; }

		[DataMember]
		public string Version { get; private set; }

		[DataMember]
		public bool IsAnalytic { get; private set; }

		[DataMember]
		public bool IsAggregate { get; private set; }

		[DataMember]
		public bool IsOffloadable { get; private set; }

		[DataMember]
		public int MinimumArguments { get; private set; }

		[DataMember]
		public int MaximumArguments { get; private set; }

		[DataMember]
		public string DisplayType { get; private set; }

		[DataMember]
		public string Description { get; private set; }
	}
}
