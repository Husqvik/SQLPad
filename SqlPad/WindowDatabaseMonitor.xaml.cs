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

namespace SqlPad
{
	public partial class WindowDatabaseMonitor
	{
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register(nameof(CurrentConnection), typeof(ConnectionStringSettings), typeof(WindowDatabaseMonitor), new FrameworkPropertyMetadata(CurrentConnectionChangedCallbackHandler));
		public static readonly DependencyProperty UserSessionOnlyProperty = DependencyProperty.Register(nameof(UserSessionOnly), typeof(bool), typeof(WindowDatabaseMonitor), new UIPropertyMetadata(true, SessionFilterChangedCallbackHandler));
		public static readonly DependencyProperty ActiveSessionOnlyProperty = DependencyProperty.Register(nameof(ActiveSessionOnly), typeof(bool), typeof(WindowDatabaseMonitor), new UIPropertyMetadata(true, SessionFilterChangedCallbackHandler));

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

		private static void SessionFilterChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var viewSource = (CollectionViewSource)((WindowDatabaseMonitor)dependencyObject).Resources["FilteredDatabaseSessions"];
			viewSource.View.Refresh();
		}

		[Bindable(true)]
		public bool ActiveSessionOnly
		{
			get { return (bool)GetValue(ActiveSessionOnlyProperty); }
			set { SetValue(ActiveSessionOnlyProperty, value); }
		}

		private const string SessionDataGridHeightProperty = "SessionDataGridSplitter";

		private static readonly object LockObject = new object();

		private readonly ObservableCollection<DatabaseSession> _databaseSessions = new ObservableCollection<DatabaseSession>();
		private readonly DispatcherTimer _refreshTimer;

		private bool _isBusy;
		private IDatabaseMonitor _databaseMonitor;
		private IDatabaseSessionDetailViewer _sessionDetailViewer;

		public IReadOnlyList<DatabaseSession> DatabaseSessions => _databaseSessions;

		private EventHandler<DataGridBeginningEditEventArgs> DataGridBeginningEditCancelTextInputHandler => App.ResultGridBeginningEditCancelTextInputHandlerImplementation;

		public WindowDatabaseMonitor()
		{
			InitializeComponent();

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
			_refreshTimer.Tick += RefreshTimerTickHandler;
		}

		private void RefreshTimerTickHandler(object sender, EventArgs e)
		{
			_refreshTimer.IsEnabled = false;
			Refresh();
			_refreshTimer.IsEnabled = true;
		}

		public void RestoreAppearance()
		{
			var customProperties = WorkDocumentCollection.RestoreWindowProperties(this);
			double sessionDataGridHeight = 0;
			if (customProperties?.TryGetValue(SessionDataGridHeightProperty, out sessionDataGridHeight) == true)
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

			Task<DatabaseSessions> task = null;
			var exception = await App.SafeActionAsync(() => task = _databaseMonitor.GetAllSessionDataAsync(CancellationToken.None));
			_isBusy = false;

			if (exception != null)
			{
				Messages.ShowError(exception.Message, owner: this);
				return;
			}

			var sessions = task.Result;

			if (!AreColumnHeadersEqual(sessions.ColumnHeaders))
			{
				_databaseSessions.Clear();
				SessionDataGrid.Columns.Clear();
				foreach (var columnHeader in sessions.ColumnHeaders)
				{
					var column =
						new DataGridTextColumn
						{
							Binding = new Binding($"{nameof(DatabaseSession.Values)}[{columnHeader.ColumnIndex}]") { Converter = CellValueConverter.Instance },
							EditingElementStyle = (Style)Application.Current.Resources["CellTextBoxStyleReadOnly"]
						};

					DataGridHelper.ApplyColumnStyle(column, columnHeader);
					SessionDataGrid.Columns.Add(column);
				}
			}

			MergeRecords(_databaseSessions, sessions.Rows, r => r.Id, MergeSessionRecordData);
		}

		private static void MergeSessionRecordData(DatabaseSession current, DatabaseSession @new)
		{
			current.IsActive = @new.IsActive;
			current.Type = @new.Type;
			current.Values = @new.Values;
		}

		public void MergeRecords<TRecord>(IList<TRecord> currentRecords, IEnumerable<TRecord> newRecords, Func<TRecord, object> getKeyFunction, Action<TRecord, TRecord> mergeAction)
		{
			var newRecordDictionary = newRecords.ToDictionary(getKeyFunction);

			var currentKeys = new HashSet<object>();
			for (var i = 0; i < currentRecords.Count; i++)
			{
				var currentRecord = currentRecords[i];
				var key = getKeyFunction(currentRecord);
				TRecord newRecord;
				currentKeys.Add(key);
				if (newRecordDictionary.TryGetValue(key, out newRecord))
				{
					mergeAction(currentRecord, newRecord);
				}
				else
				{
					currentRecords.RemoveAt(i);
				}
			}

			currentRecords.AddRange(newRecordDictionary.Values.Where(r => !currentKeys.Contains(getKeyFunction(r))));
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

		private void ColumnHeaderMouseClickHandler(object sender, RoutedEventArgs e)
		{
			
		}

		private void WindowClosingHandler(object sender, CancelEventArgs e)
		{
			e.Cancel = true;
			Hide();
			WorkDocumentCollection.StoreWindowProperties(this, new Dictionary<string, double> { { SessionDataGridHeightProperty, RowDefinitionSessionDataGrid.ActualHeight } });
		}

		private void RefreshExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Refresh();
		}

		private void RefreshCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			lock (LockObject)
			{
				e.CanExecute = !_isBusy;
			}
		}

		private void DatabaseSessionFilterHandler(object sender, FilterEventArgs e)
		{
			var filterSystemSession = !UserSessionOnly || ((DatabaseSession)e.Item).Type == SessionType.User;
			var filterInactiveSession = !ActiveSessionOnly || ((DatabaseSession)e.Item).IsActive;
			e.Accepted = filterSystemSession && filterInactiveSession;
		}

		private async void SessionDataGridSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count > 0)
			{
				var exception = await App.SafeActionAsync(() => _sessionDetailViewer.Initialize((DatabaseSession)e.AddedItems[0], CancellationToken.None));
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
			var timerState = _refreshTimer.IsEnabled ? "enabled" : "disabled";
			Trace.WriteLine($"Session monitor auto-refresh has been {timerState}. ");
		}
	}
}
