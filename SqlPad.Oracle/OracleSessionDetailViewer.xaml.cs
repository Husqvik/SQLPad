using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle
{
	public partial class OracleSessionDetailViewer : IDatabaseSessionDetailViewer
	{
		public static readonly DependencyProperty EnableSessionDetailsProperty = DependencyProperty.Register(nameof(EnableSessionDetails), typeof(bool), typeof(ExecutionPlanTreeView), new FrameworkPropertyMetadata());

		public bool EnableSessionDetails
		{
			get { return (bool)GetValue(EnableSessionDetailsProperty); }
			private set { SetValue(EnableSessionDetailsProperty, value); }
		}

		private readonly ConnectionStringSettings _connectionString;
		private readonly DispatcherTimer _refreshTimer;

		private SqlMonitorPlanItemCollection _planItemCollection;

		public Control Control => this;

		public OracleSessionDetailViewer(ConnectionStringSettings connectionString)
		{
			InitializeComponent();

			_connectionString = connectionString;

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
			_refreshTimer.Tick += async delegate { await RefreshSessionDetails(); };
		}

		private async Task RefreshSessionDetails()
		{
			var activeSessionHistoryDataProvider = new SqlMonitorActiveSessionHistoryDataProvider(_planItemCollection);
			var planMonitorDataProvider = new SqlMonitorPlanMonitorDataProvider(_planItemCollection);
			var sessionLongOperationDataProvider = new SessionLongOperationPlanMonitorDataProvider(_planItemCollection);
			await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, CancellationToken.None, false, activeSessionHistoryDataProvider, planMonitorDataProvider, sessionLongOperationDataProvider);

			EnableSessionDetails = _planItemCollection.AllItems.Values.Any(i => i.SessionItems.Count > 1);
		}

		public async Task Initialize(DatabaseSession databaseSession, CancellationToken cancellationToken)
		{
			Clear();

			_refreshTimer.IsEnabled = false;

			databaseSession = databaseSession.ParentSession ?? databaseSession;
			var sqlId = OracleReaderValueConvert.ToString(databaseSession.Values[18]);
			var executionId = OracleReaderValueConvert.ToInt32(databaseSession.Values[21]);
			if (!String.IsNullOrEmpty(sqlId) && executionId.HasValue)
			{
				var sessionId = Convert.ToInt32(databaseSession.Values[1]);
				var executionStart = (DateTime)databaseSession.Values[20];
				var monitorDataProvider = new SqlMonitorDataProvider(sessionId, executionStart, executionId.Value, sqlId, Convert.ToInt32(databaseSession.Values[19]));
				await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, cancellationToken, false, monitorDataProvider);

				_planItemCollection = monitorDataProvider.ItemCollection;

				if (_planItemCollection.RootItem != null)
				{
					ExecutionPlanTreeView.RootItem = _planItemCollection.RootItem;
					await RefreshSessionDetails();
					_refreshTimer.IsEnabled = true;
				}
			}
		}

		private void Clear()
		{
			ExecutionPlanTreeView.RootItem = null;
		}
	}
}
