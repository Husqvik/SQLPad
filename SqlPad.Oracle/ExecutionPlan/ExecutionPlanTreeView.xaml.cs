using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle.ExecutionPlan
{
	public partial class ExecutionPlanTreeView
	{
		public static readonly DependencyProperty RootItemProperty = DependencyProperty.Register(nameof(RootItem), typeof(ExecutionPlanItem), typeof(ExecutionPlanTreeView), new FrameworkPropertyMetadata(RootItemChangedHandler));
		public static readonly DependencyProperty ShowCumulativeExecutionsProperty = DependencyProperty.Register(nameof(ShowCumulativeExecutions), typeof(bool), typeof(ExecutionPlanTreeView), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty IsDeferredScrollingEnabledProperty = DependencyProperty.Register(nameof(IsDeferredScrollingEnabled), typeof(bool), typeof(ExecutionPlanTreeView), new FrameworkPropertyMetadata(true));

		public ExecutionPlanItem RootItem
		{
			get { return (ExecutionPlanItem)GetValue(RootItemProperty); }
			set { SetValue(RootItemProperty, value); }
		}

		private static void RootItemChangedHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var treeView = ((ExecutionPlanTreeView)dependencyObject).TreeView;
			lock (treeView)
			{
				treeView.Items.Clear();

				if (args.NewValue != null)
				{
					treeView.Items.Add(args.NewValue);
				}
			}
		}

		public bool ShowCumulativeExecutions
		{
			get { return (bool)GetValue(ShowCumulativeExecutionsProperty); }
			set { SetValue(ShowCumulativeExecutionsProperty, value); }
		}

		public bool IsDeferredScrollingEnabled
		{
			get { return (bool)GetValue(IsDeferredScrollingEnabledProperty); }
			set { SetValue(IsDeferredScrollingEnabledProperty, value); }
		}

		public ExecutionPlanTreeView()
		{
			InitializeComponent();
		}

		private void SaveAsPngCanExecuteHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = TreeView.HasItems;
		}

		private void SaveAsPngCanExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "PNG files (*.png)|*.png|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			SaveContentAsPng(dialog.FileName);
		}

		private void SaveContentAsPng(string fileName)
		{
			var content = (TreeViewItem)TreeView.ItemContainerGenerator.ContainerFromItem(TreeView.Items[0]);
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

		private void TreeViewItemMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs args)
		{
			var item = (TreeViewItem)sender;
			if (item.IsSelected && (Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Control))
			{
				item.IsSelected = false;
			}
		}

		private void TerminateBubbleEventHandler(object sender, RoutedEventArgs args)
		{
			args.Handled = true;
		}
	}

	internal class PlanItemTemplateSelector : DataTemplateSelector
	{
		public HierarchicalDataTemplate ExplainPlanTemplateTemplate { get; set; }

		public HierarchicalDataTemplate ExecutionStatisticsPlanItemTemplate { get; set; }

		public HierarchicalDataTemplate ExecutionMonitorPlanItemTemplate { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var executionStatisticsPlanItem = item as ExecutionStatisticsPlanItem;
			if (executionStatisticsPlanItem != null)
			{
				return ExecutionStatisticsPlanItemTemplate;
			}

			return item is SqlMonitorPlanItem
				? ExecutionMonitorPlanItemTemplate
				: ExplainPlanTemplateTemplate;
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

	internal class TreeViewLineConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var item = (TreeViewItem)value;
			var itemsControl = ItemsControl.ItemsControlFromItemContainer(item);
			return itemsControl.ItemContainerGenerator.IndexFromContainer(item) == itemsControl.Items.Count - 1;
		}
	}
}
