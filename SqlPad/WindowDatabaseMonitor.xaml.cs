using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SqlPad.Commands;

namespace SqlPad
{
	public partial class WindowDatabaseMonitor
	{
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register(nameof(CurrentConnection), typeof(ConnectionStringSettings), typeof(WindowDatabaseMonitor), new FrameworkPropertyMetadata(CurrentConnectionChangedCallbackHandler));
		public static readonly DependencyProperty UserSessionOnlyProperty = DependencyProperty.Register(nameof(UserSessionOnly), typeof(bool), typeof(WindowDatabaseMonitor), new UIPropertyMetadata(true, UserSessionOnlyChangedCallbackHandler));
		public static readonly DependencyProperty ActiveSessionOnlyProperty = DependencyProperty.Register(nameof(ActiveSessionOnly), typeof(bool), typeof(WindowDatabaseMonitor), new UIPropertyMetadata(true, ActiveSessionOnlyChangedCallbackHandler));
		public static readonly DependencyProperty MasterSessionOnlyProperty = DependencyProperty.Register(nameof(MasterSessionOnly), typeof(bool), typeof(WindowDatabaseMonitor), new UIPropertyMetadata(true, MasterSessionOnlyChangedCallbackHandler));

		[Bindable(true)]
		public ConnectionStringSettings CurrentConnection
		{
			get { return (ConnectionStringSettings)GetValue(CurrentConnectionProperty); }
			set { SetValue(CurrentConnectionProperty, value); }
		}

		private static void CurrentConnectionChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var monitorWindow = (WindowDatabaseMonitor)dependencyObject;
			monitorWindow.Initialize();
		}

		[Bindable(true)]
		public bool UserSessionOnly
		{
			get { return (bool)GetValue(UserSessionOnlyProperty); }
			set { SetValue(UserSessionOnlyProperty, value); }
		}

		private static void UserSessionOnlyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var window = (WindowDatabaseMonitor)dependencyObject;
			window.UpdateFilterSettings(c => c.UserSessionOnly = (bool)args.NewValue);
		}

		[Bindable(true)]
		public bool ActiveSessionOnly
		{
			get { return (bool)GetValue(ActiveSessionOnlyProperty); }
			set { SetValue(ActiveSessionOnlyProperty, value); }
		}

		private static void ActiveSessionOnlyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var window = (WindowDatabaseMonitor)dependencyObject;
			window.UpdateFilterSettings(c => c.ActiveSessionOnly = (bool)args.NewValue);
		}

		[Bindable(true)]
		public bool MasterSessionOnly
		{
			get { return (bool)GetValue(MasterSessionOnlyProperty); }
			set { SetValue(MasterSessionOnlyProperty, value); }
		}

		private static void MasterSessionOnlyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var window = (WindowDatabaseMonitor)dependencyObject;
			window.UpdateFilterSettings(c => c.MasterSessionOnly = (bool)args.NewValue);
		}

		private const string SessionDataGridHeightProperty = "SessionDataGridSplitter";

		private static readonly object LockObject = new object();

		private readonly ObservableCollection<DatabaseSession> _databaseSessions = new ObservableCollection<DatabaseSession>();
		private readonly DispatcherTimer _refreshTimer;

		private bool _isBusy;
		private IDatabaseMonitor _databaseMonitor;
		private IDatabaseSessionDetailViewer _sessionDetailViewer;
		private IDictionary<string, double> _customProperties;

		public IReadOnlyList<DatabaseSession> DatabaseSessions => _databaseSessions;

		private EventHandler<DataGridBeginningEditEventArgs> DataGridBeginningEditCancelTextInputHandler => App.DataGridBeginningEditCancelTextInputHandlerImplementation;

		private CollectionViewSource SessionDataGridSource => (CollectionViewSource)Resources["FilteredDatabaseSessions"];

		public WindowDatabaseMonitor()
		{
			InitializeComponent();

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
			_refreshTimer.Tick += delegate { Refresh(); };
		}

		private void UpdateFilterSettings(Action<DatabaseMonitorConfiguration> updateConfigurationAction)
		{
			var providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(CurrentConnection.ProviderName);
			updateConfigurationAction(providerConfiguration.DatabaseMonitorConfiguration);

			SessionDataGridSource.View.Refresh();
		}

		public void RestoreAppearance()
		{
			_customProperties = WorkDocumentCollection.RestoreWindowProperties(this);

			double sessionDataGridHeight = 0;
			if (_customProperties?.TryGetValue(SessionDataGridHeightProperty, out sessionDataGridHeight) == true)
			{
				RowDefinitionSessionDataGrid.Height = new GridLength(sessionDataGridHeight);
			}
		}

		private void Initialize()
		{
			var connectionConfiguration = ConfigurationProvider.GetConnectionConfiguration(CurrentConnection.Name);
			var infrastructureFactory = connectionConfiguration.InfrastructureFactory;
			_databaseMonitor = infrastructureFactory.CreateDatabaseMonitor(CurrentConnection);
			_sessionDetailViewer = _databaseMonitor.CreateSessionDetailViewer();
			_sessionDetailViewer.RestoreConfiguration(_customProperties);

			var providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(CurrentConnection.ProviderName);
			ActiveSessionOnly = providerConfiguration.DatabaseMonitorConfiguration.ActiveSessionOnly;
			UserSessionOnly = providerConfiguration.DatabaseMonitorConfiguration.UserSessionOnly;
			MasterSessionOnly = providerConfiguration.DatabaseMonitorConfiguration.MasterSessionOnly;

			if (!String.IsNullOrEmpty(providerConfiguration.DatabaseMonitorConfiguration.SortMemberPath))
			{
				SessionDataGridSource.SortDescriptions.Clear();
				SessionDataGridSource.SortDescriptions.Add(new SortDescription(providerConfiguration.DatabaseMonitorConfiguration.SortMemberPath, providerConfiguration.DatabaseMonitorConfiguration.SortColumnOrder));
			}

			lock (_databaseSessions)
			{
				_databaseSessions.Clear();
			}

			Refresh();
		}

		public async void Refresh()
		{
			if (_isBusy)
			{
				return;
			}

			lock (LockObject)
			{
				if (_isBusy)
				{
					return;
				}

				_isBusy = true;
			}

			_refreshTimer.IsEnabled = false;

			Task<DatabaseSessions> task = null;
			var exception = await App.SafeActionAsync(() => task = _databaseMonitor.GetAllSessionDataAsync(CancellationToken.None));
			_isBusy = false;

			if (exception != null)
			{
				Messages.ShowError(exception.Message, owner: this);
				return;
			}

			_refreshTimer.IsEnabled = true;

			var sessions = task.Result;

			if (!AreColumnHeadersEqual(sessions.ColumnHeaders))
			{
				SessionDataGrid.Columns.Clear();
				foreach (var columnHeader in sessions.ColumnHeaders)
				{
					var column =
						new DataGridTextColumn
						{
							Binding = new Binding($"{nameof(DatabaseSession.Values)}[{columnHeader.ColumnIndex}]") { Converter = columnHeader.CustomConverter ?? CellValueConverter.Instance },
							EditingElementStyle = (Style)Application.Current.Resources["CellTextBoxStyleReadOnly"]
						};

					DataGridHelper.ApplyColumnStyle(column, columnHeader);
					SessionDataGrid.Columns.Add(column);
				}
			}

			lock (_databaseSessions)
			{
				MergeRecords(_databaseSessions, sessions.Rows, r => r.Id, MergeSessionRecordData);
			}
		}

		private void MergeSessionRecordData(DatabaseSession current, DatabaseSession @new)
		{
			current.IsActive = @new.IsActive;
			current.Type = @new.Type;
			current.ProviderValues = @new.ProviderValues;

			if (_sessionDetailViewer.DatabaseSession == current)
			{
				_sessionDetailViewer.Initialize(current, CancellationToken.None);
			}
		}

		public void MergeRecords<TRecord>(IList<TRecord> currentRecords, IEnumerable<TRecord> newRecords, Func<TRecord, object> getKeyFunction, Action<TRecord, TRecord> mergeAction)
		{
			var newRecordDictionary = newRecords.ToDictionary(getKeyFunction);

			for (var i = currentRecords.Count - 1; i >= 0; i--)
			{
				var currentRecord = currentRecords[i];
				var key = getKeyFunction(currentRecord);
				TRecord newRecord;
				if (newRecordDictionary.TryGetValue(key, out newRecord))
				{
					mergeAction(currentRecord, newRecord);
					newRecordDictionary.Remove(key);
				}
				else
				{
					currentRecords.RemoveAt(i);
				}
			}

			currentRecords.AddRange(newRecordDictionary.Values);
		}

		private bool AreColumnHeadersEqual(IReadOnlyList<ColumnHeader> columnHeaders)
		{
			if (columnHeaders.Count != SessionDataGrid.Columns.Count)
			{
				return false;
			}

			for (var i = 0; i < columnHeaders.Count; i++)
			{
				var gridHeader = (ColumnHeader)SessionDataGrid.Columns[i].Header;
				if (!String.Equals(gridHeader.Name, columnHeaders[i].Name))
				{
					return false;
				}
			}

			return true;
		}

		private void SessionDataGridSortingHandler(object sender, DataGridSortingEventArgs args)
		{
			var sortDirection = args.Column.SortDirection == ListSortDirection.Ascending
				? ListSortDirection.Descending
				: ListSortDirection.Ascending;

			var providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(CurrentConnection.ProviderName);
			providerConfiguration.DatabaseMonitorConfiguration.SortMemberPath = args.Column.SortMemberPath;
			providerConfiguration.DatabaseMonitorConfiguration.SortColumnOrder = sortDirection;
		}

		private void SessionDataGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs args)
		{
			DataGridHelper.ShowLargeValueEditor(SessionDataGrid, r => ((DatabaseSession)r)?.Values);
		}

		private void WindowClosingHandler(object sender, CancelEventArgs args)
		{
			args.Cancel = true;
			Hide();

			StoreWindowConfiguration();
		}

		private void StoreWindowConfiguration()
		{
			_customProperties =
				new Dictionary<string, double>
				{
					{ SessionDataGridHeightProperty, RowDefinitionSessionDataGrid.ActualHeight }
				};

			_sessionDetailViewer?.StoreConfiguration(_customProperties);

			WorkDocumentCollection.StoreWindowProperties(this, _customProperties);
		}

		private void RefreshExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Refresh();
		}

		private void RefreshCanExecuteHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			lock (LockObject)
			{
				args.CanExecute = !_isBusy;
			}
		}

		private void DatabaseSessionFilterHandler(object sender, FilterEventArgs args)
		{
			var session = (DatabaseSession)args.Item;
			var filterSystemSession = !UserSessionOnly || session.Type == SessionType.User;
			var filterInactiveSession = !ActiveSessionOnly || session.IsActive;
			var filterChildSession = !MasterSessionOnly || session.Owner == null;
			args.Accepted = filterSystemSession && filterInactiveSession && filterChildSession;
		}

		private async void SessionDataGridSelectionChangedHandler(object sender, SelectionChangedEventArgs args)
		{
			_sessionDetailViewer.Shutdown();

			if (args.AddedItems.Count > 0)
			{
				var exception = await App.SafeActionAsync(() => _sessionDetailViewer.Initialize((DatabaseSession)args.AddedItems[0], CancellationToken.None));
				if (exception == null)
				{
					SessionDetailViewer.Content = _sessionDetailViewer.Control;
				}
				else
				{
					Messages.ShowError(exception.Message, owner: this);
				}
			}
			else
			{
				SessionDetailViewer.Content = null;
			}
		}

		private void IsVisibleChangedHandler(object sender, DependencyPropertyChangedEventArgs args)
		{
			_refreshTimer.IsEnabled = (bool)args.NewValue;

			if (_sessionDetailViewer != null)
			{
				_sessionDetailViewer.AutoRefreshEnabled = _refreshTimer.IsEnabled;
			}

			var timerState = _refreshTimer.IsEnabled ? "enabled" : "disabled";
			TraceLog.WriteLine($"Session monitor auto-refresh has been {timerState}. ");
		}

		private void SessionGridContextMenuOpeningHandler(object sender, RoutedEventArgs args)
		{
			SessionGridContextMenu.Items.Clear();

			var databaseSession = (DatabaseSession)SessionDataGrid.SelectedItem;
			if (databaseSession == null)
			{
				args.Handled = true;
			}
			else
			{
				foreach (var contextAction in _databaseMonitor.GetSessionContextActions(databaseSession))
				{
					var menuItem =
						new MenuItem
						{
							Header = contextAction.Name.Replace("_", "__"),
							Command = new ContextActionCommand(contextAction)
						};

					SessionGridContextMenu.Items.Add(menuItem);
				}
			}
		}
	}
}
