using System.ComponentModel;
using System.Configuration;
using System.Windows;

namespace SqlPad
{
	public partial class WindowDatabaseMonitor
	{
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register(nameof(CurrentConnection), typeof(ConnectionStringSettings), typeof(WindowDatabaseMonitor), new FrameworkPropertyMetadata());

		[Bindable(true)]
		public ConnectionStringSettings CurrentConnection
		{
			get { return (ConnectionStringSettings)GetValue(CurrentConnectionProperty); }
			set { SetValue(CurrentConnectionProperty, value); }
		}

		private IDatabaseMonitor _databaseMonitor;

		public WindowDatabaseMonitor()
		{
			InitializeComponent();
		}

		private void Initialize()
		{
			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(CurrentConnection.Name);
			var infrastructureFactory = connectionConfiguration.InfrastructureFactory;
			_databaseMonitor = infrastructureFactory.CreateDatabaseMonitor(CurrentConnection);
		}

		private void Refresh()
		{
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
	}
}
