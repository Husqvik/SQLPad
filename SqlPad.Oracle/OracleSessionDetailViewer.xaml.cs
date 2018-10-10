using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle
{
	public partial class OracleSessionDetailViewer : IDatabaseSessionDetailViewer
	{
		public static readonly DependencyProperty IsParallelProperty = DependencyProperty.Register(nameof(IsParallel), typeof(bool), typeof(OracleSessionDetailViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty SessionItemsProperty = DependencyProperty.Register(nameof(SessionItems), typeof(ObservableCollection<SqlMonitorSessionItem>), typeof(OracleSessionDetailViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty SummarySessionProperty = DependencyProperty.Register(nameof(SummarySession), typeof(SessionSummaryCollection), typeof(OracleSessionDetailViewer), new FrameworkPropertyMetadata(new SessionSummaryCollection()));
		public static readonly DependencyProperty QueryCoordinatorLongOperationsProperty = DependencyProperty.Register(nameof(QueryCoordinatorLongOperations), typeof(SessionLongOperationCollection), typeof(OracleSessionDetailViewer), new FrameworkPropertyMetadata());

		public bool IsParallel
		{
			get { return (bool)GetValue(IsParallelProperty); }
			private set { SetValue(IsParallelProperty, value); }
		}

		public ObservableCollection<SqlMonitorSessionItem> SessionItems
		{
			get { return (ObservableCollection<SqlMonitorSessionItem>)GetValue(SessionItemsProperty); }
			private set { SetValue(SessionItemsProperty, value); }
		}

		public SessionSummaryCollection SummarySession
		{
			get { return (SessionSummaryCollection)GetValue(SummarySessionProperty); }
			private set { SetValue(SummarySessionProperty, value); }
		}

		public SessionLongOperationCollection QueryCoordinatorLongOperations
		{
			get { return (SessionLongOperationCollection)GetValue(QueryCoordinatorLongOperationsProperty); }
			private set { SetValue(QueryCoordinatorLongOperationsProperty, value); }
		}

		private const string KeyExecutionPlanHeight = "ExecutionPlanHeight";
		private const string KeySessionListHeight = "SessionListHeight";
		private const string KeyExpanderExecutionPlanIsExpanded = "ExpanderExecutionPlanIsExpanded";
		private const string KeyExpanderSessionDetailsIsExpanded = "ExpanderSessionDetailsIsExpanded";
		private const string KeyExpanderLongOperationsIsExpanded = "ExpanderLongOperationsIsExpanded";

		private static readonly object LockObject = new object();
		private static readonly TimeSpan DefaultRefreshPeriod = TimeSpan.FromSeconds(10);

		private readonly ConnectionStringSettings _connectionString;
		private readonly DispatcherTimer _refreshTimer;

		private bool _isBusy;
		private bool _isSynchronizing;
		private bool _autoRefreshEnabled;
		private double? _expandedExecutionPlanHeight;
		private double? _expandedSessionListHeight;
		private SqlMonitorPlanItemCollection _planItemCollection;
		private OracleSessionValues _oracleSessionValues;

		public Control Control => this;

		public DatabaseSession DatabaseSession { get; private set; }

		public bool AutoRefreshEnabled
		{
			get { return _autoRefreshEnabled; }
			set
			{
				_refreshTimer.IsEnabled = value;
				_autoRefreshEnabled = value;
			}
		}

		private SortDescriptionCollection DefaultSortDescriptions => ((CollectionViewSource)Resources["SortedSessionItems"]).SortDescriptions;

		public OracleSessionDetailViewer(ConnectionStringSettings connectionString)
		{
			InitializeComponent();

			_connectionString = connectionString;

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = DefaultRefreshPeriod };
			_refreshTimer.Tick += RefreshTimerTickHandler;

			SetupColumnSynchronization();
		}

		private void SetupColumnSynchronization()
		{
			var summaryColumnMapping = new Dictionary<DataGridColumn, DataGridColumn>();

			for (var i = 0; i < SummaryDataGrid.Columns.Count; i++)
			{
				var sourceColumn = SessionDataGrid.Columns[i];
				var summaryColumn = SummaryDataGrid.Columns[i];

				summaryColumnMapping.Add(sourceColumn, summaryColumn);

				DataGridColumn.WidthProperty.AddValueChanged(
					sourceColumn,
					delegate
					{
						if (_isSynchronizing)
						{
							return;
						}

						_isSynchronizing = true;
						summaryColumn.Width = sourceColumn.Width;
						_isSynchronizing = false;
					});

				DataGridColumn.WidthProperty.AddValueChanged(
					summaryColumn,
					delegate
					{
						if (_isSynchronizing) return;
						_isSynchronizing = true;
						sourceColumn.Width = summaryColumn.Width;
						_isSynchronizing = false;
					});
			}

			SessionDataGrid.ColumnDisplayIndexChanged +=
				(sender, args) =>
				{
					var summaryColumn = summaryColumnMapping[args.Column];
					summaryColumn.DisplayIndex = args.Column.DisplayIndex;
				};
		}

		private async void RefreshTimerTickHandler(object sender, EventArgs eventArgs)
		{
			await SafeRefresh();
		}

		private async Task SafeRefresh()
		{
			_refreshTimer.IsEnabled = false;
			var exception = await App.SafeActionAsync(() => Refresh(CancellationToken.None));
			if (exception != null)
			{
				Messages.ShowError(exception.Message, owner: Window.GetWindow(this));
			}

			_refreshTimer.IsEnabled = _autoRefreshEnabled;
		}

		public async Task Refresh(CancellationToken cancellationToken)
		{
			if (_isBusy)
			{
				return;
			}

			lock (LockObject)
			{
				if (_isBusy || _planItemCollection == null)
				{
					return;
				}

				_isBusy = true;
			}

			try
			{
				var planItemCollection = _planItemCollection;
				var sessionMonitorDataProvider = new SessionMonitorDataProvider(planItemCollection);
				var activeSessionHistoryDataProvider = new SqlMonitorActiveSessionHistoryDataProvider(planItemCollection);
				var planMonitorDataProvider = new SqlMonitorSessionPlanMonitorDataProvider(planItemCollection);
				var sessionLongOperationDataProvider = new SessionLongOperationPlanMonitorDataProvider(planItemCollection);
				await OracleDatabaseModel.UpdateModelAsync(OracleConnectionStringRepository.GetBackgroundConnectionString(_connectionString.ConnectionString), null, false, cancellationToken, sessionMonitorDataProvider, planMonitorDataProvider, activeSessionHistoryDataProvider, sessionLongOperationDataProvider);
				if (planItemCollection != _planItemCollection)
				{
					return;
				}

				IsParallel = planItemCollection.SessionItems.Count > 1;
			}
			finally
			{
				_isBusy = false;
			}
		}

		public async Task Initialize(DatabaseSession databaseSession, CancellationToken cancellationToken)
		{
			databaseSession = databaseSession.Owner ?? databaseSession;
			var oracleSessionValues = (OracleSessionValues)databaseSession.ProviderValues;
			if (_oracleSessionValues != null && _oracleSessionValues.Instance == oracleSessionValues.Instance && _oracleSessionValues.Id == oracleSessionValues.Id && String.Equals(_oracleSessionValues.SqlId, oracleSessionValues.SqlId) && _oracleSessionValues.ExecutionId == oracleSessionValues.ExecutionId)
			{
				await Refresh(cancellationToken);
				return;
			}

			Shutdown();

			DatabaseSession = databaseSession;

			try
			{
				_oracleSessionValues = oracleSessionValues.Clone();

				if (!String.IsNullOrEmpty(_oracleSessionValues.SqlId) && _oracleSessionValues.ExecutionId.HasValue)
				{
					var monitorDataProvider = new SqlMonitorDataProvider(_oracleSessionValues.Instance, _oracleSessionValues.Id, _oracleSessionValues.ExecutionStart.Value, _oracleSessionValues.ExecutionId.Value, _oracleSessionValues.SqlId, _oracleSessionValues.ChildNumber.Value);
					await OracleDatabaseModel.UpdateModelAsync(OracleConnectionStringRepository.GetBackgroundConnectionString(_connectionString.ConnectionString), null, false, cancellationToken, monitorDataProvider);

					_planItemCollection = monitorDataProvider.ItemCollection;
					_planItemCollection.RefreshPeriod = DefaultRefreshPeriod;
					//QueryCoordinatorLongOperations = _planItemCollection.QueryCoordinatorLongOperations;

					if (_planItemCollection.RootItem != null)
					{
						ExecutionPlanTreeView.RootItem = _planItemCollection.RootItem;
						SessionItems = _planItemCollection.SessionItems;
						var collectionView = CollectionViewSource.GetDefaultView(SessionItems);
						collectionView.SortDescriptions.Add(new SortDescription { PropertyName = "SessionIdentifier.Instance" });
						collectionView.SortDescriptions.Add(new SortDescription { PropertyName = "ParallelServerGroup" });
						collectionView.SortDescriptions.Add(new SortDescription { PropertyName = "ParallelServerSet" });
						collectionView.SortDescriptions.Add(new SortDescription { PropertyName = "ParallelServerNumber" });

						SummarySession.Inititalize(_planItemCollection);

						await SafeRefresh();
					}
				}
			}
			finally
			{
				AutoRefreshEnabled = true;
			}
		}

		public void Shutdown()
		{
			if (DatabaseSession != null)
			{
				DatabaseSession = null;
				_oracleSessionValues = null;
			}

			AutoRefreshEnabled = false;
			_planItemCollection = null;
			SessionItems = null;
			QueryCoordinatorLongOperations = null;
			SummarySession.Clear();
			IsParallel = false;
			ExecutionPlanTreeView.RootItem = null;
		}

		public void StoreConfiguration(IDictionary<string, double> configurationProperties)
		{
			if (ExpanderExecutionPlan.IsExpanded)
			{
				configurationProperties[KeyExecutionPlanHeight] = RowDefinitionExecutionPlan.ActualHeight;
			}
			else if (_expandedExecutionPlanHeight.HasValue)
			{
				configurationProperties[KeyExecutionPlanHeight] = _expandedExecutionPlanHeight.Value;
			}

			if (ExpanderSessionDetails.IsExpanded)
			{
				configurationProperties[KeySessionListHeight] = RowDefinitionSessionList.ActualHeight;
			}
			else if (_expandedSessionListHeight.HasValue)
			{
				configurationProperties[KeySessionListHeight] = _expandedSessionListHeight.Value;
			}

			configurationProperties[KeyExpanderExecutionPlanIsExpanded] = Convert.ToDouble(ExpanderExecutionPlan.IsExpanded);
			configurationProperties[KeyExpanderSessionDetailsIsExpanded] = Convert.ToDouble(ExpanderSessionDetails.IsExpanded);
			configurationProperties[KeyExpanderLongOperationsIsExpanded] = Convert.ToDouble(ExpanderLongOperations.IsExpanded);
		}

		public void RestoreConfiguration(IDictionary<string, double> configurationProperties)
		{
			if (configurationProperties == null)
			{
				return;
			}

			if (configurationProperties.TryGetValue(KeyExecutionPlanHeight, out var value))
			{
				_expandedExecutionPlanHeight = value;
				RowDefinitionExecutionPlan.Height = new GridLength(value);
			}

			if (configurationProperties.TryGetValue(KeySessionListHeight, out value))
			{
				_expandedSessionListHeight = value;
				RowDefinitionSessionList.Height = new GridLength(value);
			}

			if (configurationProperties.TryGetValue(KeyExpanderExecutionPlanIsExpanded, out value))
			{
				ExpanderExecutionPlan.IsExpanded = Convert.ToBoolean(value);
			}

			if (configurationProperties.TryGetValue(KeyExpanderSessionDetailsIsExpanded, out value))
			{
				ExpanderSessionDetails.IsExpanded = Convert.ToBoolean(value);
			}

			if (configurationProperties.TryGetValue(KeyExpanderLongOperationsIsExpanded, out value))
			{
				ExpanderLongOperations.IsExpanded = Convert.ToBoolean(value);
			}
		}

		private EventHandler<DataGridBeginningEditEventArgs> SessionDataGridBeginningEditCancelTextInputHandler => App.DataGridBeginningEditCancelTextInputHandlerImplementation;

		private void SessionDataGridSortingHandler(object sender, DataGridSortingEventArgs args)
		{
			var dataGrid = (DataGrid)sender;
			if (args.Column.SortDirection != ListSortDirection.Descending || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				return;
			}

			args.Column.SortDirection = null;
			dataGrid.Items.SortDescriptions.Clear();
			dataGrid.Items.SortDescriptions.AddRange(DefaultSortDescriptions);
			dataGrid.Items.Refresh();
			args.Handled = true;
		}

		private void ExpanderExecutionPlanExpandedHandler(object sender, RoutedEventArgs args)
		{
			if (_expandedExecutionPlanHeight.HasValue)
			{
				RowDefinitionExecutionPlan.Height = new GridLength(_expandedExecutionPlanHeight.Value, GridUnitType.Pixel);
			}
		}

		private void ExpanderExecutionPlanCollapsedHandler(object sender, RoutedEventArgs args)
		{
			if (RowDefinitionExecutionPlan.ActualHeight > 0)
			{
				_expandedExecutionPlanHeight = RowDefinitionExecutionPlan.ActualHeight;
			}

			RowDefinitionExecutionPlan.Height = new GridLength(RowDefinitionExecutionPlan.MinHeight, GridUnitType.Pixel);
		}
	}

	public class SessionSummaryCollection : List<SqlMonitorSessionItem>
	{
		private ObservableCollection<SqlMonitorSessionItem> _items;
		private SessionIdentifier _queryCoordinatorSessionId;

		private SqlMonitorSessionItem SummaryItem { get; } = new SqlMonitorSessionItem();

		public void Inititalize(SqlMonitorPlanItemCollection planItemCollection)
		{
			Clear();

			if (_items != null)
			{
				_items.CollectionChanged -= ItemsCollectionChangedHandler;

				foreach (var oldItem in _items)
				{
					oldItem.PropertyChanged -= ItemPropertyChangedHandler;
				}
			}

			_items = planItemCollection.SessionItems;
			_queryCoordinatorSessionId = planItemCollection.SessionIdentifier;

			_items.CollectionChanged += ItemsCollectionChangedHandler;
		}

		private void ItemsCollectionChangedHandler(object sender, NotifyCollectionChangedEventArgs args)
		{
			foreach (SqlMonitorSessionItem newItem in args.NewItems)
			{
				if (_queryCoordinatorSessionId == newItem.SessionIdentifier)
				{
					SummaryItem.IsCrossInstance = newItem.IsCrossInstance;
					SummaryItem.ParallelServersAllocated = newItem.ParallelServersAllocated;
					SummaryItem.ParallelServersRequested = newItem.ParallelServersRequested;
					SummaryItem.MaxDegreeOfParallelism = newItem.MaxDegreeOfParallelism;
					SummaryItem.MaxDegreeOfParallelismInstances = newItem.MaxDegreeOfParallelismInstances;

					Add(SummaryItem);
				}

				newItem.PropertyChanged += ItemPropertyChangedHandler;
			}

			Update();
		}

		private void ItemPropertyChangedHandler(object o, PropertyChangedEventArgs eventArgs)
		{
			Update();
		}

		public void Update()
		{
			var elapsedTime = TimeSpan.Zero;
			var queingTime = TimeSpan.Zero;
			var cpuTime = TimeSpan.Zero;
			var applicationWaitTime = TimeSpan.Zero;
			var concurrencyWaitTime = TimeSpan.Zero;
			var clusterWaitTime = TimeSpan.Zero;
			var userIoWaitTime = TimeSpan.Zero;
			var plSqlExecutionTime = TimeSpan.Zero;
			var javaExecutionTime = TimeSpan.Zero;
			var fetches = 0L;
			var bufferGets = 0L;
			var diskReads = 0L;
			var directWrites = 0L;
			var ioInterconnectBytes = 0L;
			var physicalReadRequests = 0L;
			var physicalReadBytes = 0L;
			var physicalWriteRequests = 0L;
			var physicalWriteBytes = 0L;

			foreach (var item in _items)
			{
				elapsedTime += item.ElapsedTime;
				queingTime += item.QueingTime;
				cpuTime += item.CpuTime;
				applicationWaitTime += item.ApplicationWaitTime;
				concurrencyWaitTime += item.ConcurrencyWaitTime;
				clusterWaitTime += item.ClusterWaitTime;
				userIoWaitTime += item.UserIoWaitTime;
				plSqlExecutionTime += item.PlSqlExecutionTime;
				javaExecutionTime += item.JavaExecutionTime;
				fetches += item.Fetches;
				bufferGets += item.BufferGets;
				diskReads += item.DiskReads;
				directWrites += item.DirectWrites;
				ioInterconnectBytes += item.IoInterconnectBytes;
				physicalReadRequests += item.PhysicalReadRequests;
				physicalReadBytes += item.PhysicalReadBytes;
				physicalWriteRequests += item.PhysicalWriteRequests;
				physicalWriteBytes += item.PhysicalWriteBytes;
			}

			SummaryItem.ElapsedTime = elapsedTime;
			SummaryItem.QueingTime = queingTime;
			SummaryItem.CpuTime = cpuTime;
			SummaryItem.ApplicationWaitTime = applicationWaitTime;
			SummaryItem.ConcurrencyWaitTime = concurrencyWaitTime;
			SummaryItem.ClusterWaitTime = clusterWaitTime;
			SummaryItem.UserIoWaitTime = userIoWaitTime;
			SummaryItem.PlSqlExecutionTime = plSqlExecutionTime;
			SummaryItem.JavaExecutionTime = javaExecutionTime;
			SummaryItem.Fetches = fetches;
			SummaryItem.BufferGets = bufferGets;
			SummaryItem.DiskReads = diskReads;
			SummaryItem.DirectWrites = directWrites;
			SummaryItem.IoInterconnectBytes = ioInterconnectBytes;
			SummaryItem.PhysicalReadRequests = physicalReadRequests;
			SummaryItem.PhysicalReadBytes = physicalReadBytes;
			SummaryItem.PhysicalWriteRequests = physicalWriteRequests;
			SummaryItem.PhysicalWriteBytes = physicalWriteBytes;
		}
	}

	public class SessionIdSummaryConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Equals(value, 0) ? "Total" : value;
		}
	}
}
