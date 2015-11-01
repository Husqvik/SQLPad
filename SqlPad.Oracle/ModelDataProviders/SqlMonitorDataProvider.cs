using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class SqlMonitorDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly SqlMonitorBuilder _sqlMonitorBuilder = new SqlMonitorBuilder();

		private readonly int _sessionId;
		private readonly int _executionId;
		private readonly string _sqlId;

		public SqlMonitorDataProvider(int sessionId, int executionId, string sqlId)
			: base(null)
		{
			_sessionId = sessionId;
			_executionId = executionId;
			_sqlId = sqlId;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectSqlMonitorCommandText;
			command.AddSimpleParameter("SQL_EXEC_ID", _executionId);
			command.AddSimpleParameter("SID", _sessionId);
			command.AddSimpleParameter("SQL_ID", _sqlId);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			await _sqlMonitorBuilder.Build(reader, cancellationToken);
		}
	}

	public class SqlMonitorPlanItemCollection : ExecutionPlanItemCollectionBase<SqlMonitorPlanItem> { }

	[DebuggerDisplay("SqlMonitorPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class SqlMonitorPlanItem : ExecutionPlanItem
	{
		
	}

	internal class SqlMonitorBuilder : ExecutionPlanBuilderBase<SqlMonitorPlanItemCollection, SqlMonitorPlanItem>
	{
		/*private static readonly TextInfo TextInfo = CultureInfo.CurrentUICulture.TextInfo;

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
			item.EstimatedOptimalSizeBytes = OracleReaderValueConvert.ToInt64(reader["ESTIMATED_OPTIMAL_SIZE"]) * 1024;
			item.EstimatedOnePassSizeBytes = OracleReaderValueConvert.ToInt64(reader["ESTIMATED_ONEPASS_SIZE"]) * 1024;
			item.LastMemoryUsedBytes = OracleReaderValueConvert.ToInt64(reader["LAST_MEMORY_USED"]) * 1024;
			item.LastExecutionMethod = TextInfo.ToTitleCase(OracleReaderValueConvert.ToString(reader["LAST_EXECUTION"]));
			item.LastParallelDegree = OracleReaderValueConvert.ToInt32(reader["LAST_DEGREE"]);
			item.TotalWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["TOTAL_EXECUTIONS"]);
			item.OptimalWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["OPTIMAL_EXECUTIONS"]);
			item.OnePassWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["ONEPASS_EXECUTIONS"]);
			item.MultiPassWorkAreaExecutions = OracleReaderValueConvert.ToInt32(reader["MULTIPASSES_EXECUTIONS"]);
			var activeTime = OracleReaderValueConvert.ToInt64(reader["ACTIVE_TIME"]);
			item.ActiveWorkAreaTime = activeTime.HasValue ? TimeSpan.FromMilliseconds(activeTime.Value * 10) : (TimeSpan?)null;
			item.MaxTemporarySizeBytes = OracleReaderValueConvert.ToInt64(reader["MAX_TEMPSEG_SIZE"]);
			item.LastTemporarySizeBytes = OracleReaderValueConvert.ToInt64(reader["LAST_TEMPSEG_SIZE"]);
		}*/
	}
}