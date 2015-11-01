using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
			ExecutionPlanTreeView.TreeView.Items.Clear();
		}

		private void SetRootItem(ExecutionPlanItem rootItem)
		{
			if (rootItem != null)
			{
				ExecutionPlanTreeView.TreeView.Items.Add(rootItem);
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

	internal class LastExecutionWorkAreaInfoConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var planItem = value as ExecutionStatisticsPlanItem;
			return planItem?.LastMemoryUsedBytes == null
				? String.Empty
				: $"{DataSpaceConverter.PrettyPrint(planItem.LastMemoryUsedBytes.Value)} ({planItem.LastExecutionMethod}, {planItem.WorkAreaSizingPolicy})";
		}
	}

	internal class CumulativeExecutionWorkAreaInfoConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var planItem = value as ExecutionStatisticsPlanItem;
			return planItem?.LastMemoryUsedBytes == null
				? String.Empty
				: $"{planItem.TotalWorkAreaExecutions} total/{planItem.OptimalWorkAreaExecutions} optimal/{planItem.OnePassWorkAreaExecutions} one-pass/{planItem.MultiPassWorkAreaExecutions} multi-pass";
		}
	}

	internal class TreeViewLineConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var item = (TreeViewItem)value;
			var itemsControl = ItemsControl.ItemsControlFromItemContainer(item);
			return itemsControl.ItemContainerGenerator.IndexFromContainer(item) == itemsControl.Items.Count - 1;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return false;
		}
	}
}
