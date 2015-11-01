using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlPad
{
	public partial class WindowDatabaseMonitor
	{
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register(nameof(CurrentConnection), typeof(ConnectionStringSettings), typeof(WindowDatabaseMonitor), new FrameworkPropertyMetadata(CurrentConnectionChangedCallbackHandler));
		public static readonly DependencyProperty UserSessionOnlyProperty = DependencyProperty.Register(nameof(UserSessionOnly), typeof(bool), typeof(WindowDatabaseMonitor), new UIPropertyMetadata(true, UserSessionOnlyChangedCallbackHandler));

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
			var viewSource = (CollectionViewSource)((WindowDatabaseMonitor)dependencyObject).Resources["FilteredDatabaseSessions"];
			viewSource.View.Refresh();
		}

		private readonly ObservableCollection<DatabaseSession> _databaseSessions = new ObservableCollection<DatabaseSession>();

		private bool _isBusy;
		private IDatabaseMonitor _databaseMonitor;
		private IDatabaseSessionDetailViewer _sessionDetailViewer;

		public IReadOnlyList<DatabaseSession> DatabaseSessions => _databaseSessions;

		private EventHandler<DataGridBeginningEditEventArgs> DataGridBeginningEditCancelTextInputHandler => App.ResultGridBeginningEditCancelTextInputHandlerImplementation;

		public WindowDatabaseMonitor()
		{
			InitializeComponent();
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
			_isBusy = true;
			_databaseSessions.Clear();

			Task<DatabaseSessions> task = null;
			var exception = await App.SafeActionAsync(() => task = _databaseMonitor.GetAllSessionDataAsync(CancellationToken.None));
			_isBusy = false;

			if (exception != null)
			{
				Messages.ShowError(exception.Message, owner: this);
				return;
			}

			SessionDataGrid.Columns.Clear();
			var sessions = task.Result;
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

			_databaseSessions.AddRange(sessions.Rows);
		}

		private void ColumnHeaderMouseClickHandler(object sender, RoutedEventArgs e)
		{
			
		}

		private void WindowClosingHandler(object sender, CancelEventArgs e)
		{
			e.Cancel = true;
			Hide();
			WorkDocumentCollection.StoreWindowProperties(this);
		}

		private void RefreshExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Refresh();
		}

		private void RefreshCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = !_isBusy;
		}

		private void DatabaseSessionFilterHandler(object sender, FilterEventArgs e)
		{
			e.Accepted = !UserSessionOnly || ((DatabaseSession)e.Item).Type == SessionType.User;
		}

		private void SessionDataGridSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			SessionDetailViewer.Content = e.AddedItems.Count > 0
				?_sessionDetailViewer.Control
				: null;
		}
	}
}
