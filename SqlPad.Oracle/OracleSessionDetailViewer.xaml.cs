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
		private static readonly TimeSpan DefaultRefreshPeriod = TimeSpan.FromSeconds(10);

		private readonly ConnectionStringSettings _connectionString;
		private readonly DispatcherTimer _refreshTimer;

		private bool _isBusy;
		private SqlMonitorPlanItemCollection _planItemCollection;
		private OracleSessionValues _oracleSessionValues;

		public Control Control => this;

		public DatabaseSession DatabaseSession { get; private set; }

		public OracleSessionDetailViewer(ConnectionStringSettings connectionString)
		{
			InitializeComponent();

			_connectionString = connectionString;

			_refreshTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = DefaultRefreshPeriod };
			_refreshTimer.Tick += RefreshTimerTickHandler;
		}

		private async void RefreshTimerTickHandler(object sender, EventArgs eventArgs)
		{
			var exception = await App.SafeActionAsync(() => Refresh(CancellationToken.None));
			if (exception != null)
			{
				Messages.ShowError(exception.Message, owner: Window.GetWindow(this));
			}
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
				await OracleDatabaseModel.UpdateModelAsync(OracleConnectionStringRepository.GetBackgroundConnectionString(_connectionString.ConnectionString), null, cancellationToken, false, sessionMonitorDataProvider, activeSessionHistoryDataProvider, planMonitorDataProvider, sessionLongOperationDataProvider);
				if (planItemCollection != _planItemCollection)
				{
					return;
				}

				EnableSessionDetails = planItemCollection.AllItems.Values.Any(i => i.ParallelSlaveSessionItems.Count > 0);
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
			if (_oracleSessionValues != null && _oracleSessionValues.Id == oracleSessionValues.Id &&  _oracleSessionValues.SqlId == oracleSessionValues.SqlId && _oracleSessionValues.ExecutionId == oracleSessionValues.ExecutionId)
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
					var monitorDataProvider = new SqlMonitorDataProvider(_oracleSessionValues.Id, _oracleSessionValues.ExecutionStart.Value, _oracleSessionValues.ExecutionId.Value, _oracleSessionValues.SqlId, _oracleSessionValues.ChildNumber.Value);
					await OracleDatabaseModel.UpdateModelAsync(OracleConnectionStringRepository.GetBackgroundConnectionString(_connectionString.ConnectionString), null, cancellationToken, false, monitorDataProvider);

					_planItemCollection = monitorDataProvider.ItemCollection;
					_planItemCollection.RefreshPeriod = DefaultRefreshPeriod;

					if (_planItemCollection.RootItem != null)
					{
						ExecutionPlanTreeView.RootItem = _planItemCollection.RootItem;
					}
				}
			}
			finally
			{
				_refreshTimer.IsEnabled = true;
			}
		}

		public void Shutdown()
		{
			if (DatabaseSession != null)
			{
				DatabaseSession = null;
				_oracleSessionValues = null;
			}

			_refreshTimer.IsEnabled = false;
			_planItemCollection = null;
			ExecutionPlanTreeView.RootItem = null;
		}
	}
}
