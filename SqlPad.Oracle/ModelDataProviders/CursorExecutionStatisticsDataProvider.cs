using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class CursorExecutionStatisticsDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly CursorExecutionStatisticsBuilder _executionStatisticsBuilder = new CursorExecutionStatisticsBuilder();
		private readonly string _sqlId;
		private readonly int? _childNumber;

		public ExecutionStatisticsPlanItem RootItem { get; private set; }

		public CursorExecutionStatisticsDataProvider(string sqlId, int childNumber)
			: base(null)
		{
			_sqlId = sqlId;
			_childNumber = childNumber;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.GetCursorExecutionStatistics;
			command.AddSimpleParameter("SQL_ID", _sqlId);
			command.AddSimpleParameter("CHILD_NUMBER", _childNumber);
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			RootItem = _executionStatisticsBuilder.Build(reader);
		}
	}

	internal class CursorExecutionStatisticsBuilder : ExecutionPlanBuilderBase<ExecutionStatisticsPlanItem>
	{
		private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

		protected override void FillData(IDataRecord reader, ExecutionStatisticsPlanItem item)
		{
			item.Timestamp = Convert.ToDateTime(reader["TIMESTAMP"]);
			item.Executions = Convert.ToInt32(reader["EXECUTIONS"]);
			item.LastStarts = OracleReaderValueConvert.ToInt32(reader["LAST_STARTS"]);
			item.TotalStarts = OracleReaderValueConvert.ToInt32(reader["STARTS"]);
			item.LastOutputRows = OracleReaderValueConvert.ToInt64(reader["LAST_OUTPUT_ROWS"]);
			item.TotalOutputRows = OracleReaderValueConvert.ToInt64(reader["OUTPUT_ROWS"]);
			item.LastConsistentReadBufferGets = OracleReaderValueConvert.ToInt64(reader["LAST_CR_BUFFER_GETS"]);
			item.TotalConsistentReadBufferGets = OracleReaderValueConvert.ToInt64(reader["CR_BUFFER_GETS"]);
			item.LastCurrentReadBufferGets = OracleReaderValueConvert.ToInt64(reader["LAST_CU_BUFFER_GETS"]);
			item.TotalCurrentReadBufferGets = OracleReaderValueConvert.ToInt64(reader["CU_BUFFER_GETS"]);
			item.LastDiskReads = OracleReaderValueConvert.ToInt64(reader["LAST_DISK_READS"]);
			item.TotalDiskReads = OracleReaderValueConvert.ToInt64(reader["DISK_READS"]);
			item.LastDiskWrites = OracleReaderValueConvert.ToInt64(reader["LAST_DISK_WRITES"]);
			item.TotalDiskWrites = OracleReaderValueConvert.ToInt64(reader["DISK_WRITES"]);
			var lastElapsedMicroseconds = OracleReaderValueConvert.ToInt64(reader["LAST_ELAPSED_TIME"]);
			item.LastElapsedTime = lastElapsedMicroseconds.HasValue ? TimeSpan.FromMilliseconds(lastElapsedMicroseconds.Value / 1000d) : (TimeSpan?)null;
			var totalElapsedMicroseconds = OracleReaderValueConvert.ToInt64(reader["ELAPSED_TIME"]);
			item.TotalElapsedTime = totalElapsedMicroseconds.HasValue ? TimeSpan.FromMilliseconds(totalElapsedMicroseconds.Value / 1000d) : (TimeSpan?)null;
			item.WorkAreaSizingPolicy = TextInfo.ToTitleCase(OracleReaderValueConvert.ToString(reader["POLICY"]));
			item.EstimatedOptimalSizeBytes = OracleReaderValueConvert.ToInt64(reader["ESTIMATED_OPTIMAL_SIZE"]);
			item.EstimatedOnePassSizeBytes = OracleReaderValueConvert.ToInt64(reader["ESTIMATED_ONEPASS_SIZE"]);
			item.LastMemoryUsedBytes = OracleReaderValueConvert.ToInt64(reader["LAST_MEMORY_USED"]);
			item.LastExecutionMethod = TextInfo.ToTitleCase(OracleReaderValueConvert.ToString(reader["LAST_EXECUTION"]));
			item.LastParallelDegree = OracleReaderValueConvert.ToInt32(reader["LAST_DEGREE"]);
			item.TotalWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["TOTAL_EXECUTIONS"]);
			item.OptimalWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["OPTIMAL_EXECUTIONS"]);
			item.OnePassWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["ONEPASS_EXECUTIONS"]);
			item.MultiPassWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["MULTIPASSES_EXECUTIONS"]);
			var activeTime = OracleReaderValueConvert.ToInt64(reader["ACTIVE_TIME"]);
			item.ActiveTime = activeTime.HasValue ? TimeSpan.FromMilliseconds(activeTime.Value * 10) : (TimeSpan?)null;
			item.MaxTemporarySizeBytes = OracleReaderValueConvert.ToInt64(reader["MAX_TEMPSEG_SIZE"]);
			item.LastTemporarySizeBytes = OracleReaderValueConvert.ToInt64(reader["LAST_TEMPSEG_SIZE"]);
		}
	}

	[DebuggerDisplay("ExecutionStatisticsPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class ExecutionStatisticsPlanItem : ExecutionPlanItem
	{
		public DateTime Timestamp { get; set; }

		public int Executions { get; set; }

		public int? LastStarts { get; set; }

		public int? TotalStarts { get; set; }

		public long? LastOutputRows { get; set; }

		public long? TotalOutputRows { get; set; }

		public long? LastConsistentReadBufferGets { get; set; }

		public long? TotalConsistentReadBufferGets { get; set; }

		public long? LastCurrentReadBufferGets { get; set; }

		public long? TotalCurrentReadBufferGets { get; set; }

		public long? LastDiskReads { get; set; }

		public long? TotalDiskReads { get; set; }

		public long? LastDiskWrites { get; set; }

		public long? TotalDiskWrites { get; set; }

		public TimeSpan? LastElapsedTime { get; set; }

		public TimeSpan? TotalElapsedTime { get; set; }

		public string WorkAreaSizingPolicy { get; set; }

		public long? EstimatedOptimalSizeBytes { get; set; }

		public long? EstimatedOnePassSizeBytes { get; set; }

		public long? LastMemoryUsedBytes { get; set; }

		public string LastExecutionMethod { get; set; }

		public int? LastParallelDegree { get; set; }

		public int? TotalWorkAreaExecutions { get; set; }

		public int? OptimalWorkAreaExecutions { get; set; }

		public int? OnePassWorkAreaExecutions { get; set; }

		public int? MultiPassWorkAreaExecutions { get; set; }

		public TimeSpan? ActiveTime { get; set; }

		public long? MaxTemporarySizeBytes { get; set; }

		public long? LastTemporarySizeBytes { get; set; }
	}
}