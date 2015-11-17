using System;
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
		public static readonly DependencyProperty ShowSessionDetailsProperty = DependencyProperty.Register(nameof(ShowSessionDetails), typeof(bool), typeof(ExecutionPlanTreeView), new FrameworkPropertyMetadata());

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
				if (args.NewValue == null)
				{
					treeView.Items.Clear();
				}
				else
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

		public bool ShowSessionDetails
		{
			get { return (bool)GetValue(ShowSessionDetailsProperty); }
			set { SetValue(ShowSessionDetailsProperty, value); }
		}

		public ExecutionPlanTreeView()
		{
			InitializeComponent();
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

			var executionMonitorPlanItem = item as SqlMonitorPlanItem;
			if (executionMonitorPlanItem != null)
			{
				return ExecutionMonitorPlanItemTemplate;
			}

			return ExplainPlanTemplateTemplate;
		}
	}
}
