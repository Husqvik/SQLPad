using System;
using System.Collections.Generic;
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

		public ObservableDictionary<int, List<ActiveSessionHistoryItem>> ActiveSessionHistoryItems { get; } = new ObservableDictionary<int, List<ActiveSessionHistoryItem>>();
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

	[DebuggerDisplay("SqlMonitorPlanItem (Id={Id}; Operation={Operation}; ObjectName={ObjectName}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class SqlMonitorPlanItem : ExecutionPlanItem
	{
		private SqlMonitorSessionItem _allSessionSummaryItem;

		public SqlMonitorSessionItem AllSessionSummaryItem
		{
			get { return _allSessionSummaryItem; }
			set { UpdateValueAndRaisePropertyChanged(ref _allSessionSummaryItem, value); }
		}

		public ObservableDictionary<int, SqlMonitorSessionItem> SessionItems { get; } = new ObservableDictionary<int, SqlMonitorSessionItem>();

		public ObservableDictionary<int, SqlMonitorSessionLongOperationItem> SessionLongOperationItems { get; } = new ObservableDictionary<int, SqlMonitorSessionLongOperationItem>();
	}

	[DebuggerDisplay("SqlMonitorSessionLongOperationItem (SessionId={SessionId}; Message={_message})")]
	public class SqlMonitorSessionLongOperationItem : ModelBase
	{
		private string _operationName;
		private string _target;
		private string _targetDescription;
		private long _soFar;
		private long _totalWork;
		private string _units;
		private DateTime _startTime;
		private DateTime _lastUpdateTime;
		private TimeSpan? _timeRemaining;
		private TimeSpan _elapsed;
		private long? _context;
		private string _message;

		public int SessionId { get; set; }

		public SqlMonitorPlanItem PlanItem { get; set; }

		public string OperationName
		{
			get { return _operationName; }
			set { UpdateValueAndRaisePropertyChanged(ref _operationName, value); }
		}

		public string Target
		{
			get { return _target; }
			set { UpdateValueAndRaisePropertyChanged(ref _target, value); }
		}

		public string TargetDescription
		{
			get { return _targetDescription; }
			set { UpdateValueAndRaisePropertyChanged(ref _targetDescription, value); }
		}

		public long SoFar
		{
			get { return _soFar; }
			set { UpdateValueAndRaisePropertyChanged(ref _soFar, value); }
		}

		public long TotalWork
		{
			get { return _totalWork; }
			set { UpdateValueAndRaisePropertyChanged(ref _totalWork, value); }
		}

		public string Units
		{
			get { return _units; }
			set { UpdateValueAndRaisePropertyChanged(ref _units, value); }
		}

		public DateTime StartTime
		{
			get { return _startTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _startTime, value); }
		}

		public DateTime LastUpdateTime
		{
			get { return _lastUpdateTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _lastUpdateTime, value); }
		}

		public TimeSpan? TimeRemaining
		{
			get { return _timeRemaining; }
			set { UpdateValueAndRaisePropertyChanged(ref _timeRemaining, value); }
		}

		public TimeSpan Elapsed
		{
			get { return _elapsed; }
			set { UpdateValueAndRaisePropertyChanged(ref _elapsed, value); }
		}

		public long? Context
		{
			get { return _context; }
			set { UpdateValueAndRaisePropertyChanged(ref _context, value); }
		}

		public string Message
		{
			get { return _message; }
			set { UpdateValueAndRaisePropertyChanged(ref _message, value); }
		}
	}

	public class SqlMonitorSessionItem : ModelBase
	{
		private int _sessionId;
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

		public int SessionId
		{
			get { return _sessionId; }
			set { UpdateValueAndRaisePropertyChanged(ref _sessionId, value); }
		}

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

		public void Reset()
		{
			if (_sessionId != 0)
			{
				throw new InvalidOperationException("Only summary item can be reseted. ");
			}

			Starts = 0;
			OutputRows = 0;
			IoInterconnectBytes = 0;
			PhysicalReadRequests = 0;
			PhysicalReadBytes = 0;
			PhysicalWriteRequests = 0;
			PhysicalWriteBytes = 0;
			WorkAreaMemoryBytes = null;
			WorkAreaMemoryMaxBytes = null;
			WorkAreaTempBytes = null;
			WorkAreaTempMaxBytes = null;
		}

		public void MergeSessionItem(SqlMonitorSessionItem sessionItem)
		{
			Starts += sessionItem.Starts;
			OutputRows += sessionItem.OutputRows;
			IoInterconnectBytes += sessionItem.IoInterconnectBytes;
			PhysicalReadRequests += sessionItem.PhysicalReadRequests;
			PhysicalReadBytes += sessionItem.PhysicalReadBytes;
			PhysicalWriteRequests += sessionItem.PhysicalWriteRequests;
			PhysicalWriteBytes += sessionItem.PhysicalWriteBytes;

			MergeNullableValue(ref _workAreaMemoryBytes, sessionItem._workAreaMemoryBytes);
			MergeNullableValue(ref _workAreaMemoryMaxBytes, sessionItem._workAreaMemoryMaxBytes);
			MergeNullableValue(ref _workAreaTempBytes, sessionItem._workAreaTempBytes);
			MergeNullableValue(ref _workAreaTempMaxBytes, sessionItem._workAreaTempMaxBytes);
		}

		private static void MergeNullableValue(ref long? summaryValue, long? sessionValue)
		{
			if (sessionValue == null)
			{
				return;
			}

			if (summaryValue == null)
			{
				summaryValue = 0;
			}

			summaryValue += sessionValue.Value;
		}

		public void NotifyPropertyChanged()
		{
			RaisePropertyChanged(
				nameof(Starts),
				nameof(OutputRows),
				nameof(IoInterconnectBytes),
				nameof(PhysicalReadRequests),
				nameof(PhysicalReadBytes),
				nameof(PhysicalWriteRequests),
				nameof(PhysicalWriteBytes),
				nameof(WorkAreaMemoryBytes),
				nameof(WorkAreaMemoryMaxBytes),
				nameof(WorkAreaTempBytes),
				nameof(WorkAreaTempMaxBytes));
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
			List<ActiveSessionHistoryItem> activeSessionHistoryItems;
			var lastSampleTime = DataModel.ActiveSessionHistoryItems.TryGetValue(DataModel.SessionId, out activeSessionHistoryItems)
				? activeSessionHistoryItems.Last()?.SampleTime
				: DateTime.MinValue;

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
				var sessionId = Convert.ToInt32(reader["SESSION_ID"]);
				List<ActiveSessionHistoryItem> activeSessionHistoryItems;
				if (!DataModel.ActiveSessionHistoryItems.TryGetValue(sessionId, out activeSessionHistoryItems))
				{
					DataModel.ActiveSessionHistoryItems[sessionId] = activeSessionHistoryItems = new List<ActiveSessionHistoryItem>();
				}

				var historyItem =
					new ActiveSessionHistoryItem
					{
						SampleTime = (DateTime)reader["SAMPLE_TIME"],
						SessionId = sessionId,
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

				activeSessionHistoryItems.Add(historyItem);

				var planLineId = OracleReaderValueConvert.ToInt32(reader["SQL_PLAN_LINE_ID"]);

				historyItem.ActiveLine = planLineId.HasValue
					? DataModel.AllItems[planLineId.Value]
					: null;
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
			command.CommandText = OracleDatabaseCommands.SelectPlanMonitorCommandText;
			command.AddSimpleParameter("SID", DataModel.SessionId);
			command.AddSimpleParameter("SQL_ID", DataModel.SqlId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionId = Convert.ToInt32(reader["SID"]);
				var planLineId = Convert.ToInt32(reader["PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				SqlMonitorSessionItem sqlMonitorItem;
				if (!planItem.SessionItems.TryGetValue(sessionId, out sqlMonitorItem))
				{
					planItem.SessionItems[sessionId] = sqlMonitorItem =
						new SqlMonitorSessionItem
						{
							SessionId = sessionId
						};
				}

				sqlMonitorItem.Starts = Convert.ToInt64(reader["STARTS"]);
				sqlMonitorItem.OutputRows = Convert.ToInt64(reader["OUTPUT_ROWS"]);
				sqlMonitorItem.IoInterconnectBytes = Convert.ToInt64(reader["IO_INTERCONNECT_BYTES"]);
				sqlMonitorItem.PhysicalReadRequests = Convert.ToInt64(reader["PHYSICAL_READ_REQUESTS"]);
				sqlMonitorItem.PhysicalReadBytes = Convert.ToInt64(reader["PHYSICAL_READ_BYTES"]);
				sqlMonitorItem.PhysicalWriteRequests = Convert.ToInt64(reader["PHYSICAL_WRITE_REQUESTS"]);
				sqlMonitorItem.PhysicalWriteBytes = Convert.ToInt64(reader["PHYSICAL_WRITE_BYTES"]);
				sqlMonitorItem.WorkAreaMemoryBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MEM"]);
				sqlMonitorItem.WorkAreaMemoryMaxBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MAX_MEM"]);
				sqlMonitorItem.WorkAreaTempBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_TEMPSEG"]);
				sqlMonitorItem.WorkAreaTempMaxBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MAX_TEMPSEG"]);
			}

			foreach (var planItem in DataModel.AllItems.Values)
			{
				var summaryItem = planItem.AllSessionSummaryItem;
				if (summaryItem == null)
				{
					planItem.AllSessionSummaryItem = summaryItem = new SqlMonitorSessionItem();
				}

				summaryItem.Reset();

				foreach (var planSessionItem in planItem.SessionItems.Values)
				{
					summaryItem.MergeSessionItem(planSessionItem);
				}

				summaryItem.NotifyPropertyChanged();
			}
		}
	}

	internal class SessionLongOperationPlanMonitorDataProvider : ModelDataProvider<SqlMonitorPlanItemCollection>
	{
		public SessionLongOperationPlanMonitorDataProvider(SqlMonitorPlanItemCollection sqlMonitorPlanItemCollection)
			: base(sqlMonitorPlanItemCollection)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectSessionLongOperationCommandText;
			command.AddSimpleParameter("SID", DataModel.SessionId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionId = Convert.ToInt32(reader["SID"]);
				var planLineId = Convert.ToInt32(reader["SQL_PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				SqlMonitorSessionLongOperationItem longOperationItem;
				if (!planItem.SessionLongOperationItems.TryGetValue(sessionId, out longOperationItem))
				{
					planItem.SessionLongOperationItems[sessionId] = longOperationItem =
						new SqlMonitorSessionLongOperationItem
						{
							SessionId = sessionId,
							PlanItem = planItem
						};
				}

				longOperationItem.OperationName = (string)reader["OPNAME"];
				longOperationItem.Target = OracleReaderValueConvert.ToString(reader["TARGET"]);
				longOperationItem.TargetDescription = OracleReaderValueConvert.ToString(reader["TARGET_DESC"]);
				longOperationItem.SoFar = Convert.ToInt64(reader["SOFAR"]);
				longOperationItem.TotalWork = Convert.ToInt64(reader["TOTALWORK"]);
				longOperationItem.Units = OracleReaderValueConvert.ToString(reader["UNITS"]);
				longOperationItem.StartTime = (DateTime)reader["START_TIME"];
				longOperationItem.LastUpdateTime = (DateTime)reader["LAST_UPDATE_TIME"];
				var timestamp = reader["TIMESTAMP"];
				var secondsRemaining = OracleReaderValueConvert.ToInt64(reader["TIME_REMAINING"]);
				if (secondsRemaining.HasValue)
				{
					longOperationItem.TimeRemaining = TimeSpan.FromSeconds(secondsRemaining.Value);
				}

				longOperationItem.Elapsed = TimeSpan.FromSeconds(Convert.ToInt64(reader["ELAPSED_SECONDS"]));
				longOperationItem.Context = Convert.ToInt64(reader["CONTEXT"]);
				longOperationItem.Message = OracleReaderValueConvert.ToString(reader["MESSAGE"]);
			}
		}
	}
}
