using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class ColumnDetailDataProvider : ModelDataProvider<ColumnDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;

		public ColumnDetailDataProvider(ColumnDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectColumnStatisticsCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}
			
			DataModel.DistinctValueCount = Convert.ToInt32(reader["NUM_DISTINCT"]);
			DataModel.LastAnalyzed = (DateTime)reader["LAST_ANALYZED"];
			DataModel.NullValueCount = Convert.ToInt32(reader["NUM_NULLS"]);
			DataModel.SampleSize = OracleReaderValueConvert.ToInt32(reader["SAMPLE_SIZE"]);
			DataModel.AverageValueSize = Convert.ToInt32(reader["AVG_COL_LEN"]);
			DataModel.HistogramBucketCount = Convert.ToInt32(reader["NUM_BUCKETS"]);
			DataModel.HistogramType = (string)reader["HISTOGRAM"];
		}
	}

	internal class ConstraintDataProvider : ModelDataProvider<ModelWithConstraints>
	{
		private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;

		public ConstraintDataProvider(ModelWithConstraints dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));

			if (String.IsNullOrEmpty(_columnName))
			{
				command.CommandText = OracleDatabaseCommands.SelectTableConstraintDescriptionCommandText;
			}
			else
			{
				command.CommandText = OracleDatabaseCommands.SelectColumnConstraintDescriptionCommandText;
				command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
			}

			command.InitialLONGFetchSize = 32767;
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var deleteRuleRaw = OracleReaderValueConvert.ToString(reader["DELETE_RULE"]);
				var constraintDetail =
					new ConstraintDetailsModel
					{
						Owner = (string)reader["OWNER"],
						Name = (string)reader["CONSTRAINT_NAME"],
						Type = GetConstraintType((string)reader["CONSTRAINT_TYPE"]),
						SearchCondition = OracleReaderValueConvert.ToString(reader["SEARCH_CONDITION"]),
						DeleteRule = TextInfo.ToTitleCase(deleteRuleRaw.ToLowerInvariant()),
						IsEnabled = (string)reader["STATUS"] == "ENABLED",
						IsDeferrable = (string)reader["DEFERRABLE"] == "DEFERRABLE",
						IsDeferred = (string)reader["DEFERRED"] == "DEFERRED",
						IsValidated = (string)reader["VALIDATED"] == "VALIDATED",
						Reliability = OracleReaderValueConvert.ToString(reader["RELY"]) == "RELY" ? "Relied" : "Enforced",
						LastChange = (DateTime)reader["LAST_CHANGE"]
					};

				DataModel.ConstraintDetails.Add(constraintDetail);
			}
		}

		private string GetConstraintType(string code)
		{
			switch (code)
			{
				case "C":
					return "Check";
				case "P":
					return "Primary key";
				case "U":
					return "Unique key";
				case "R":
					return "Referential integrity";
				case "V":
					return "With check option";
				case "O":
					return "With read only";
				default:
					throw new NotSupportedException($"Constraint type '{code}' is not supported. ");
			}
		}
	}

	internal class ColumnDetailHistogramDataProvider : ModelDataProvider<ColumnDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;

		public ColumnDetailHistogramDataProvider(ColumnDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectColumnHistogramCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var histogramValues = new List<double>();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				histogramValues.Add(Convert.ToInt32(reader["ENDPOINT_NUMBER"]));
			}

			DataModel.HistogramValues = histogramValues;
		}

		public override bool IsValid => DataModel.HistogramType != null && DataModel.HistogramType != "None";
	}

	internal class ColumnDetailInMemoryDataProvider : ModelDataProvider<ColumnDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;
		private readonly Version _oracleVersion;

		public ColumnDetailInMemoryDataProvider(ColumnDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string columnName, Version oracleVersion)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
			_oracleVersion = oracleVersion;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectColumnInMemoryDetailsCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}

			DataModel.InMemoryCompression = (string)reader["INMEMORY_COMPRESSION"];
		}

		public override bool IsValid => InMemoryHelper.HasInMemorySupport(_oracleVersion);
	}

	internal static class InMemoryHelper
	{
		public static bool HasInMemorySupport(Version oracleVersion)
		{
			return String.CompareOrdinal(oracleVersion.ToString(), "12.1.0.2") >= 0;
		}
	}

	internal class TableDetailDataProvider : ModelDataProvider<TableDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;

		public TableDetailDataProvider(TableDetailsModel dataModel, OracleObjectIdentifier objectIdentifier)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectTableDetailsCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}

			var loggingRaw = reader["LOGGING"];
			DataModel.RowCount = OracleReaderValueConvert.ToInt32(reader["NUM_ROWS"]);
			DataModel.LastAnalyzed = OracleReaderValueConvert.ToDateTime(reader["LAST_ANALYZED"]);
			DataModel.AverageRowSize = OracleReaderValueConvert.ToInt32(reader["AVG_ROW_LEN"]);
			DataModel.BlockCount = OracleReaderValueConvert.ToInt32(reader["BLOCKS"]);
			DataModel.Compression = OracleReaderValueConvert.ToString(reader["COMPRESSION"]);
			DataModel.Organization = OracleReaderValueConvert.ToString(reader["ORGANIZATION"]);
			DataModel.ParallelDegree = OracleReaderValueConvert.ToString(reader["DEGREE"]);
			DataModel.ClusterName = OracleReaderValueConvert.ToString(reader["CLUSTER_NAME"]);
			DataModel.TablespaceName = OracleReaderValueConvert.ToString(reader["TABLESPACE_NAME"]);
			DataModel.SampleRows = OracleReaderValueConvert.ToInt64(reader["SAMPLE_SIZE"]);
			DataModel.Logging = loggingRaw == DBNull.Value ? (bool?)null : String.Equals((string)loggingRaw, "YES");
			DataModel.IsTemporary = (string)reader["TEMPORARY"] == "Y";
		}
	}

	internal class TablespaceDetailDataProvider : ModelDataProvider<TablespaceDetailModel>
	{
		public TablespaceDetailDataProvider(TablespaceDetailModel dataModel)
			: base(dataModel)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectTablespaceDetails;
			command.AddSimpleParameter("TABLESPACE_NAME", DataModel.Name);
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}

			DataModel.BlockSize = Convert.ToInt32(reader["BLOCK_SIZE"]);
			DataModel.InitialExtent = Convert.ToInt32(reader["INITIAL_EXTENT"]);
			DataModel.NextExtent = OracleReaderValueConvert.ToInt32(reader["NEXT_EXTENT"]);
			DataModel.MinimumExtents = Convert.ToInt32(reader["MIN_EXTENTS"]);
			DataModel.MaximumExtents = OracleReaderValueConvert.ToInt32(reader["MAX_EXTENTS"]);
			DataModel.SegmentMaximumSizeBlocks = Convert.ToInt64(reader["MAX_SIZE"]);
			DataModel.PercentIncrease = OracleReaderValueConvert.ToInt32(reader["PCT_INCREASE"]);
			DataModel.MinimumExtentSizeBytes = Convert.ToInt32(reader["MIN_EXTLEN"]);
			DataModel.Status = (string)reader["STATUS"];
			DataModel.Contents = (string)reader["CONTENTS"];
			DataModel.Logging = String.Equals((string)reader["LOGGING"], "LOGGING");
			DataModel.ForceLogging = String.Equals((string)reader["FORCE_LOGGING"], "YES");
			DataModel.ExtentManagement = (string)reader["EXTENT_MANAGEMENT"];
			DataModel.AllocationType = (string)reader["ALLOCATION_TYPE"];
			DataModel.SegmentSpaceManagement = (string)reader["SEGMENT_SPACE_MANAGEMENT"];
			DataModel.DefaultTableCompression = (string)reader["DEFAULT_TABLE_COMPRESSION"];
			DataModel.Retention = (string)reader["RETENTION"];
			DataModel.IsBigFile = String.Equals((string)reader["BIGFILE"], "YES");
			DataModel.PredicateEvaluation = (string)reader["PREDICATE_EVALUATION"];
			DataModel.IsEncrypted = String.Equals((string)reader["ENCRYPTED"], "YES");
			DataModel.CompressFor = OracleReaderValueConvert.ToString(reader["COMPRESS_FOR"]);
		}
	}

	internal class TablespaceFilesDataProvider : ModelDataProvider<TablespaceDetailModel>
	{
		public TablespaceFilesDataProvider(TablespaceDetailModel dataModel)
			: base(dataModel)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectTablespaceDatafiles;
			command.AddSimpleParameter("TABLESPACE_NAME", DataModel.Name);
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var dataFileModel =
					new DatafileDetailModel
					{
						FileName = (string)reader["FILE_NAME"],
						FileId = Convert.ToInt32(reader["FILE_ID"]),
						SizeBytes = Convert.ToInt64(reader["BYTES"]),
						SizeBlocks = Convert.ToInt64(reader["BLOCKS"]),
						Status = OracleReaderValueConvert.ToString(reader["STATUS"]),
						RelativeFileNumber = Convert.ToInt32(reader["RELATIVE_FNO"]),
						IsAutoextensible = String.Equals((string)reader["AUTOEXTENSIBLE"], "YES"),
						MaximumSizeBytes = Convert.ToInt64(reader["MAXBYTES"]),
						MaximumSizeBlocks = Convert.ToInt64(reader["MAXBLOCKS"]),
						IncrementByBlocks = Convert.ToInt64(reader["INCREMENT_BY"]),
						UserSizeBytes = Convert.ToInt64(reader["USER_BYTES"]),
						UserSizeBlocks = Convert.ToInt64(reader["USER_BLOCKS"]),
						OnlineStatus = (string)reader["ONLINE_STATUS"]
					};

				DataModel.Datafiles.Add(dataFileModel);
			}
		}
	}

	internal class ProfileDetailsDataProvider : ModelDataProvider<ProfileDetailModel>
	{
		public ProfileDetailsDataProvider(ProfileDetailModel dataModel)
			: base(dataModel)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectProfileDetails;
			command.AddSimpleParameter("PROFILE", DataModel.Name);
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var resourceName = (string)reader["RESOURCE_NAME"];
				var limitRaw = (string)reader["LIMIT"];
				int? limit = null;
				var passwordVerifyFunction = String.Empty;
				if (String.Equals(resourceName, "PASSWORD_VERIFY_FUNCTION"))
				{
					passwordVerifyFunction = String.Equals(limitRaw, "NULL") ? "Not set" : limitRaw;
				}
				else
				{
					limit = String.Equals(limitRaw, "UNLIMITED") ? (int?)null : Convert.ToInt32(limitRaw);
				}

				switch (resourceName)
				{
					case "SESSIONS_PER_USER":
						DataModel.SessionsPerUser = limit;
						break;
					case "COMPOSITE_LIMIT":
						DataModel.CompositeLimit = limit;
						break;
					case "CPU_PER_SESSION":
						DataModel.CpuPerSession = limit == null ? (TimeSpan?)null : TimeSpan.FromMilliseconds(limit.Value * 10L);
						break;
					case "CPU_PER_CALL":
						DataModel.CpuPerCall = limit == null ? (TimeSpan?)null : TimeSpan.FromMilliseconds(limit.Value * 10L);
						break;
					case "LOGICAL_READS_PER_SESSION":
						DataModel.LogicalReadsPerSession = limit;
						break;
					case "LOGICAL_READS_PER_CALL":
						DataModel.LogicalReadsPerCall = limit;
						break;
					case "IDLE_TIME":
						DataModel.IdleTime = limit;
						break;
					case "CONNECT_TIME":
						DataModel.ConnectTime = limit;
						break;
					case "PRIVATE_SGA":
						DataModel.PrivateSystemGlobalArea = limit;
						break;
					case "FAILED_LOGIN_ATTEMPTS":
						DataModel.FailedLoginAttempts = limit;
						break;
					case "PASSWORD_LIFE_TIME":
						DataModel.PasswordLifeTime = limit;
						break;
					case "PASSWORD_REUSE_TIME":
						DataModel.PasswordReuseTime = limit;
						break;
					case "PASSWORD_REUSE_MAX":
						DataModel.PasswordReuseMax = limit;
						break;
					case "PASSWORD_VERIFY_FUNCTION":
						DataModel.PasswordVerifyFunction = passwordVerifyFunction;
						break;
					case "PASSWORD_LOCK_TIME":
						DataModel.PasswordLockTime = limit;
						break;
					case "PASSWORD_GRACE_TIME":
						DataModel.PasswordGraceTime = limit;
						break;
				}
			}
		}
	}

	internal class CommentDataProvider : ModelDataProvider<IModelWithComment>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;

		public CommentDataProvider(IModelWithComment dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));

			if (String.IsNullOrEmpty(_columnName))
			{
				command.CommandText = String.Format(OracleDatabaseCommands.SelectTableCommentCommandText);
			}
			else
			{
				command.CommandText = String.Format(OracleDatabaseCommands.SelectColumnCommentCommandText);
				command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
			}
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}

			DataModel.Comment = OracleReaderValueConvert.ToString(reader["COMMENTS"]);
		}
	}

	internal class TableSpaceAllocationDataProvider : ModelDataProvider<SegmentDetailsModelBase>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _partitionName;

		public TableSpaceAllocationDataProvider(SegmentDetailsModelBase dataModel, OracleObjectIdentifier objectIdentifier, string partitionName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_partitionName = partitionName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectTableAllocatedBytesCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("PARTITION_NAME", _partitionName.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}

			DataModel.LargeObjectBytes = OracleReaderValueConvert.ToInt64(reader["LOB_BYTES"]);
			DataModel.AllocatedBytes = OracleReaderValueConvert.ToInt64(reader["IN_ROW_BYTES"]) + (DataModel.LargeObjectBytes ?? 0);
		}
	}

	internal class IndexDetailDataProvider : ModelDataProvider<IModelWithIndexes>
	{
		private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;
		private readonly string _columnName;
		private readonly OracleObjectIdentifier _objectIdentifier;

		public IndexDetailDataProvider(IModelWithIndexes dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = String.Format(OracleDatabaseCommands.SelectIndexDescriptionCommandText);
			command.AddSimpleParameter("TABLE_OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", String.IsNullOrEmpty(_columnName) ? null : _columnName.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var degreeOfParallelismRaw = (string)reader["DEGREE"];
				var indexDetails =
					new IndexDetailsModel
					{
						Owner = (string)reader["OWNER"],
						Name = (string)reader["INDEX_NAME"],
						Type = TextInfo.ToTitleCase(((string)reader["INDEX_TYPE"]).ToLowerInvariant()),
						IsUnique = (string)reader["UNIQUENESS"] == "UNIQUE",
						Compression = TextInfo.ToTitleCase(((string)reader["COMPRESSION"]).ToLowerInvariant()),
						PrefixLength = OracleReaderValueConvert.ToInt32(reader["PREFIX_LENGTH"]),
						Logging = OracleReaderValueConvert.ToString(reader["LOGGING"]) == "LOGGING",
						ClusteringFactor = OracleReaderValueConvert.ToInt64(reader["CLUSTERING_FACTOR"]),
						Status = TextInfo.ToTitleCase(((string)reader["STATUS"]).ToLowerInvariant()),
						Rows = OracleReaderValueConvert.ToInt64(reader["NUM_ROWS"]),
						SampleRows = OracleReaderValueConvert.ToInt64(reader["SAMPLE_SIZE"]),
						DistinctKeys = OracleReaderValueConvert.ToInt64(reader["DISTINCT_KEYS"]),
						LastAnalyzed = OracleReaderValueConvert.ToDateTime(reader["LAST_ANALYZED"]),
						Blocks = OracleReaderValueConvert.ToInt32(reader["BLOCKS"]),
						LeafBlocks = OracleReaderValueConvert.ToInt32(reader["LEAF_BLOCKS"]),
						Bytes = OracleReaderValueConvert.ToInt64(reader["BYTES"]),
						DegreeOfParallelism = degreeOfParallelismRaw == "DEFAULT" ? (int?)null : Convert.ToInt32(degreeOfParallelismRaw.Trim()),
						TablespaceName = OracleReaderValueConvert.ToString(reader["TABLESPACE_NAME"])
					};

				DataModel.IndexDetails.Add(indexDetails);
			}
		}
	}

	internal class IndexColumnDataProvider : ModelDataProvider<IModelWithIndexes>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;
		private Dictionary<OracleObjectIdentifier, IndexDetailsModel> _indexes;

		public IndexColumnDataProvider(IModelWithIndexes dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectIndexColumnDescriptionCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", String.IsNullOrEmpty(_columnName) ? null : _columnName.Trim('"'));

			_indexes = DataModel.IndexDetails.ToDictionary(i => OracleObjectIdentifier.Create(i.Owner, i.Name));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var indexOwner = (string)reader["INDEX_OWNER"];
				var indexName = (string)reader["INDEX_NAME"];
				var indexIdentifier = OracleObjectIdentifier.Create(indexOwner, indexName);

				IndexDetailsModel indexModel;
				if (!_indexes.TryGetValue(indexIdentifier, out indexModel))
				{
					continue;
				}

				var indexColumn =
					new IndexColumnModel
					{
						ColumnName = (string)reader["COLUMN_NAME"],
						SortOrder = (string)reader["DESCEND"] == "ASC" ? SortOrder.Ascending : SortOrder.Descending
					};

				indexModel.Columns.Add(indexColumn);
			}
		}
	}

	internal class CompilationErrorDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly List<CompilationError> _errors = new List<CompilationError>();
		
		private readonly string _owner;
		private readonly string _objectName;
		private readonly StatementBase _statement;

		public IReadOnlyList<CompilationError> Errors => _errors.AsReadOnly();

		public CompilationErrorDataProvider(StatementBase statement, string currentSchema)
			: base(null)
		{
			_statement = statement;
			
			OracleObjectIdentifier objectIdentifier;
			if (!OracleStatement.TryGetPlSqlUnitName(statement, out objectIdentifier))
			{
				return;
			}
			
			if (!objectIdentifier.HasOwner)
			{
				objectIdentifier = OracleObjectIdentifier.Create(currentSchema, objectIdentifier.Name);
			}

			_owner = objectIdentifier.NormalizedOwner.Trim('"');
			_objectName = objectIdentifier.NormalizedName.Trim('"');
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectCompilationErrorsCommandText;
			command.AddSimpleParameter("OWNER", _owner);
			command.AddSimpleParameter("NAME", _objectName);
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var error =
					new CompilationError
					{
						Owner = _owner,
						ObjectName = _objectName,
						ObjectType = (string)reader["TYPE"],
						Line = Convert.ToInt32(reader["LINE"]) - 1,
						Column = Convert.ToInt32(reader["POSITION"]),
						Message = (string)reader["TEXT"],
						Severity = (string)reader["ATTRIBUTE"],
						Code = Convert.ToInt32(reader["MESSAGE_NUMBER"]),
						Statement = _statement
					};

				_errors.Add(error);
			}
		}

		public override bool IsValid => !String.IsNullOrEmpty(_objectName);
	}

	internal class TableInMemorySpaceAllocationDataProvider : ModelDataProvider<TableDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly Version _oracleVersion;

		public TableInMemorySpaceAllocationDataProvider(TableDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, Version oracleVersion)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_oracleVersion = oracleVersion;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectTableInMemoryAllocatedBytesCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("SEGMENT_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			if (!await reader.ReadAsynchronous(cancellationToken))
			{
				return;
			}

			DataModel.InMemoryCompression = OracleReaderValueConvert.ToString(reader["INMEMORY_COMPRESSION"]);

			DataModel.SetInMemoryAllocationStatus(
				 OracleReaderValueConvert.ToInt64(reader["INMEMORY_SIZE"]),
				 OracleReaderValueConvert.ToInt64(reader["BYTES"]),
				 OracleReaderValueConvert.ToInt64(reader["BYTES_NOT_POPULATED"]),
				 OracleReaderValueConvert.ToString(reader["POPULATE_STATUS"]));
		}

		public override bool HasScalarResult => false;

		public override bool IsValid => InMemoryHelper.HasInMemorySupport(_oracleVersion);
	}

	internal class ObjectScriptDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly OracleSchemaObject _schemaObject;

		public string ScriptText { get; private set; }

		public ObjectScriptDataProvider(OracleSchemaObject schemaObject) : base(null)
		{
			_schemaObject = schemaObject;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = _schemaObject.Type.In(OracleSchemaObjectType.Table, OracleSchemaObjectType.View, OracleSchemaObjectType.MaterializedView)
				? OracleDatabaseCommands.SelectComplexObjectScriptCommandText
				: OracleDatabaseCommands.SelectSimpleObjectScriptCommandText;

			command.AddSimpleParameter("OBJECT_TYPE", _schemaObject.Type.Replace(' ', '_').ToUpperInvariant());
			command.AddSimpleParameter("NAME", _schemaObject.FullyQualifiedName.Name.Trim('"'));
			command.AddSimpleParameter("SCHEMA", _schemaObject.FullyQualifiedName.Owner.Trim('"'));
		}

		public override void MapScalarValue(object value)
		{
			ScriptText = (string)value;
		}

		public override bool HasScalarResult { get; } = true;
	}

	internal class DisplayCursorDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly string _sqlId;
		private readonly int? _childNumber;
		private readonly bool _displayLastCursor;
		private readonly bool _resolveAdaptivePlan;

		public string PlanText { get; private set; }

		private DisplayCursorDataProvider(Version oracleVersion) : this(null, 0, oracleVersion)
		{
			_displayLastCursor = true;
		}

		public DisplayCursorDataProvider(string sqlId, int childNumber, Version oracleVersion)
			: base(null)
		{
			_sqlId = sqlId;
			_childNumber = childNumber;
			_resolveAdaptivePlan = oracleVersion.Major >= 12;
		}

		public static DisplayCursorDataProvider CreateDisplayLastCursorDataProvider(Version oracleVersion)
		{
			return new DisplayCursorDataProvider(oracleVersion);
		}

		public override void InitializeCommand(OracleCommand command)
		{
			
			command.CommandText = String.Format(OracleDatabaseCommands.SelectExecutionPlanTextCommandTextBase, _resolveAdaptivePlan ? "ADAPTIVE" : null);
			command.AddSimpleParameter("SQL_ID", _sqlId);
			command.AddSimpleParameter("CHILD_NUMBER", _childNumber);
		}

		public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var builder = new StringBuilder();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				builder.AppendLine(Convert.ToString(reader["PLAN_TABLE_OUTPUT"]));
			}

			PlanText = builder.ToString();
		}

		public override bool IsValid => _displayLastCursor || _sqlId != null;
	}

	internal class RemoteTableColumnDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly List<string> _columns = new List<string>();
		private readonly string _commandText;

		public RemoteTableColumnDataProvider(string databaseLink, OracleObjectIdentifier objectIdentifer) : base(null)
		{
			_commandText = $"SELECT * FROM {objectIdentifer.ToNormalizedString()}@{databaseLink} WHERE 1 = 0";
		}

		public IReadOnlyList<string> Columns => _columns.AsReadOnly();

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = _commandText;
		}

		public override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var columnNames = OracleConnectionAdapter.GetColumnHeadersFromReader(reader)
				.Select(h => $"\"{h.Name}\"");

			_columns.AddRange(columnNames);
			return Task.FromResult(0);
		}
	}
}
