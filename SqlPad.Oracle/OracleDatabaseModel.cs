using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : IDatabaseModel
	{
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private const string SqlFuntionMetadataFileName = "OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml";
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleSqlFunctionMetadataCollection));
		private OracleSqlFunctionMetadataCollection _sqlFunctionMetadata;

		public OracleDatabaseModel(ConnectionStringSettings connectionString)
		{
			ConnectionString = connectionString;
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);

			string metadata;
			if (MetadataCache.TryLoadMetadata(SqlFuntionMetadataFileName, out metadata))
			{
				using (var reader = XmlReader.Create(new StringReader(metadata)))
				{
					_sqlFunctionMetadata = (OracleSqlFunctionMetadataCollection)Serializer.ReadObject(reader);
				}
			}
			else
			{
				Task.Factory.StartNew(GenerateSqlFunctionMetadata);
			}
		}

		public ConnectionStringSettings ConnectionString { get; private set; }

		public string CurrentSchema
		{
			get { return _oracleConnectionString.UserID; }
		}

		public ICollection<string> Schemas { get; private set; }
		public IDictionary<IObjectIdentifier, IDatabaseObject> Objects { get; private set; }
		public IDictionary<IObjectIdentifier, IDatabaseObject> AllObjects { get; private set; }

		public void Refresh()
		{
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

			_sqlFunctionMetadata = new OracleSqlFunctionMetadataCollection(sqlFunctionMetadata);

			using (var writer = XmlWriter.Create(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
			{
				Serializer.WriteObject(writer, _sqlFunctionMetadata);
			}
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
	}

	[DataContract]
	[DebuggerDisplay("OracleSqlFunctionMetadata (Name={Name}; DataType={DataType}; IsAnalytic={IsAnalytic}; IsAggregate={IsAggregate}; MinimumArguments={MinimumArguments}; MaximumArguments={MaximumArguments})")]
	public class OracleSqlFunctionMetadata
	{
		internal OracleSqlFunctionMetadata(IList<object> values)
		{
			FunctionId = Convert.ToInt32(values[0]);
			Name = (string)values[1];
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
