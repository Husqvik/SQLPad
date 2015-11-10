using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle
{
	public partial class OracleSessionDetailViewer : IDatabaseSessionDetailViewer
	{
		private readonly ConnectionStringSettings _connectionString;
		private readonly DispatcherTimer _refreshTimer;

		private SqlMonitorPlanItemCollection _planItemCollection;

		public Control Control => this;

		public OracleSessionDetailViewer(ConnectionStringSettings connectionString)
		{
			InitializeComponent();

			_connectionString = connectionString;

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
			_refreshTimer.Tick += async delegate { await RefreshActiveSessionHistory(); };
		}

		private async Task RefreshActiveSessionHistory()
		{
			var activeSessionHistoryDataProvider = new SqlMonitorActiveSessionHistoryDataProvider(_planItemCollection);
			var planMonitorDataProvider = new SqlMonitorPlanMonitorDataProvider(_planItemCollection);
			var sessionLongOperationDataProvider = new SessionLongOperationPlanMonitorDataProvider(_planItemCollection);
			await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, CancellationToken.None, false, activeSessionHistoryDataProvider, planMonitorDataProvider, sessionLongOperationDataProvider);
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
					ExecutionPlanTreeView.TreeView.Items.Add(_planItemCollection.RootItem);
					await RefreshActiveSessionHistory();
					_refreshTimer.IsEnabled = true;
				}
			}
		}

		private void Clear()
		{
			ExecutionPlanTreeView.TreeView.Items.Clear();
		}
	}
}
