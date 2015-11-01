using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle
{
	public partial class OracleSessionDetailViewer : IDatabaseSessionDetailViewer
	{
		private readonly ConnectionStringSettings _connectionString;
		public Control Control => this;

		public OracleSessionDetailViewer(ConnectionStringSettings connectionString)
		{
			InitializeComponent();

			_connectionString = connectionString;
		}

		public async Task Initialize(DatabaseSession databaseSession, CancellationToken cancellationToken)
		{
			Clear();

			databaseSession = databaseSession.ParentSession ?? databaseSession;
			var executionId = OracleReaderValueConvert.ToInt32(databaseSession.Values[21]);
			if (executionId.HasValue)
			{
				var monitorDataProvider = new SqlMonitorDataProvider(Convert.ToInt32(databaseSession.Values[1]), executionId.Value, (string)databaseSession.Values[18]);
				await OracleDatabaseModel.UpdateModelAsync(_connectionString.ConnectionString, null, cancellationToken, false, monitorDataProvider);
				if (monitorDataProvider.ItemCollection.RootItem != null)
				{
					ExecutionPlanTreeView.TreeView.Items.Add(monitorDataProvider.ItemCollection.RootItem);
				}
			}
		}

		private void Clear()
		{
			ExecutionPlanTreeView.TreeView.Items.Clear();
		}
	}
}
