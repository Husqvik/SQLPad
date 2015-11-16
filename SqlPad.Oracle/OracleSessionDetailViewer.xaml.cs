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

		private static readonly object LockObject = new object();

		private readonly ConnectionStringSettings _connectionString;
		private readonly DispatcherTimer _refreshTimer;

		private bool _isBusy;
		private SqlMonitorPlanItemCollection _planItemCollection;

		public Control Control => this;

		public OracleSessionDetailViewer(ConnectionStringSettings connectionString)
		{
			InitializeComponent();

			_connectionString = connectionString;

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(10) };
			_refreshTimer.Tick += async delegate { await Refresh(CancellationToken.None); };
		}

		public async Task Refresh(CancellationToken cancellationToken)
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

			try
			{
				var activeSessionHistoryDataProvider = new SqlMonitorActiveSessionHistoryDataProvider(_planItemCollection);
				var planMonitorDataProvider = new SqlMonitorPlanMonitorDataProvider(_planItemCollection);
				var sessionLongOperationDataProvider = new SessionLongOperationPlanMonitorDataProvider(_planItemCollection);
				await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, cancellationToken, false, activeSessionHistoryDataProvider, planMonitorDataProvider, sessionLongOperationDataProvider);

				EnableSessionDetails = _planItemCollection.AllItems.Values.Any(i => i.SessionItems.Count > 1);
			}
			finally
			{
				_isBusy = false;
			}
		}

		public async Task Initialize(DatabaseSession databaseSession, CancellationToken cancellationToken)
		{
			Clear();

			_refreshTimer.IsEnabled = false;

			try
			{
				databaseSession = databaseSession.ParentSession ?? databaseSession;
				var oracleSession = (OracleSessionValues)databaseSession.ProviderValues;
				if (!String.IsNullOrEmpty(oracleSession.SqlId) && oracleSession.ExecutionId.HasValue)
				{
					var monitorDataProvider = new SqlMonitorDataProvider(oracleSession.Id, oracleSession.ExecutionStart.Value, oracleSession.ExecutionId.Value, oracleSession.SqlId, oracleSession.ChildNumber.Value);
					await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, cancellationToken, false, monitorDataProvider);

					_planItemCollection = monitorDataProvider.ItemCollection;

					if (_planItemCollection.RootItem != null)
					{
						ExecutionPlanTreeView.RootItem = _planItemCollection.RootItem;
						await Refresh(cancellationToken);
					}
				}
			}
			finally
			{
				_refreshTimer.IsEnabled = true;
			}
		}

		private void Clear()
		{
			ExecutionPlanTreeView.RootItem = null;
		}
	}
}
