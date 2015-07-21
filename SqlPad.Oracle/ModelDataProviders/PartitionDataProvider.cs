using System;
using System.Data;
using System.Globalization;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class PartitionDataProvider
	{
		private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;
		private const int HighValueMaxLength = 255;

		public IModelDataProvider PartitionDetailDataProvider { get; private set; }
		
		public IModelDataProvider SubPartitionDetailDataProvider { get; private set; }

		public PartitionDataProvider(TableDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, Version oracleVersion)
		{
			var hasInMemorySupport = InMemoryHelper.HasInMemorySupport(oracleVersion);
			PartitionDetailDataProvider = new PartitionDetailDataProviderInternal(dataModel, objectIdentifier, hasInMemorySupport);
			SubPartitionDetailDataProvider = new SubPartitionDetailDataProviderInternal(dataModel, objectIdentifier, hasInMemorySupport);
		}

		public PartitionDataProvider(PartitionDetailsModel dataModel, Version oracleVersion)
		{
			PartitionDetailDataProvider = new PartitionDetailDataProviderInternal(dataModel);
			SubPartitionDetailDataProvider = new SubPartitionDetailDataProviderInternal(dataModel, InMemoryHelper.HasInMemorySupport(oracleVersion));
		}

		public PartitionDataProvider(SubPartitionDetailsModel dataModel, Version oracleVersion)
		{
			SubPartitionDetailDataProvider = new SubPartitionDetailDataProviderInternal(dataModel, InMemoryHelper.HasInMemorySupport(oracleVersion));
		}

		private static void InitializeCommand(OracleCommand command, OracleObjectIdentifier partitionOwner)
		{
			command.AddSimpleParameter("TABLE_OWNER", partitionOwner.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", partitionOwner.Name.Trim('"'));
			command.InitialLONGFetchSize = HighValueMaxLength + 1;
		}
		
		private static void MapPartitionSegmentData(IDataRecord reader, PartitionDetailsModelBase model)
		{
			var highValue = OracleReaderValueConvert.ToString(reader["HIGH_VALUE"]);
			model.HighValue = highValue.Length > HighValueMaxLength ? $"{highValue.Substring(0, HighValueMaxLength)}{OracleLargeTextValue.Ellipsis}" : highValue;
			model.TablespaceName = OracleReaderValueConvert.ToString(reader["TABLESPACE_NAME"]);
			model.Logging = (string)reader["LOGGING"] == "YES";
			model.Compression = TextInfo.ToTitleCase(((string)reader["COMPRESSION"]).ToLowerInvariant());
			model.RowCount = OracleReaderValueConvert.ToInt64(reader["NUM_ROWS"]);
			model.SampleRows = OracleReaderValueConvert.ToInt64(reader["SAMPLE_SIZE"]);
			model.LastAnalyzed = OracleReaderValueConvert.ToDateTime(reader["LAST_ANALYZED"]);
			model.BlockCount = OracleReaderValueConvert.ToInt32(reader["BLOCKS"]);
			model.AverageRowSize = OracleReaderValueConvert.ToInt32(reader["AVG_ROW_LEN"]);
			model.InMemoryCompression = OracleReaderValueConvert.ToString(reader["INMEMORY_COMPRESSION"]);
		}

		private static string BuildCommandText(string commandTextBase, bool hasInMemorySupport)
		{
			var inMemoryColumn = hasInMemorySupport ? "NVL(INITCAP(INMEMORY_COMPRESSION), 'No in-memory')" : "'Not supported'";
			return String.Format(commandTextBase, inMemoryColumn);
		}

		private class PartitionDetailDataProviderInternal : ModelDataProvider<TableDetailsModel>
		{
			private readonly OracleObjectIdentifier _partitionOwner;
			private readonly PartitionDetailsModel _partitionDataModel;
			private readonly bool _includeInMemorySettings;

			public PartitionDetailDataProviderInternal(TableDetailsModel dataModel, OracleObjectIdentifier partitionOwner, bool includeInMemorySettings)
				: base(dataModel)
			{
				_partitionOwner = partitionOwner;
				_includeInMemorySettings = includeInMemorySettings;
			}

			public PartitionDetailDataProviderInternal(PartitionDetailsModel dataModel)
				: base(null)
			{
				_partitionDataModel = dataModel;
				_partitionOwner = dataModel.Owner;
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = BuildCommandText(OracleDatabaseCommands.SelectTablePartitionDetailsCommandTextBase, _includeInMemorySettings);
				PartitionDataProvider.InitializeCommand(command, _partitionOwner);

				command.AddSimpleParameter("PARTITION_NAME", _partitionDataModel?.Name);
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				while (reader.Read())
				{
					var partitionDetails = _partitionDataModel ??
					                       new PartitionDetailsModel
					                       {
						                       Name = (string)reader["PARTITION_NAME"]
					                       };

					MapPartitionSegmentData(reader, partitionDetails);

					if (_partitionDataModel == null)
					{
						DataModel.AddPartition(partitionDetails);
					}
				}
			}
		}

		private class SubPartitionDetailDataProviderInternal : ModelDataProvider<TableDetailsModel>
		{
			private readonly OracleObjectIdentifier _subPartitionOwner;
			private readonly PartitionDetailsModel _partitionDataModel;
			private readonly SubPartitionDetailsModel _subPartitionDataModel;
			private readonly bool _includeInMemorySettings;

			public SubPartitionDetailDataProviderInternal(TableDetailsModel dataModel, OracleObjectIdentifier partitionOwner, bool includeInMemorySettings)
				: base(dataModel)
			{
				_subPartitionOwner = partitionOwner;
				_includeInMemorySettings = includeInMemorySettings;
			}

			public SubPartitionDetailDataProviderInternal(PartitionDetailsModel dataModel, bool includeInMemorySettings)
				: base(null)
			{
				_subPartitionOwner = dataModel.Owner;
				_partitionDataModel = dataModel;
				_includeInMemorySettings = includeInMemorySettings;
			}

			public SubPartitionDetailDataProviderInternal(SubPartitionDetailsModel dataModel, bool includeInMemorySettings)
				: base(null)
			{
				_subPartitionOwner = dataModel.Owner;
				_subPartitionDataModel = dataModel;
				_includeInMemorySettings = includeInMemorySettings;
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = BuildCommandText(OracleDatabaseCommands.SelectTableSubPartitionsDetailsCommandTextBase, _includeInMemorySettings);
				PartitionDataProvider.InitializeCommand(command, _subPartitionOwner);

				command.AddSimpleParameter("PARTITION_NAME", _partitionDataModel?.Name);
				command.AddSimpleParameter("SUBPARTITION_NAME", _subPartitionDataModel?.Name);
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				while (reader.Read())
				{
					var subPartitionDetails = _subPartitionDataModel ??
					                          new SubPartitionDetailsModel
					                          {
						                          Name = (string)reader["SUBPARTITION_NAME"]
					                          };

					MapPartitionSegmentData(reader, subPartitionDetails);

					if (_subPartitionDataModel == null)
					{
						var partition = _partitionDataModel ?? DataModel.GetPartitions((string)reader["PARTITION_NAME"]);
						partition.AddSubPartition(subPartitionDetails);
					}
				}
			}
		}
	}
}
