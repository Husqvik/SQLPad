using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SqlPad
{
	public partial class WindowDatabaseMonitor
	{
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register(nameof(CurrentConnection), typeof(ConnectionStringSettings), typeof(WindowDatabaseMonitor), new FrameworkPropertyMetadata(CurrentConnectionPropertyChangedCallbackHandler));

		private static void CurrentConnectionPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var monitorWindow = (WindowDatabaseMonitor)dependencyObject;
			monitorWindow.Initialize();
		}

		[Bindable(true)]
		public ConnectionStringSettings CurrentConnection
		{
			get { return (ConnectionStringSettings)GetValue(CurrentConnectionProperty); }
			set { SetValue(CurrentConnectionProperty, value); }
		}

		private readonly ObservableCollection<object[]> _databaseSessions = new ObservableCollection<object[]>();

		private bool _isBusy;
		private IDatabaseMonitor _databaseMonitor;

		public IReadOnlyList<object[]> DatabaseSessions => _databaseSessions;

		public WindowDatabaseMonitor()
		{
			InitializeComponent();
		}

		private void Initialize()
		{
			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(CurrentConnection.Name);
			var infrastructureFactory = connectionConfiguration.InfrastructureFactory;
			_databaseMonitor = infrastructureFactory.CreateDatabaseMonitor(CurrentConnection);

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

			var sessions = task.Result;
            DataGridHelper.InitializeDataGridColumns(SessionDataGrid, sessions.ColumnHeaders, null, null);
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
	}
}
