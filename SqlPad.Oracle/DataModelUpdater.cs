using System;
using System.Collections.Generic;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle
{
	internal interface IDataModelUpdater
	{
		string CommandText { get; }
		
		bool CanContinue { get; }
		
		void MapData(OracleDataReader reader);
	}

	internal abstract class DataModelUpdater<TModel> : IDataModelUpdater where TModel: ModelBase
	{
		protected TModel DataModel { get; private set; }

		protected DataModelUpdater(TModel dataModel)
		{
			DataModel = dataModel;
		}

		public abstract string CommandText { get; }

		public abstract void MapData(OracleDataReader reader);

		public abstract bool CanContinue { get; }
	}

	internal class ColumnDetailsModelUpdater : DataModelUpdater<ColumnDetailsModel>
	{
		public ColumnDetailsModelUpdater(ColumnDetailsModel dataModel) : base(dataModel)
		{
		}

		public override string CommandText
		{
			get { return DatabaseCommands.GetColumnStatisticsCommand; }
		}

		public override void MapData(OracleDataReader reader)
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

		public override bool CanContinue
		{
			get { return DataModel.HistogramType != null && DataModel.HistogramType != "None"; }
		}
	}

	internal class ColumnDetailsHistogramUpdater : DataModelUpdater<ColumnDetailsModel>
	{
		public ColumnDetailsHistogramUpdater(ColumnDetailsModel dataModel)
			: base(dataModel)
		{
		}

		public override string CommandText
		{
			get { return DatabaseCommands.GetColumnHistogramCommand; }
		}

		public override void MapData(OracleDataReader reader)
		{
			var histogramValues = new List<double>();

			while (reader.Read())
			{
				histogramValues.Add(Convert.ToInt32(reader["ENDPOINT_NUMBER"]));
			}

			DataModel.HistogramValues = histogramValues;
		}

		public override bool CanContinue
		{
			get { return false; }
		}
	}

	internal class TableDetailsModelUpdater : DataModelUpdater<TableDetailsModel>
	{
		public TableDetailsModelUpdater(TableDetailsModel dataModel)
			: base(dataModel)
		{
		}

		public override string CommandText
		{
			get { return DatabaseCommands.GetTableDetailsCommand; }
		}

		public override void MapData(OracleDataReader reader)
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
			DataModel.ClusterName = OracleReaderValueConvert.ToString(reader["CLUSTER_NAME"]);
			DataModel.IsTemporary = (string)reader["TEMPORARY"] == "Y";
			DataModel.IsPartitioned = (string)reader["PARTITIONED"] == "YES";
		}

		public override bool CanContinue
		{
			get { return true; }
		}
	}

	internal class TableSpaceAllocationModelUpdater : DataModelUpdater<TableDetailsModel>
	{
		public TableSpaceAllocationModelUpdater(TableDetailsModel dataModel)
			: base(dataModel)
		{
		}

		public override string CommandText
		{
			get { return DatabaseCommands.GetTableAllocatedBytesCommand; }
		}

		public override void MapData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

			DataModel.AllocatedBytes = OracleReaderValueConvert.ToInt64(reader["ALLOCATED_BYTES"]);
		}

		public override bool CanContinue
		{
			get { return false; }
		}
	}
}
