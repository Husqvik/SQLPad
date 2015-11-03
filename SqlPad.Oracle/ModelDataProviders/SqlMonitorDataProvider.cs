using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class SqlMonitorDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly SqlMonitorBuilder _sqlMonitorBuilder;

		private readonly string _sqlId;
		private readonly int _childNumber;

		public SqlMonitorPlanItemCollection ItemCollection { get; private set; }

		public SqlMonitorDataProvider(int sessionId, int executionId, string sqlId, int childNumber)
			: base(null)
		{
			_sqlId = sqlId;
			_childNumber = childNumber;
			_sqlMonitorBuilder = new SqlMonitorBuilder(sessionId, executionId);
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectExecutionPlanCommandText;
			command.AddSimpleParameter("SQL_ID", _sqlId);
			command.AddSimpleParameter("CHILD_NUMBER", _childNumber);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			ItemCollection = await _sqlMonitorBuilder.Build(reader, cancellationToken);
		}
	}

	public class SqlMonitorPlanItemCollection : ExecutionPlanItemCollectionBase<SqlMonitorPlanItem>
	{
		public int SessionId { get; set; }

		public int ExecutionId { get; set; }

		public ObservableCollection<ActiveSessionHistoryItem> ActiveSessionHistoryItems { get; } = new ObservableCollection<ActiveSessionHistoryItem>();
	}

	public class ActiveSessionHistoryItem
	{
		public DateTime SampleTime { get; set; }

		public int SessionId { get; set; }

		public int SessionSerial { get; set; }

		public SqlMonitorPlanItem ActiveLine { get; set; }

		public string WaitClass { get; set; }

		public TimeSpan WaitTime { get; set; }

		public TimeSpan TimeWaited { get; set; }

		public TimeSpan? DeltaTime { get; set; }

		public TimeSpan? DeltaCpuTime { get; set; }

		public TimeSpan? DeltaDbTime { get; set; }

		public TimeSpan TimeSincePrevious { get; set; }

		public int DeltaReadIoRequests { get; set; }

		public int DeltaWriteIoRequests { get; set; }

		public long DeltaReadIoBytes { get; set; }

		public long DeltaWriteIoBytes { get; set; }

		public long DeltaInterconnectIoBytes { get; set; }

		public long DeltaReadMemoryBytes { get; set; }

		public long PgaAllocated { get; set; }

		public long TempSpaceAllocated { get; set; }
	}

	[DebuggerDisplay("SqlMonitorPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class SqlMonitorPlanItem : ExecutionPlanItem
	{
		/*public ulong Starts { get; set; }

		public ulong OutputRows { get; set; }

		public ulong IoInterconnectBytes { get; set; }

		public ulong PhysicalReadRequests { get; set; }

		public ulong PhysicalReadBytes { get; set; }

		public ulong PhysicalWriteRequests { get; set; }

		public ulong PhysicalWriteBytes { get; set; }

		public ulong WorkAreaMemoryBytes { get; set; }

		public ulong WorkAreaMemoryMaxBytes { get; set; }

		public ulong WorkAreaTempBytes { get; set; }

		public ulong WorkAreaTempMaxBytes { get; set; }*/
	}

	internal class SqlMonitorBuilder : ExecutionPlanBuilderBase<SqlMonitorPlanItemCollection, SqlMonitorPlanItem>
	{
		private readonly int _sessionId;
		private readonly int _executionId;

		public SqlMonitorBuilder(int sessionId, int executionId)
		{
			_sessionId = sessionId;
			_executionId = executionId;
		}

		protected override SqlMonitorPlanItemCollection InitializePlanItemCollection()
		{
			return
				new SqlMonitorPlanItemCollection
				{
					SessionId = _sessionId,
					ExecutionId = _executionId
				};
		}
	}

	internal class SqlMonitorActiveSessionHistoryDataProvider : ModelDataProvider<ModelBase>
	{
		private readonly SqlMonitorPlanItemCollection _sqlMonitorPlanItemCollection;

		public SqlMonitorActiveSessionHistoryDataProvider(SqlMonitorPlanItemCollection sqlMonitorPlanItemCollection)
			: base(null)
		{
			_sqlMonitorPlanItemCollection = sqlMonitorPlanItemCollection;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			var lastSampleTime = _sqlMonitorPlanItemCollection.ActiveSessionHistoryItems.LastOrDefault()?.SampleTime ?? DateTime.MinValue;
			command.CommandText = OracleDatabaseCommands.SelectActiveSessionHistoryCommandText;
			command.AddSimpleParameter("SID", _sqlMonitorPlanItemCollection.SessionId);
			command.AddSimpleParameter("SQL_EXEC_ID", _sqlMonitorPlanItemCollection.ExecutionId);
			command.AddSimpleParameter("SAMPLE_TIME", lastSampleTime);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var planLineId = Convert.ToInt32(reader["SQL_PLAN_LINE_ID"]);

				var historyItem =
					new ActiveSessionHistoryItem
					{
						SampleTime = (DateTime)reader["SAMPLE_TIME"],
						SessionId = Convert.ToInt32(reader["SESSION_ID"]),
						SessionSerial = Convert.ToInt32(reader["SESSION_SERIAL#"]),
						WaitClass = OracleReaderValueConvert.ToString(reader["WAIT_CLASS"]),
						WaitTime = TimeSpan.FromTicks(Convert.ToInt64(reader["WAIT_TIME"]) * 10),
						TimeWaited = TimeSpan.FromTicks(Convert.ToInt64(reader["TIME_WAITED"]) * 10),
						DeltaTime = OracleReaderValueConvert.ToTimeSpanFromMicroseconds(reader["TM_DELTA_TIME"]),
						DeltaCpuTime = OracleReaderValueConvert.ToTimeSpanFromMicroseconds(reader["TM_DELTA_CPU_TIME"]),
						DeltaDbTime = OracleReaderValueConvert.ToTimeSpanFromMicroseconds(reader["TM_DELTA_DB_TIME"]),
						TimeSincePrevious = TimeSpan.FromTicks(Convert.ToInt64(reader["DELTA_TIME"]) * 10),
						DeltaReadIoRequests = Convert.ToInt32(reader["DELTA_READ_IO_REQUESTS"]),
						DeltaWriteIoRequests = Convert.ToInt32(reader["DELTA_WRITE_IO_REQUESTS"]),
						DeltaReadIoBytes = Convert.ToInt64(reader["DELTA_READ_IO_BYTES"]),
						DeltaWriteIoBytes = Convert.ToInt64(reader["DELTA_WRITE_IO_BYTES"]),
						DeltaInterconnectIoBytes = Convert.ToInt64(reader["DELTA_INTERCONNECT_IO_BYTES"]),
						DeltaReadMemoryBytes = Convert.ToInt64(reader["DELTA_READ_MEM_BYTES"]),
						PgaAllocated = Convert.ToInt64(reader["PGA_ALLOCATED"]),
						TempSpaceAllocated = Convert.ToInt64(reader["TEMP_SPACE_ALLOCATED"]),
					};

				_sqlMonitorPlanItemCollection.ActiveSessionHistoryItems.Add(historyItem);
				historyItem.ActiveLine = _sqlMonitorPlanItemCollection.AllItems[planLineId];
			}
		}
	}
}
