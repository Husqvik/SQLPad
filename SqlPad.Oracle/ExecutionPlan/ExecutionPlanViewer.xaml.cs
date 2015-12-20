using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.ExecutionPlan
{
	public partial class ExecutionPlanViewer : IExecutionPlanViewer
	{
		public static readonly DependencyProperty TotalExecutionsProperty = DependencyProperty.Register(nameof(TotalExecutions), typeof(int?), typeof(ExecutionPlanViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty TextExecutionPlanProperty = DependencyProperty.Register(nameof(TextExecutionPlan), typeof(string), typeof(ExecutionPlanViewer), new FrameworkPropertyMetadata());

		public int? TotalExecutions
		{
			get { return (int?)GetValue(TotalExecutionsProperty); }
			private set { SetValue(TotalExecutionsProperty, value); }
		}

		public string TextExecutionPlan
		{
			get { return (string)GetValue(TextExecutionPlanProperty); }
			private set { SetValue(TextExecutionPlanProperty, value); }
		}

		private readonly OutputViewer _outputViewer;

		public Control Control => this;

		public ItemCollection PlanItemCollection => ExecutionPlanTreeView?.TreeView.Items;

		public ExecutionPlanViewer(OutputViewer outputViewer)
		{
			InitializeComponent();

			_outputViewer = outputViewer;
		}

		public async Task ShowActualAsync(IConnectionAdapter connectionAdapter, CancellationToken cancellationToken)
		{
			ResetView();

			ExecutionStatisticsPlanItemCollection itemCollection = null;
			
			try
			{
				itemCollection = await ((OracleConnectionAdapterBase)connectionAdapter).GetCursorExecutionStatisticsAsync(cancellationToken);
			}
			catch (Exception exception)
			{
				var errorMessage = $"Execution statistics cannot be retrieved: {Messages.GetExceptionErrorMessage(exception)}";
				_outputViewer.AddExecutionLog(DateTime.Now, errorMessage);
				Messages.ShowError(errorMessage);
			}

			if (itemCollection == null)
			{
				return;
			}
			
			SetRootItem(itemCollection.RootItem);
			TextExecutionPlan = itemCollection.PlanText;

			if (itemCollection.RootItem == null)
			{
				TabPlainText.IsSelected = true;
			}
			else
			{
				TotalExecutions = itemCollection.RootItem.Executions;
			}
		}

		public async Task ExplainAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			ResetView();

			var databaseModel = (OracleDatabaseModelBase)_outputViewer.DocumentPage.DatabaseModel;
			var itemCollection = await databaseModel.ExplainPlanAsync(executionModel, cancellationToken);

			if (itemCollection != null)
			{
				SetRootItem(itemCollection.RootItem);
				TabTreeView.IsSelected = true;
			}
		}

		private void ResetView()
		{
			TotalExecutions = null;
			TextExecutionPlan = null;
			ExecutionPlanTreeView.RootItem = null;
		}

		private void SetRootItem(ExecutionPlanItem rootItem)
		{
			if (rootItem != null)
			{
				ExecutionPlanTreeView.RootItem = rootItem;
			}
		}

		private void ShowLastOrAllExecutionCheckedHandler(object sender, RoutedEventArgs e)
		{
			if (IsInitialized)
			{
				ExecutionPlanTreeView.ShowCumulativeExecutions = ShowCumulative.IsChecked == true;
			}
		}
	}
}
