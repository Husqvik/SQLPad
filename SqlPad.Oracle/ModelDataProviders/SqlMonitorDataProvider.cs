using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
		private readonly Dictionary<int, SqlMonitorSessionItem> _sessionItemMapping = new Dictionary<int, SqlMonitorSessionItem>();

		private int _totalActiveSessionHistorySamples;

		public SqlMonitorPlanItemCollection(int sessionId, string sqlId, DateTime executionStart, int executionId)
		{
			SessionId = sessionId;
			SqlId = sqlId;
			ExecutionStart = executionStart;
			ExecutionId = executionId;
			RefreshPeriod = TimeSpan.FromSeconds(1);
		}

		public int SessionId { get; }

		public string SqlId { get; }

		public DateTime ExecutionStart { get; set; }

		public int ExecutionId { get; }

		public TimeSpan RefreshPeriod { get; set; }

		public DateTime? LastSampleTime { get; private set; }

		public ObservableCollection<SqlMonitorSessionItem> SessionItems { get; } = new ObservableCollection<SqlMonitorSessionItem>();

		public SqlMonitorSessionItem GetSessionItem(int sessionId)
		{
			SqlMonitorSessionItem sessionItem;
			_sessionItemMapping.TryGetValue(sessionId, out sessionItem);
			return sessionItem;
		}

		public void MergeSessionItem(SqlMonitorSessionItem newSessionItem)
		{
			SqlMonitorSessionItem currentSessionItem;
			if (_sessionItemMapping.TryGetValue(newSessionItem.SessionId, out currentSessionItem))
			{
				currentSessionItem.ApplicationWaitTime = newSessionItem.ApplicationWaitTime;
				currentSessionItem.BufferGets = newSessionItem.BufferGets;
				currentSessionItem.ClusterWaitTime = newSessionItem.ClusterWaitTime;
				currentSessionItem.ConcurrencyWaitTime = newSessionItem.ConcurrencyWaitTime;
				currentSessionItem.CpuTime = newSessionItem.CpuTime;
				currentSessionItem.DiskReads = newSessionItem.DiskReads;
				currentSessionItem.DirectWrites = newSessionItem.DirectWrites;
				currentSessionItem.ElapsedTime = newSessionItem.ElapsedTime;
				currentSessionItem.Fetches = newSessionItem.Fetches;
				currentSessionItem.IoInterconnectBytes = newSessionItem.IoInterconnectBytes;
				currentSessionItem.JavaExecutionTime = newSessionItem.JavaExecutionTime;
				currentSessionItem.PhysicalReadBytes = newSessionItem.PhysicalReadBytes;
				currentSessionItem.PhysicalReadRequests = newSessionItem.PhysicalReadRequests;
				currentSessionItem.PhysicalWriteBytes = newSessionItem.PhysicalWriteBytes;
				currentSessionItem.PhysicalWriteRequests = newSessionItem.PhysicalWriteRequests;
				currentSessionItem.PlSqlExecutionTime = newSessionItem.PlSqlExecutionTime;
				currentSessionItem.QueingTime = newSessionItem.QueingTime;
				currentSessionItem.UserIoWaitTime = newSessionItem.UserIoWaitTime;
			}
			else
			{
				_sessionItemMapping.Add(newSessionItem.SessionId, newSessionItem);
				SessionItems.Add(newSessionItem);
			}
		}

		public void AddActiveSessionHistoryItems(IEnumerable<ActiveSessionHistoryItem> historyItems)
		{
			foreach (var historyItem in historyItems)
			{
				historyItem.PlanItem.AddActiveSessionHistoryItem(historyItem);

				if (LastSampleTime == null || historyItem.SampleTime > LastSampleTime)
				{
					LastSampleTime = historyItem.SampleTime;
				}

				_totalActiveSessionHistorySamples++;
			}

			foreach (var planLineItem in AllItems.Values)
			{
				planLineItem.IsBeingExecuted = LastSampleTime.HasValue && planLineItem.ActiveSessionHistoryItems.Values.Any(items => items.LastOrDefault()?.SampleTime >= LastSampleTime.Value.Add(-RefreshPeriod));

				if (_totalActiveSessionHistorySamples > 0)
				{
					var planItemSamples = planLineItem.ActiveSessionHistoryItems.Sum(kvp => kvp.Value.Count);
					planLineItem.ActivityRatio = planItemSamples / (decimal)_totalActiveSessionHistorySamples;
				}
			}
		}
	}

	[DebuggerDisplay("ActiveSessionHistoryItem (SessionId={SessionId}; SessionSerial={SessionSerial}; SampleTime={SampleTime})")]
	public class ActiveSessionHistoryItem
	{
		public DateTime SampleTime { get; set; }

		public int SessionId { get; set; }

		public int SessionSerial { get; set; }

		public SqlMonitorPlanItem PlanItem { get; set; }

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
		private SqlMonitorSessionPlanItem _allSessionSummaryPlanItem;
		private bool _isBeingExecuted;
		private decimal? _activityRatio;

		private readonly ObservableCollection<SessionLongOperationCollection> _sessionLongOperations = new ObservableCollection<SessionLongOperationCollection>();
		private readonly Dictionary<int, SessionLongOperationCollection> _sessionLongOperationsItemMapping = new Dictionary<int, SessionLongOperationCollection>();
		private readonly ObservableCollection<SqlMonitorSessionPlanItem> _parallelSlaveSessionItems = new ObservableCollection<SqlMonitorSessionPlanItem>();
		private readonly Dictionary<int, SqlMonitorSessionPlanItem> _parallelSlaveSessionItemMapping = new Dictionary<int, SqlMonitorSessionPlanItem>();
		private readonly Dictionary<int, ObservableCollection<ActiveSessionHistoryItem>> _activeSessionHistoryItemMapping = new Dictionary<int, ObservableCollection<ActiveSessionHistoryItem>>();

		public SqlMonitorSessionPlanItem AllSessionSummaryPlanItem
		{
			get { return _allSessionSummaryPlanItem; }
			set { UpdateValueAndRaisePropertyChanged(ref _allSessionSummaryPlanItem, value); }
		}

		public IReadOnlyCollection<SqlMonitorSessionPlanItem> ParallelSlaveSessionItems => _parallelSlaveSessionItems;

		public IReadOnlyCollection<SessionLongOperationCollection> SessionLongOperations => _sessionLongOperations;

		public IReadOnlyDictionary<int, ObservableCollection<ActiveSessionHistoryItem>> ActiveSessionHistoryItems => _activeSessionHistoryItemMapping;

		public SessionLongOperationCollection QueryCoordinatorLongOperations { get; } = new SessionLongOperationCollection();

		public bool IsBeingExecuted
		{
			get { return _isBeingExecuted; }
			set { UpdateValueAndRaisePropertyChanged(ref _isBeingExecuted, value); }
		}

		public decimal? ActivityRatio
		{
			get { return _activityRatio; }
			set { UpdateValueAndRaisePropertyChanged(ref _activityRatio, value); }
		}

		public SqlMonitorSessionPlanItem GetSessionPlanItem(int sessionId)
		{
			SqlMonitorSessionPlanItem sessionPlanItem;
			if (!_parallelSlaveSessionItemMapping.TryGetValue(sessionId, out sessionPlanItem))
			{
				_parallelSlaveSessionItemMapping.Add(sessionId, sessionPlanItem = new SqlMonitorSessionPlanItem { SessionId = sessionId });
				_parallelSlaveSessionItems.Add(sessionPlanItem);
			}

			return sessionPlanItem;
		}

		public SessionLongOperationCollection GetSessionLongOperationCollection(int sessionId)
		{
			SessionLongOperationCollection longOperationCollection;
			if (!_sessionLongOperationsItemMapping.TryGetValue(sessionId, out longOperationCollection))
			{
				_sessionLongOperationsItemMapping.Add(sessionId, longOperationCollection = new SessionLongOperationCollection { SessionId = sessionId, PlanItem = this });
				_sessionLongOperations.Add(longOperationCollection);
			}

			return longOperationCollection;
		}

		public void AddActiveSessionHistoryItem(ActiveSessionHistoryItem historyItem)
		{
			ObservableCollection<ActiveSessionHistoryItem> activeSessionHistoryItems;
			if (!_activeSessionHistoryItemMapping.TryGetValue(historyItem.SessionId, out activeSessionHistoryItems))
			{
				_activeSessionHistoryItemMapping[historyItem.SessionId] = activeSessionHistoryItems = new ObservableCollection<ActiveSessionHistoryItem>();
			}

			activeSessionHistoryItems.Add(historyItem);
		}
	}

	[DebuggerDisplay("SessionLongOperationCollection (SessionId={SessionId})")]
	public class SessionLongOperationCollection : ModelBase
	{
		private SqlMonitorSessionLongOperationItem _activeLongOperationItem;

		public int SessionId { get; set; }

		public SqlMonitorPlanItem PlanItem { get; set; }

		public ObservableCollection<SqlMonitorSessionLongOperationItem> CompletedSessionLongOperationItems { get; } = new ObservableCollection<SqlMonitorSessionLongOperationItem>();

		public SqlMonitorSessionLongOperationItem ActiveLongOperationItem
		{
			get { return _activeLongOperationItem; }
			set { UpdateValueAndRaisePropertyChanged(ref _activeLongOperationItem, value); }
		}

		public void MergeItems(IList<SqlMonitorSessionLongOperationItem> items)
		{
			ActiveLongOperationItem = items.Last();

			for (var i = 0; i < items.Count - 1; i++)
			{
				var item = items[i];
				if (CompletedSessionLongOperationItems.Count == i)
				{
					CompletedSessionLongOperationItems.Add(item);
				}
				else
				{
					CompletedSessionLongOperationItems[i] = item;
				}
			}
		}
	}

	[DebuggerDisplay("SqlMonitorSessionLongOperationItem (Message={_message})")]
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

	public class SqlMonitorSessionPlanItem : ModelBase
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
		private SqlMonitorSessionItem _sessionItem;

		public int SessionId { get; set; }

		public SqlMonitorSessionItem SessionItem
		{
			get { return _sessionItem; }
			set { UpdateValueAndRaisePropertyChanged(ref _sessionItem, value); }
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
			if (SessionId != 0)
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

		public void MergeSessionItem(SqlMonitorSessionPlanItem sessionPlanItem)
		{
			Starts += sessionPlanItem.Starts;
			OutputRows += sessionPlanItem.OutputRows;
			IoInterconnectBytes += sessionPlanItem.IoInterconnectBytes;
			PhysicalReadRequests += sessionPlanItem.PhysicalReadRequests;
			PhysicalReadBytes += sessionPlanItem.PhysicalReadBytes;
			PhysicalWriteRequests += sessionPlanItem.PhysicalWriteRequests;
			PhysicalWriteBytes += sessionPlanItem.PhysicalWriteBytes;

			MergeNullableValue(ref _workAreaMemoryBytes, sessionPlanItem._workAreaMemoryBytes);
			MergeNullableValue(ref _workAreaMemoryMaxBytes, sessionPlanItem._workAreaMemoryMaxBytes);
			MergeNullableValue(ref _workAreaTempBytes, sessionPlanItem._workAreaTempBytes);
			MergeNullableValue(ref _workAreaTempMaxBytes, sessionPlanItem._workAreaTempMaxBytes);
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
			var lastSampleTime = DataModel.LastSampleTime ?? DateTime.MinValue;

			command.CommandText = OracleDatabaseCommands.SelectActiveSessionHistoryCommandText;
			command.AddSimpleParameter("SID", DataModel.SessionId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SAMPLE_TIME", lastSampleTime);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var historyItems = new List<ActiveSessionHistoryItem>();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionId = Convert.ToInt32(reader["SESSION_ID"]);
				var planLineId = OracleReaderValueConvert.ToInt32(reader["SQL_PLAN_LINE_ID"]);
				if (planLineId == null)
				{
					continue;
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
						PlanItem = DataModel.AllItems[planLineId.Value]
					};

				historyItems.Add(historyItem);
			}

			DataModel.AddActiveSessionHistoryItems(historyItems);
		}

		public override bool IsValid => DataModel.AllItems.Count > 0;
	}

	internal class SqlMonitorSessionPlanMonitorDataProvider : ModelDataProvider<SqlMonitorPlanItemCollection>
	{
		public SqlMonitorSessionPlanMonitorDataProvider(SqlMonitorPlanItemCollection sqlMonitorPlanItemCollection)
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
			var queryCoordinatorSessionItems = new Dictionary<int, SqlMonitorSessionPlanItem>();
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionId = Convert.ToInt32(reader["SID"]);
				var planLineId = Convert.ToInt32(reader["PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				var sqlMonitorPlanItem = sessionId == DataModel.SessionId
					? queryCoordinatorSessionItems.GetAndAddIfMissing(planLineId)
					: planItem.GetSessionPlanItem(sessionId);

				if (sqlMonitorPlanItem.SessionItem == null)
				{
					sqlMonitorPlanItem.SessionItem = DataModel.GetSessionItem(sessionId);
				}

				sqlMonitorPlanItem.Starts = Convert.ToInt64(reader["STARTS"]);
				sqlMonitorPlanItem.OutputRows = Convert.ToInt64(reader["OUTPUT_ROWS"]);
				sqlMonitorPlanItem.IoInterconnectBytes = Convert.ToInt64(reader["IO_INTERCONNECT_BYTES"]);
				sqlMonitorPlanItem.PhysicalReadRequests = Convert.ToInt64(reader["PHYSICAL_READ_REQUESTS"]);
				sqlMonitorPlanItem.PhysicalReadBytes = Convert.ToInt64(reader["PHYSICAL_READ_BYTES"]);
				sqlMonitorPlanItem.PhysicalWriteRequests = Convert.ToInt64(reader["PHYSICAL_WRITE_REQUESTS"]);
				sqlMonitorPlanItem.PhysicalWriteBytes = Convert.ToInt64(reader["PHYSICAL_WRITE_BYTES"]);
				sqlMonitorPlanItem.WorkAreaMemoryBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MEM"]);
				sqlMonitorPlanItem.WorkAreaMemoryMaxBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MAX_MEM"]);
				sqlMonitorPlanItem.WorkAreaTempBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_TEMPSEG"]);
				sqlMonitorPlanItem.WorkAreaTempMaxBytes = OracleReaderValueConvert.ToInt64(reader["WORKAREA_MAX_TEMPSEG"]);
			}

			foreach (var planItem in DataModel.AllItems.Values)
			{
				var summaryItem = planItem.AllSessionSummaryPlanItem;
				if (summaryItem == null)
				{
					planItem.AllSessionSummaryPlanItem = summaryItem = new SqlMonitorSessionPlanItem();
					summaryItem.SessionItem = DataModel.GetSessionItem(DataModel.SessionId);
				}

				summaryItem.Reset();

				SqlMonitorSessionPlanItem masterSessionPlanItem;
				if (queryCoordinatorSessionItems.TryGetValue(planItem.Id, out masterSessionPlanItem))
				{
					summaryItem.MergeSessionItem(masterSessionPlanItem);
				}

				foreach (var planSessionItem in planItem.ParallelSlaveSessionItems)
				{
					summaryItem.MergeSessionItem(planSessionItem);
				}

				summaryItem.NotifyPropertyChanged();
			}
		}

		public override bool IsValid => DataModel.AllItems.Count > 0;
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
			var sessionItems = new Dictionary<SessionPlanItem, List<SqlMonitorSessionLongOperationItem>>();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionId = Convert.ToInt32(reader["SID"]);
				var planLineId = Convert.ToInt32(reader["SQL_PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				var key = new SessionPlanItem { PlanItem = planItem, SessionId = sessionId };
				List<SqlMonitorSessionLongOperationItem> items;
				if (!sessionItems.TryGetValue(key, out items))
				{
					sessionItems.Add(key, items = new List<SqlMonitorSessionLongOperationItem>());
				}

				var longOperationItem = new SqlMonitorSessionLongOperationItem();
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

				items.Add(longOperationItem);
			}

			foreach (var sessionPlanItem in sessionItems)
			{
				var sessionId = sessionPlanItem.Key.SessionId;
				var planItem = sessionPlanItem.Key.PlanItem;
				var longOperationCollection = planItem.GetSessionLongOperationCollection(sessionId);

				longOperationCollection.MergeItems(sessionPlanItem.Value);

				if (sessionId == DataModel.SessionId)
				{
					planItem.QueryCoordinatorLongOperations.MergeItems(sessionPlanItem.Value);
				}
			}
		}

		public override bool IsValid => DataModel.AllItems.Count > 0;

		private struct SessionPlanItem
		{
			public int SessionId;
			public SqlMonitorPlanItem PlanItem;
		}
	}

	internal class SessionMonitorDataProvider : ModelDataProvider<SqlMonitorPlanItemCollection>
	{
		public SessionMonitorDataProvider(SqlMonitorPlanItemCollection sqlMonitorPlanItemCollection)
			: base(sqlMonitorPlanItemCollection)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectSessionMonitorCommandText;
			command.AddSimpleParameter("SID", DataModel.SessionId);
			command.AddSimpleParameter("SQL_ID", DataModel.SqlId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var parallelSlaves = new List<SqlMonitorSessionItem>();
			SqlMonitorSessionItem queryCoordinatorSessionItem = null;

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				//var bindXml = OracleReaderValueConvert.ToString(await reader.GetValueAsynchronous(reader.GetOrdinal("BINDS_XML"), cancellationToken));
				//var otherXml = OracleReaderValueConvert.ToString(await reader.GetValueAsynchronous(reader.GetOrdinal("OTHER_XML"), cancellationToken));

				var isCrossInstanceRaw = OracleReaderValueConvert.ToString(reader["PX_IS_CROSS_INSTANCE"]);

				var sessionItem =
					new SqlMonitorSessionItem
					{
						MaxDegreeOfParallelism = OracleReaderValueConvert.ToInt32(reader["PX_MAXDOP"]),
						MaxDegreeOfParallelismInstances = OracleReaderValueConvert.ToInt32(reader["PX_MAXDOP_INSTANCES"]),
						ParallelServersRequested = OracleReaderValueConvert.ToInt32(reader["PX_SERVERS_REQUESTED"]),
						ParallelServersAllocated = OracleReaderValueConvert.ToInt32(reader["PX_SERVERS_ALLOCATED"]),
						ParallelServerNumber = OracleReaderValueConvert.ToInt32(reader["PX_SERVER#"]),
						ParallelServerGroup = OracleReaderValueConvert.ToInt32(reader["PX_SERVER_GROUP"]),
						ParallelServerSet = OracleReaderValueConvert.ToInt32(reader["PX_SERVER_SET"]),
						ParallelServerQueryCoordinatorInstanceId = OracleReaderValueConvert.ToInt32(reader["PX_QCINST_ID"]),
						IsCrossInstance = String.IsNullOrEmpty(isCrossInstanceRaw) ? (bool?)null : String.Equals(isCrossInstanceRaw, "Y"),
						//Binds = String.IsNullOrEmpty(bindXml) ? null : XDocument.Parse(bindXml),
						//Other = String.IsNullOrEmpty(otherXml) ? null : XDocument.Parse(otherXml),
						SessionId = Convert.ToInt32(reader["SID"]),
						ApplicationWaitTime = TimeSpan.FromTicks(Convert.ToInt64(reader["APPLICATION_WAIT_TIME"]) * 10),
						BufferGets = Convert.ToInt64(reader["BUFFER_GETS"]),
						ClusterWaitTime = TimeSpan.FromTicks(Convert.ToInt64(reader["CLUSTER_WAIT_TIME"]) * 10),
						ConcurrencyWaitTime = TimeSpan.FromTicks(Convert.ToInt64(reader["CONCURRENCY_WAIT_TIME"]) * 10),
						CpuTime = TimeSpan.FromTicks(Convert.ToInt64(reader["CPU_TIME"]) * 10),
						DiskReads = Convert.ToInt64(reader["DISK_READS"]),
						DirectWrites = Convert.ToInt64(reader["DIRECT_WRITES"]),
						ElapsedTime = TimeSpan.FromTicks(Convert.ToInt64(reader["ELAPSED_TIME"]) * 10),
						Fetches = Convert.ToInt64(reader["FETCHES"]),
						IoInterconnectBytes = Convert.ToInt64(reader["IO_INTERCONNECT_BYTES"]),
						JavaExecutionTime = TimeSpan.FromTicks(Convert.ToInt64(reader["JAVA_EXEC_TIME"]) * 10),
						PhysicalReadBytes = Convert.ToInt64(reader["PHYSICAL_READ_BYTES"]),
						PhysicalReadRequests = Convert.ToInt64(reader["PHYSICAL_READ_REQUESTS"]),
						PhysicalWriteBytes = Convert.ToInt64(reader["PHYSICAL_WRITE_BYTES"]),
						PhysicalWriteRequests = Convert.ToInt64(reader["PHYSICAL_WRITE_REQUESTS"]),
						PlSqlExecutionTime = TimeSpan.FromTicks(Convert.ToInt64(reader["PLSQL_EXEC_TIME"]) * 10),
						QueingTime = TimeSpan.FromTicks(Convert.ToInt64(reader["QUEUING_TIME"]) * 10),
						UserIoWaitTime = TimeSpan.FromTicks(Convert.ToInt64(reader["USER_IO_WAIT_TIME"]) * 10)
					};

				//var queryCoordinatorSessionId = Convert.ToInt32(reader["QC_SID"]);

				if (sessionItem.SessionId == DataModel.SessionId)
				{
					queryCoordinatorSessionItem = sessionItem;
				}
				else
				{
					parallelSlaves.Add(sessionItem);
				}
			}

			if (queryCoordinatorSessionItem == null)
			{
				return;
			}

			DataModel.MergeSessionItem(queryCoordinatorSessionItem);

			foreach (var parallelSlave in parallelSlaves)
			{
				parallelSlave.QueryCoordinatorSession = queryCoordinatorSessionItem;
				DataModel.MergeSessionItem(parallelSlave);
			}
		}

		public override bool IsValid => DataModel.AllItems.Count > 0;
	}

	[DebuggerDisplay("SqlMonitorSessionItem (SessionId={SessionId})")]
	public class SqlMonitorSessionItem : ModelBase
	{
		private TimeSpan _elapsedTime;
		private TimeSpan _queingTime;
		private TimeSpan _cpuTime;
		private TimeSpan _applicationWaitTime;
		private TimeSpan _concurrencyWaitTime;
		private TimeSpan _clusterWaitTime;
		private TimeSpan _userIoWaitTime;
		private TimeSpan _plSqlExecutionTime;
		private TimeSpan _javaExecutionTime;
		private long _fetches;
		private long _bufferGets;
		private long _diskReads;
		private long _directWrites;
		private long _ioInterconnectBytes;
		private long _physicalReadRequests;
		private long _physicalReadBytes;
		private long _physicalWriteRequests;
		private long _physicalWriteBytes;

		public int SessionId { get; set; }

		public SqlMonitorSessionItem QueryCoordinatorSession { get; set; }

		public bool? IsCrossInstance { get; set; }

		public int? MaxDegreeOfParallelism { get; set; }

		public int? MaxDegreeOfParallelismInstances { get; set; }

		public int? ParallelServersRequested { get; set; }

		public int? ParallelServersAllocated { get; set; }

		public bool ParallelServersDegraded => ParallelServersAllocated < ParallelServersRequested;

		public int? ParallelServerNumber { get; set; }

		public int? ParallelServerGroup { get; set; }

		public int? ParallelServerSet { get; set; }

		public int? ParallelServerQueryCoordinatorInstanceId { get; set; }

		public XDocument Binds { get; set; }

		public XDocument Other { get; set; }

		public TimeSpan ElapsedTime
		{
			get { return _elapsedTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _elapsedTime, value); }
		}

		public TimeSpan QueingTime
		{
			get { return _queingTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _queingTime, value); }
		}

		public TimeSpan CpuTime
		{
			get { return _cpuTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _cpuTime, value); }
		}

		public long Fetches
		{
			get { return _fetches; }
			set { UpdateValueAndRaisePropertyChanged(ref _fetches, value); }
		}

		public long BufferGets
		{
			get { return _bufferGets; }
			set { UpdateValueAndRaisePropertyChanged(ref _bufferGets, value); }
		}

		public long DiskReads
		{
			get { return _diskReads; }
			set { UpdateValueAndRaisePropertyChanged(ref _diskReads, value); }
		}

		public long DirectWrites
		{
			get { return _directWrites; }
			set { UpdateValueAndRaisePropertyChanged(ref _directWrites, value); }
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

		public TimeSpan ApplicationWaitTime
		{
			get { return _applicationWaitTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _applicationWaitTime, value); }
		}

		public TimeSpan ConcurrencyWaitTime
		{
			get { return _concurrencyWaitTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _concurrencyWaitTime, value); }
		}

		public TimeSpan ClusterWaitTime
		{
			get { return _clusterWaitTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _clusterWaitTime, value); }
		}

		public TimeSpan UserIoWaitTime
		{
			get { return _userIoWaitTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _userIoWaitTime, value); }
		}

		public TimeSpan PlSqlExecutionTime
		{
			get { return _plSqlExecutionTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _plSqlExecutionTime, value); }
		}

		public TimeSpan JavaExecutionTime
		{
			get { return _javaExecutionTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _javaExecutionTime, value); }
		}
	}
}
