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
			await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, CancellationToken.None, false, activeSessionHistoryDataProvider);
		}

		public async Task Initialize(DatabaseSession databaseSession, CancellationToken cancellationToken)
		{
			Clear();

			_refreshTimer.IsEnabled = false;

			databaseSession = databaseSession.ParentSession ?? databaseSession;
			var sqlId = OracleReaderValueConvert.ToString(databaseSession.Values[18]);
			if (!String.IsNullOrEmpty(sqlId))
			{
				var sessionId = Convert.ToInt32(databaseSession.Values[2]);
				var executionId = Convert.ToInt32(databaseSession.Values[23]);
				var monitorDataProvider = new SqlMonitorDataProvider(sessionId, executionId, sqlId, Convert.ToInt32(databaseSession.Values[19]));
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
