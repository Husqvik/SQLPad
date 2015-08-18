using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
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
			TreeView.Items.Clear();
		}

		private void SetRootItem(ExecutionPlanItem rootItem)
		{
			if (rootItem != null)
			{
				TreeView.Items.Add(rootItem);
			}
		}

		private void SaveContentAsPng(string fileName)
		{
			var content = (TreeViewItem)(TreeView.ItemContainerGenerator.ContainerFromItem(TreeView.Items[0]));
			var presentationSource = PresentationSource.FromVisual(content);
			var dpiX = 96.0 * presentationSource.CompositionTarget.TransformToDevice.M11;
			var dpiY = 96.0 * presentationSource.CompositionTarget.TransformToDevice.M22;
			var renderTarget = new RenderTargetBitmap((int)Math.Ceiling(content.RenderSize.Width), (int)Math.Ceiling(content.RenderSize.Height), dpiX, dpiY, PixelFormats.Pbgra32);

			content.Measure(content.RenderSize);
			content.Arrange(new Rect(content.RenderSize));

			renderTarget.Render(content);

			var encoder = new PngBitmapEncoder();
			var bitmapFrame = BitmapFrame.Create(renderTarget);
			encoder.Frames.Add(bitmapFrame);

			try
			{
				using (var stream = File.Create(fileName))
				{
					encoder.Save(stream);
				}
			}
			catch (Exception e)
			{
				Messages.ShowError(e.Message, owner: App.MainWindow);
			}
		}

		private void SaveAsPngCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = TreeView.HasItems;
		}

		private void SaveAsPngCanExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new SaveFileDialog { Filter = "PNG files (*.png)|*.png|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			SaveContentAsPng(dialog.FileName);
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

	internal class PlanItemTemplateSelector : DataTemplateSelector
	{
		public HierarchicalDataTemplate ExplainPlanTemplateTemplate { get; set; }

		public HierarchicalDataTemplate ExecutionStatisticsPlanItemTemplate { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var executionStatisticsPlanItem = item as ExecutionStatisticsPlanItem;
			return executionStatisticsPlanItem == null ? ExplainPlanTemplateTemplate : ExecutionStatisticsPlanItemTemplate;
		}
	}
}
