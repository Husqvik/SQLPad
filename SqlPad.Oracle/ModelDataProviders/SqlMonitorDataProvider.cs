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

		public SqlMonitorDataProvider(int sessionId, DateTime executionStart, int executionId, string sqlId, int childNumber)
			: base(null)
		{
			_sqlId = sqlId;
			_childNumber = childNumber;
			_sqlMonitorBuilder = new SqlMonitorBuilder(sessionId, _sqlId, executionStart, executionId);
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
		public SqlMonitorPlanItemCollection(int sessionId, string sqlId, DateTime executionStart, int executionId)
		{
			SessionId = sessionId;
			SqlId = sqlId;
			ExecutionStart = executionStart;
			ExecutionId = executionId;
		}

		public int SessionId { get; }

		public string SqlId { get; }

		public DateTime ExecutionStart { get; set; }

		public int ExecutionId { get; }

		public ObservableCollection<ActiveSessionHistoryItem> ActiveSessionHistoryItems { get; } = new ObservableCollection<ActiveSessionHistoryItem>();
	}

	[DebuggerDisplay("ActiveSessionHistoryItem (SessionId={SessionId}; SessionSerial={SessionSerial}; SampleTime={SampleTime})")]
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
		private long _starts;
		private long _outputRows;
		private long _ioInterconnectBytes;
		private long _physicalReadRequests;
		private long _physicalReadBytes;
		private long _physicalWriteRequests;
		private long _physicalWriteBytes;
		private long? _workAreaMemoryBytes;
		private long? _workAreaMemoryMaxBytes;
		private long? _workAreaTempBytes;
		private long? _workAreaTempMaxBytes;

		public long Starts
		{
			get { return _starts; }
			set { UpdateValueAndRaisePropertyChanged(ref _starts, value); }
		}

		public long OutputRows
		{
			get { return _outputRows; }
			set { UpdateValueAndRaisePropertyChanged(ref _outputRows, value); }
		}

		public long IoInterconnectBytes
		{
			get { return _ioInterconnectBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _ioInterconnectBytes, value); }
		}

		public long PhysicalReadRequests
		{
			get { return _physicalReadRequests; }
			set { UpdateValueAndRaisePropertyChanged(ref _physicalReadRequests, value); }
		}

		public long PhysicalReadBytes
		{
			get { return _physicalReadBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _physicalReadBytes, value); }
		}

		public long PhysicalWriteRequests
		{
			get { return _physicalWriteRequests; }
			set { UpdateValueAndRaisePropertyChanged(ref _physicalWriteRequests, value); }
		}

		public long PhysicalWriteBytes
		{
			get { return _physicalWriteBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _physicalWriteBytes, value); }
		}

		public long? WorkAreaMemoryBytes
		{
			get { return _workAreaMemoryBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _workAreaMemoryBytes, value); }
		}

		public long? WorkAreaMemoryMaxBytes
		{
			get { return _workAreaMemoryMaxBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _workAreaMemoryMaxBytes, value); }
		}

		public long? WorkAreaTempBytes
		{
			get { return _workAreaTempBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _workAreaTempBytes, value); }
		}

		public long? WorkAreaTempMaxBytes
		{
			get { return _workAreaTempMaxBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _workAreaTempMaxBytes, value); }
		}
	}

	internal class SqlMonitorBuilder : ExecutionPlanBuilderBase<SqlMonitorPlanItemCollection, SqlMonitorPlanItem>
	{
		private readonly int _sessionId;
		private readonly string _sqlId;
		private readonly DateTime _executionStart;
		private readonly int _executionId;

		public SqlMonitorBuilder(int sessionId, string sqlId, DateTime executionStart, int executionId)
		{
			_sessionId = sessionId;
			_sqlId = sqlId;
			_executionStart = executionStart;
			_executionId = executionId;
		}

		protected override SqlMonitorPlanItemCollection InitializePlanItemCollection()
		{
			return new SqlMonitorPlanItemCollection(_sessionId, _sqlId, _executionStart, _executionId);
		}
	}

	internal class SqlMonitorActiveSessionHistoryDataProvider : ModelDataProvider<SqlMonitorPlanItemCollection>
	{
		public SqlMonitorActiveSessionHistoryDataProvider(SqlMonitorPlanItemCollection sqlMonitorPlanItemCollection)
			: base(sqlMonitorPlanItemCollection)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			var lastSampleTime = DataModel.ActiveSessionHistoryItems.LastOrDefault()?.SampleTime ?? DateTime.MinValue;
			command.CommandText = OracleDatabaseCommands.SelectActiveSessionHistoryCommandText;
			command.AddSimpleParameter("SID", DataModel.SessionId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SAMPLE_TIME", lastSampleTime);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
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

				DataModel.ActiveSessionHistoryItems.Add(historyItem);
				historyItem.ActiveLine = DataModel.AllItems[planLineId];
			}
		}
	}

	internal class SqlMonitorPlanMonitorDataProvider : ModelDataProvider<SqlMonitorPlanItemCollection>
	{
		public SqlMonitorPlanMonitorDataProvider(SqlMonitorPlanItemCollection sqlMonitorPlanItemCollection)
			: base(sqlMonitorPlanItemCollection)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectSqlPlanMonitorCommandText;
			command.AddSimpleParameter("SID", DataModel.SessionId);
			command.AddSimpleParameter("SQL_ID", DataModel.SqlId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var planLineId = Convert.ToInt32(reader["PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				planItem.Starts = Convert.ToInt64(reader["Starts"]);
				planItem.OutputRows = Convert.ToInt64(reader["OUTPUT_ROWS"]);
				planItem.IoInterconnectBytes = Convert.ToInt64(reader["IO_INTERCONNECT_BYTES"]);
				planItem.PhysicalReadRequests = Convert.ToInt64(reader["PHYSICAL_READ_REQUESTS"]);
				planItem.PhysicalReadBytes = Convert.ToInt64(reader["PHYSICAL_READ_BYTES"]);
				planItem.PhysicalWriteRequests = Convert.ToInt64(reader["PHYSICAL_WRITE_REQUESTS"]);
				planItem.PhysicalWriteBytes = Convert.ToInt64(reader["PHYSICAL_WRITE_BYTES"]);
				planItem.WorkAreaMemoryBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MEM"]);
				planItem.WorkAreaMemoryMaxBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MAX_MEM"]);
				planItem.WorkAreaTempBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_TEMPSEG"]);
				planItem.WorkAreaTempMaxBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MAX_TEMPSEG"]);
			}
		}
	}
}
