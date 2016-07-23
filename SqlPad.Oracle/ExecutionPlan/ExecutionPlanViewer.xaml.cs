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
		public static readonly DependencyProperty HasInactiveItemsProperty = DependencyProperty.Register(nameof(HasInactiveItems), typeof(bool), typeof(ExecutionPlanViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty ShowAllItemsProperty = DependencyProperty.Register(nameof(ShowAllItems), typeof(bool), typeof(ExecutionPlanViewer), new UIPropertyMetadata(true, ShowAllItemsChangedHandler));

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

		public bool HasInactiveItems
		{
			get { return (bool)GetValue(HasInactiveItemsProperty); }
			private set { SetValue(HasInactiveItemsProperty, value); }
		}

		public bool ShowAllItems
		{
			get { return (bool)GetValue(ShowAllItemsProperty); }
			set { SetValue(ShowAllItemsProperty, value); }
		}

		private static void ShowAllItemsChangedHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var executionPlanView = (ExecutionPlanViewer)dependencyObject;
			executionPlanView.ConfigureExecutionPlanItemVisibility();
		}

		private readonly OutputViewer _outputViewer;
		private IExecutionPlanItemCollection _planItemCollection;

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
				_planItemCollection = itemCollection = await ((OracleConnectionAdapterBase)connectionAdapter).GetCursorExecutionStatisticsAsync(cancellationToken);
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

			ConfigureExecutionPlanItemVisibility();

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

			ExecutionPlanItemCollection itemCollection;
			_planItemCollection = itemCollection = await ((OracleConnectionAdapterBase)_outputViewer.ConnectionAdapter).ExplainPlanAsync(executionModel, cancellationToken);
			if (itemCollection != null)
			{
				ConfigureExecutionPlanItemVisibility();
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

		private void ConfigureExecutionPlanItemVisibility()
		{
			HasInactiveItems = _planItemCollection.HasInactiveItems;

			if (ShowAllItems)
			{
				_planItemCollection.SetAllItems();
			}
			else
			{
				_planItemCollection.SetActiveItems();
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
