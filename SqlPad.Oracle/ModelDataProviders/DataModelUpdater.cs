using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
			command.CommandText = DatabaseCommands.SelectColumnStatisticsCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
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
				command.CommandText = DatabaseCommands.SelectTableConstraintDescriptionCommandText;
			}
			else
			{
				command.CommandText = DatabaseCommands.SelectColumnConstraintDescriptionCommandText;
				command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
			}

			command.InitialLONGFetchSize = 32767;
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			while (reader.Read())
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
					throw new NotSupportedException(String.Format("Constraint type '{0}' is not supported. ", code));
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
			command.CommandText = DatabaseCommands.SelectColumnHistogramCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			var histogramValues = new List<double>();

			while (reader.Read())
			{
				histogramValues.Add(Convert.ToInt32(reader["ENDPOINT_NUMBER"]));
			}

			DataModel.HistogramValues = histogramValues;
		}

		public override bool IsValid
		{
			get { return DataModel.HistogramType != null && DataModel.HistogramType != "None"; }
		}
	}

	internal class ColumnDetailInMemoryDataProvider : ModelDataProvider<ColumnDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;
		private readonly string _oracleVersion;

		public ColumnDetailInMemoryDataProvider(ColumnDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string columnName, string oracleVersion)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
			_oracleVersion = oracleVersion;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.SelectColumnInMemoryDetailsCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

			DataModel.InMemoryCompression = (string)reader["INMEMORY_COMPRESSION"];
		}

		public override bool IsValid
		{
			get { return InMemoryHelper.HasInMemorySupport(_oracleVersion); }
		}
	}

	internal static class InMemoryHelper
	{
		public static bool HasInMemorySupport(string oracleVersion)
		{
			return String.CompareOrdinal(oracleVersion, "12.1.0.2.0") >= 0;
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
			command.CommandText = String.Format(DatabaseCommands.SelectTableDetailsCommandText);
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

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
			DataModel.Logging = (string)reader["LOGGING"] == "YES";
			DataModel.IsTemporary = (string)reader["TEMPORARY"] == "Y";
			DataModel.IsPartitioned = (string)reader["PARTITIONED"] == "YES";
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
				command.CommandText = String.Format(DatabaseCommands.SelectTableCommentCommandText);
			}
			else
			{
				command.CommandText = String.Format(DatabaseCommands.SelectColumnCommentCommandText);
				command.AddSimpleParameter("COLUMN_NAME", _columnName.Trim('"'));
			}
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

			DataModel.Comment = OracleReaderValueConvert.ToString(reader["COMMENTS"]);
		}
	}

	internal class TableSpaceAllocationDataProvider : ModelDataProvider<TableDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		
		public TableSpaceAllocationDataProvider(TableDetailsModel dataModel,  OracleObjectIdentifier objectIdentifier)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.SelectTableAllocatedBytesCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
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
			command.CommandText = String.Format(DatabaseCommands.SelectIndexDescriptionCommandText);
			command.AddSimpleParameter("TABLE_OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", String.IsNullOrEmpty(_columnName) ? null : _columnName.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			while (reader.Read())
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
						Logging = (string)reader["LOGGING"] == "LOGGING",
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

	internal class PartitionDataProvider
	{
		private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

		public IModelDataProvider PartitionDetailDataProvider { get; private set; }
		
		public IModelDataProvider SubPartitionDetailDataProvider { get; private set; }

		public PartitionDataProvider(TableDetailsModel dataModel, OracleObjectIdentifier objectIdentifier)
		{
			var owner = objectIdentifier.Owner.Trim('"');
			var tableName = objectIdentifier.Name.Trim('"');
			PartitionDetailDataProvider = new PartitionDetailDataProviderInternal(dataModel, owner, tableName);
			SubPartitionDetailDataProvider = new SubPartitionDetailDataProviderInternal(dataModel, owner, tableName);
		}

		private static void MapSegmentData(OracleDataReader reader, PartitionDetailsModelBase model)
		{
			model.HighValue = OracleReaderValueConvert.ToString(reader["HIGH_VALUE"]);
			model.TablespaceName = OracleReaderValueConvert.ToString(reader["TABLESPACE_NAME"]);
			model.Logging = (string)reader["LOGGING"] == "YES";
			model.Compression = TextInfo.ToTitleCase(((string)reader["COMPRESSION"]).ToLowerInvariant());
			model.RowCount = OracleReaderValueConvert.ToInt64(reader["NUM_ROWS"]);
			model.SampleRows = OracleReaderValueConvert.ToInt64(reader["SAMPLE_SIZE"]);
			model.LastAnalyzed = OracleReaderValueConvert.ToDateTime(reader["LAST_ANALYZED"]);
			model.BlockCount = OracleReaderValueConvert.ToInt32(reader["BLOCKS"]);
			model.AverageRowSize = OracleReaderValueConvert.ToInt32(reader["AVG_ROW_LEN"]);
		}

		private class PartitionDetailDataProviderInternal : ModelDataProvider<TableDetailsModel>
		{
			private readonly string _owner;
			private readonly string _tableName;

			public PartitionDetailDataProviderInternal(TableDetailsModel dataModel, string owner, string tableName)
				: base(dataModel)
			{
				_tableName = tableName;
				_owner = owner;
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = String.Format(DatabaseCommands.SelectTablePartitionDetailsCommandText);
				command.AddSimpleParameter("TABLE_OWNER", _owner);
				command.AddSimpleParameter("TABLE_NAME", _tableName);
				command.InitialLONGFetchSize = 255;
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				while (reader.Read())
				{
					var partitionDetails =
						new PartitionDetailsModel
						{
							Name = (string)reader["PARTITION_NAME"]
						};

					MapSegmentData(reader, partitionDetails);

					DataModel.AddPartition(partitionDetails);
				}
			}
		}

		private class SubPartitionDetailDataProviderInternal : ModelDataProvider<TableDetailsModel>
		{
			private readonly string _owner;
			private readonly string _tableName;

			public SubPartitionDetailDataProviderInternal(TableDetailsModel dataModel, string owner, string tableName)
				: base(dataModel)
			{
				_tableName = tableName;
				_owner = owner;
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = String.Format(DatabaseCommands.SelectTableSubPartitionsDetailsCommandText);
				command.AddSimpleParameter("TABLE_OWNER", _owner);
				command.AddSimpleParameter("TABLE_NAME", _tableName);
				command.InitialLONGFetchSize = 255;
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				while (reader.Read())
				{
					var subPartitionDetails =
						new SubPartitionDetailsModel
						{
							Name = (string)reader["SUBPARTITION_NAME"]
						};

					MapSegmentData(reader, subPartitionDetails);

					var partition = DataModel.GetPartitions((string)reader["PARTITION_NAME"]);
					partition.SubPartitionDetails.Add(subPartitionDetails);
				}
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
			command.CommandText = DatabaseCommands.SelectIndexColumnDescriptionCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", String.IsNullOrEmpty(_columnName) ? null : _columnName.Trim('"'));

			_indexes = DataModel.IndexDetails.ToDictionary(i => OracleObjectIdentifier.Create(i.Owner, i.Name));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			while (reader.Read())
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

		public IReadOnlyList<CompilationError> Errors { get { return _errors.AsReadOnly(); } } 

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

			_owner = objectIdentifier.Owner.Trim('"');
			_objectName = objectIdentifier.Name.Trim('"');
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.SelectCompilationErrorsCommandText;
			command.AddSimpleParameter("OWNER", _owner);
			command.AddSimpleParameter("NAME", _objectName);
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			while (reader.Read())
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

		public override bool IsValid
		{
			get { return !String.IsNullOrEmpty(_objectName); }
		}
	}

	internal class TableInMemorySpaceAllocationDataProvider : ModelDataProvider<TableDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _oracleVersion;

		public TableInMemorySpaceAllocationDataProvider(TableDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string oracleVersion)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_oracleVersion = oracleVersion;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.SelectTableInMemoryAllocatedBytesCommandText;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("SEGMENT_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
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

		public override bool HasScalarResult
		{
			get { return false; }
		}

		public override bool IsValid
		{
			get { return InMemoryHelper.HasInMemorySupport(_oracleVersion); }
		}
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
			command.CommandText = DatabaseCommands.SelectObjectScriptCommandText;
			command.AddSimpleParameter("OBJECT_TYPE", _schemaObject.Type.Replace(' ', '_').ToUpperInvariant());
			command.AddSimpleParameter("NAME", _schemaObject.FullyQualifiedName.Name.Trim('"'));
			command.AddSimpleParameter("SCHEMA", _schemaObject.FullyQualifiedName.Owner.Trim('"'));
		}

		public override void MapScalarValue(object value)
		{
			ScriptText = (string)value;
		}

		public override bool HasScalarResult { get { return true; } }
	}

	internal class DisplayCursorDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly string _sqlId;
		private readonly int? _childNumber;
		private readonly bool _displayLastCursor;

		public string PlanText { get; private set; }

		private DisplayCursorDataProvider() : base(null)
		{
			_displayLastCursor = true;
		}

		public DisplayCursorDataProvider(string sqlId, int childNumber)
			: base(null)
		{
			_sqlId = sqlId;
			_childNumber = childNumber;
		}

		public static DisplayCursorDataProvider CreateDisplayLastCursorDataProvider()
		{
			return new DisplayCursorDataProvider();
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.SelectExecutionPlanTextCommandText;
			command.AddSimpleParameter("SQL_ID", _sqlId);
			command.AddSimpleParameter("CHILD_NUMBER", _childNumber);
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			var builder = new StringBuilder();

			while (reader.Read())
			{
				builder.AppendLine(Convert.ToString(reader["PLAN_TABLE_OUTPUT"]));
			}

			PlanText = builder.ToString();
		}

		public override bool IsValid
		{
			get { return _displayLastCursor || _sqlId != null; }
		}
	}

	internal class RemoteTableColumnDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly List<string> _columns = new List<string>();
		private readonly string _commandText;

		public RemoteTableColumnDataProvider(string databaseLink, OracleObjectIdentifier objectIdentifer) : base(null)
		{
			_commandText = String.Format("SELECT * FROM {0}@{1} WHERE 1 = 0", objectIdentifer.ToNormalizedString(), databaseLink);
		}

		public IReadOnlyList<string> Columns
		{
			get { return _columns.AsReadOnly(); }
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = _commandText;
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			var columnNames = OracleDatabaseModel.GetColumnHeadersFromReader(reader)
				.Select(h => String.Format("\"{0}\"", h.Name));

			_columns.AddRange(columnNames);
		}
	}
}
