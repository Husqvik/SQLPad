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

		public SqlMonitorDataProvider(int instanceId, int sessionId, DateTime executionStart, int executionId, string sqlId, int childNumber)
			: base(null)
		{
			_sqlId = sqlId;
			_childNumber = childNumber;
			_sqlMonitorBuilder = new SqlMonitorBuilder(instanceId, sessionId, _sqlId, executionStart, executionId);
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
		private readonly Dictionary<SessionIdentifier, SqlMonitorSessionItem> _sessionItemMapping = new Dictionary<SessionIdentifier, SqlMonitorSessionItem>();
		private readonly Dictionary<SqlMonitorPlanItem, List<ActiveSessionHistoryItem>> _planItemActiveSessionHistoryItems = new Dictionary<SqlMonitorPlanItem, List<ActiveSessionHistoryItem>>();

		private int _totalActiveSessionHistorySamples;
		private DateTime? _lastSampleTime;

		public SqlMonitorPlanItemCollection(SessionIdentifier sessionIdentifier, string sqlId, DateTime executionStart, int executionId)
		{
			SessionIdentifier = sessionIdentifier;
			SqlId = sqlId;
			ExecutionStart = executionStart;
			ExecutionId = executionId;
			RefreshPeriod = TimeSpan.FromSeconds(1);
		}

		public SessionIdentifier SessionIdentifier { get; }

		public string SqlId { get; }

		public DateTime ExecutionStart { get; set; }

		public int ExecutionId { get; }

		public TimeSpan RefreshPeriod { get; set; }

		public DateTime? LastSampleTime
		{
			get { return _lastSampleTime; }
			private set { UpdateValueAndRaisePropertyChanged(ref _lastSampleTime, value); }
		}

		public ObservableCollection<SqlMonitorSessionItem> SessionItems { get; } = new ObservableCollection<SqlMonitorSessionItem>();

		public SqlMonitorSessionItem GetSessionItem(SessionIdentifier sessionId)
		{
			SqlMonitorSessionItem sessionItem;
			_sessionItemMapping.TryGetValue(sessionId, out sessionItem);
			return sessionItem;
		}

		public void MergeSessionItem(SqlMonitorSessionItem newSessionItem)
		{
			SqlMonitorSessionItem currentSessionItem;
			if (_sessionItemMapping.TryGetValue(newSessionItem.SessionIdentifier, out currentSessionItem))
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
				newSessionItem.PlanItemCollection = this;
				_sessionItemMapping.Add(newSessionItem.SessionIdentifier, newSessionItem);
				SessionItems.Add(newSessionItem);
			}
		}

		public void AddActiveSessionHistoryItems(IEnumerable<ActiveSessionHistoryItem> historyItems)
		{
			foreach (var itemGroup in historyItems.GroupBy(i => i.SessionIdentifier))
			{
				SqlMonitorSessionItem sessionItem;
				if (_sessionItemMapping.TryGetValue(itemGroup.Key, out sessionItem))
				{
					sessionItem.AddActiveSessionHistoryItems(itemGroup);
				}

				foreach (var historyItem in itemGroup)
				{
					if (LastSampleTime == null || historyItem.SampleTime > LastSampleTime)
					{
						LastSampleTime = historyItem.SampleTime;
					}

					List<ActiveSessionHistoryItem> planHistoryItems;
					if (!_planItemActiveSessionHistoryItems.TryGetValue(historyItem.PlanItem, out planHistoryItems))
					{
						_planItemActiveSessionHistoryItems[historyItem.PlanItem] = planHistoryItems = new List<ActiveSessionHistoryItem>();
					}

					planHistoryItems.Add(historyItem);

					_totalActiveSessionHistorySamples++;
				}
			}

			if (LastSampleTime == null)
			{
				return;
			}

			var recentActivityThreshold = LastSampleTime.Value.Add(-RefreshPeriod);

			foreach (var planItemActiveSessionHistoryItems in _planItemActiveSessionHistoryItems)
			{
				planItemActiveSessionHistoryItems.Key.IsBeingExecuted = planItemActiveSessionHistoryItems.Value.Last().SampleTime >= recentActivityThreshold;

				if (_totalActiveSessionHistorySamples > 0)
				{
					var planItemSamples = planItemActiveSessionHistoryItems.Value.Count;
					planItemActiveSessionHistoryItems.Key.ActiveSessionHistorySampleCount = planItemSamples;
					planItemActiveSessionHistoryItems.Key.ActivityRatio = (decimal)planItemSamples / _totalActiveSessionHistorySamples;
				}
			}
		}
	}

	[DebuggerDisplay("ActiveSessionHistoryItem (Instance={SessionIdentifier.Instance}; SessionId={SessionIdentifier.SessionId}; SessionSerial={SessionSerial}; SampleTime={SampleTime}; SessionState={SessionState})")]
	public class ActiveSessionHistoryItem
	{
		public DateTime SampleTime { get; set; }

		public string SessionState { get; set; }

		public SessionIdentifier SessionIdentifier { get; set; }

		public int SessionSerial { get; set; }

		public SqlMonitorPlanItem PlanItem { get; set; }

		public string WaitClass { get; set; }

		public string Event { get; set; }

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

	[DebuggerDisplay("SqlMonitorPlanItem (Id={Id}; Operation={Operation}; ObjectName={ObjectName}; IsInactive={IsInactive}; IsBeingExecuted={IsBeingExecuted}; ExecutionOrder={ExecutionOrder}; Depth={Depth}; IsLeaf={IsLeaf})")]
	public class SqlMonitorPlanItem : ExecutionPlanItem
	{
		private SqlMonitorSessionPlanItem _allSessionSummaryPlanItem;
		private bool _isBeingExecuted;
		private decimal? _activityRatio;
		private int _activeSessionHistorySampleCount;

		private readonly ObservableCollection<SessionLongOperationCollection> _sessionLongOperations = new ObservableCollection<SessionLongOperationCollection>();
		private readonly Dictionary<SessionIdentifier, SessionLongOperationCollection> _sessionLongOperationsItemMapping = new Dictionary<SessionIdentifier, SessionLongOperationCollection>();
		private readonly ObservableCollection<SqlMonitorSessionPlanItem> _parallelSlaveSessionItems = new ObservableCollection<SqlMonitorSessionPlanItem>();
		private readonly Dictionary<SessionIdentifier, SqlMonitorSessionPlanItem> _parallelSlaveSessionItemMapping = new Dictionary<SessionIdentifier, SqlMonitorSessionPlanItem>();

		public SqlMonitorSessionPlanItem AllSessionSummaryPlanItem
		{
			get { return _allSessionSummaryPlanItem; }
			set { UpdateValueAndRaisePropertyChanged(ref _allSessionSummaryPlanItem, value); }
		}

		public IReadOnlyCollection<SqlMonitorSessionPlanItem> ParallelSlaveSessionItems => _parallelSlaveSessionItems;

		public IReadOnlyCollection<SessionLongOperationCollection> SessionLongOperations => _sessionLongOperations;

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

		public int ActiveSessionHistorySampleCount
		{
			get { return _activeSessionHistorySampleCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _activeSessionHistorySampleCount, value); }
		}

		public SqlMonitorSessionPlanItem GetSessionPlanItem(SessionIdentifier sessionIdentifier)
		{
			SqlMonitorSessionPlanItem sessionPlanItem;
			if (!_parallelSlaveSessionItemMapping.TryGetValue(sessionIdentifier, out sessionPlanItem))
			{
				sessionPlanItem =
					new SqlMonitorSessionPlanItem
					{
						SessionIdentifier = sessionIdentifier
					};

				_parallelSlaveSessionItemMapping.Add(sessionIdentifier, sessionPlanItem);
				_parallelSlaveSessionItems.Add(sessionPlanItem);
			}

			return sessionPlanItem;
		}

		public SessionLongOperationCollection GetSessionLongOperationCollection(SessionIdentifier sessionIdentifier)
		{
			SessionLongOperationCollection longOperationCollection;
			if (!_sessionLongOperationsItemMapping.TryGetValue(sessionIdentifier, out longOperationCollection))
			{
				longOperationCollection =
					new SessionLongOperationCollection
					{
						SessionIdentifier = sessionIdentifier,
						PlanItem = this
					};

				_sessionLongOperationsItemMapping.Add(sessionIdentifier, longOperationCollection);
				_sessionLongOperations.Add(longOperationCollection);
			}

			return longOperationCollection;
		}
	}

	[DebuggerDisplay("SessionLongOperationCollection (SessionId={SessionIdentifier})")]
	public class SessionLongOperationCollection : ModelBase
	{
		private SqlMonitorSessionLongOperationItem _activeLongOperationItem;

		public SessionIdentifier SessionIdentifier { get; set; }

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
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _soFar, value))
				{
					RaisePropertyChanged(nameof(ProgressRatio));
				}
			}
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

		public decimal ProgressRatio => SoFar / (decimal)TotalWork;
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

		public SessionIdentifier SessionIdentifier { get; set; }

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
			if (SessionIdentifier.SessionId != 0)
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
		private readonly SessionIdentifier _sessionIdentifier;
		private readonly string _sqlId;
		private readonly DateTime _executionStart;
		private readonly int _executionId;

		public SqlMonitorBuilder(int instanceId, int sessionId, string sqlId, DateTime executionStart, int executionId)
		{
			_sessionIdentifier = new SessionIdentifier(instanceId, sessionId);
			_sqlId = sqlId;
			_executionStart = executionStart;
			_executionId = executionId;
		}

		protected override SqlMonitorPlanItemCollection InitializePlanItemCollection()
		{
			return new SqlMonitorPlanItemCollection(_sessionIdentifier, _sqlId, _executionStart, _executionId);
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
			command.AddSimpleParameter("SID", DataModel.SessionIdentifier.SessionId);
			command.AddSimpleParameter("INST_ID", DataModel.SessionIdentifier.Instance);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SAMPLE_TIME", lastSampleTime);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var historyItems = new List<ActiveSessionHistoryItem>();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionIdentifier = new SessionIdentifier(Convert.ToInt32(reader["INSTANCE_ID"]), Convert.ToInt32(reader["SESSION_ID"]));
				var planLineId = OracleReaderValueConvert.ToInt32(reader["SQL_PLAN_LINE_ID"]);
				if (planLineId == null)
				{
					continue;
				}

				var historyItem =
					new ActiveSessionHistoryItem
					{
						SampleTime = (DateTime)reader["SAMPLE_TIME"],
						SessionIdentifier = sessionIdentifier,
						SessionSerial = Convert.ToInt32(reader["SESSION_SERIAL#"]),
						WaitClass = OracleReaderValueConvert.ToString(reader["WAIT_CLASS"]),
						Event = OracleReaderValueConvert.ToString(reader["EVENT"]),
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
						SessionState = (string)reader["SESSION_STATE"],
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
			command.AddSimpleParameter("SID", DataModel.SessionIdentifier.SessionId);
			command.AddSimpleParameter("INST_ID", DataModel.SessionIdentifier.Instance);
			command.AddSimpleParameter("SQL_ID", DataModel.SqlId);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var queryCoordinatorSessionItems = new Dictionary<int, SqlMonitorSessionPlanItem>();
			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionId = new SessionIdentifier(Convert.ToInt32(reader["INSTANCE_ID"]), Convert.ToInt32(reader["SID"]));
				var planLineId = Convert.ToInt32(reader["PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				var sqlMonitorPlanItem = sessionId == DataModel.SessionIdentifier
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
					summaryItem.SessionItem = DataModel.GetSessionItem(DataModel.SessionIdentifier);
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
			command.AddSimpleParameter("SID", DataModel.SessionIdentifier.SessionId);
			command.AddSimpleParameter("INST_ID", DataModel.SessionIdentifier.Instance);
			command.AddSimpleParameter("SQL_EXEC_ID", DataModel.ExecutionId);
			command.AddSimpleParameter("SQL_EXEC_START", DataModel.ExecutionStart);
		}

		public async override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var sessionItems = new Dictionary<SessionPlanItem, List<SqlMonitorSessionLongOperationItem>>();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var sessionIdentifier = new SessionIdentifier(Convert.ToInt32(reader["INSTANCE_ID"]), Convert.ToInt32(reader["SID"]));
				var planLineId = Convert.ToInt32(reader["SQL_PLAN_LINE_ID"]);
				var planItem = DataModel.AllItems[planLineId];

				var key = new SessionPlanItem { PlanItem = planItem, SessionIdentifier = sessionIdentifier };
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
				var sessionIdentifier = sessionPlanItem.Key.SessionIdentifier;
				var planItem = sessionPlanItem.Key.PlanItem;
				var longOperationCollection = planItem.GetSessionLongOperationCollection(sessionIdentifier);

				longOperationCollection.MergeItems(sessionPlanItem.Value);

				if (sessionIdentifier == DataModel.SessionIdentifier)
				{
					planItem.QueryCoordinatorLongOperations.MergeItems(sessionPlanItem.Value);
				}
			}
		}

		public override bool IsValid => DataModel.AllItems.Count > 0;

		private struct SessionPlanItem
		{
			public SessionIdentifier SessionIdentifier;
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
			command.AddSimpleParameter("SID", DataModel.SessionIdentifier.SessionId);
			command.AddSimpleParameter("INST_ID", DataModel.SessionIdentifier.Instance);
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
						SessionIdentifier = new SessionIdentifier(Convert.ToInt32(reader["INSTANCE_ID"]), Convert.ToInt32(reader["SID"])),
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

				if (sessionItem.SessionIdentifier == DataModel.SessionIdentifier)
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

	[DebuggerDisplay("SqlMonitorSessionItem (Instance={SessionIdentifier.Instance}l SessionId={SessionIdentifier.SessionId})")]
	public class SqlMonitorSessionItem : ModelBase
	{
		private readonly ObservableCollection<ActiveSessionHistoryItem> _activeSessionHistoryItems = new ObservableCollection<ActiveSessionHistoryItem>();

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
		private readonly List<string> _topActivities = new List<string>();

		internal SqlMonitorPlanItemCollection PlanItemCollection { get; set; }

		public SessionIdentifier SessionIdentifier { get; set; }

		public SqlMonitorSessionItem QueryCoordinatorSession { get; set; }

		public bool? IsCrossInstance { get; set; }

		public int? MaxDegreeOfParallelism { get; set; }

		public int? MaxDegreeOfParallelismInstances { get; set; }

		public int? ParallelServersRequested { get; set; }

		public int? ParallelServersAllocated { get; set; }

		public IReadOnlyCollection<ActiveSessionHistoryItem> ActiveSessionHistoryItems => _activeSessionHistoryItems;

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

		public IReadOnlyList<string> TopActivities => _topActivities;

		public void AddActiveSessionHistoryItems(IEnumerable<ActiveSessionHistoryItem> historyItems)
		{
			_activeSessionHistoryItems.AddRange(historyItems);

			var topActivities = _activeSessionHistoryItems
				.GroupBy(i => i.Event)
				.Select(g => new { Event = String.IsNullOrEmpty(g.Key) ? "On CPU" : g.Key, Count = g.Count() })
				.OrderByDescending(e => e.Count)
				.Select(a => $"{a.Event} ({a.Count})")
				.Take(4);

			_topActivities.Clear();
			_topActivities.AddRange(topActivities);
			RaisePropertyChanged(nameof(TopActivities));
		}
	}
}
