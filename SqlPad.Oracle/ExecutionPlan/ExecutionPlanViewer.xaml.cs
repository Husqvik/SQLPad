using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SqlPad.Oracle.ExecutionPlan
{
	public partial class ExecutionPlanViewer : IExecutionPlanViewer
	{
		private readonly OracleDatabaseModelBase _databaseModel;

		public Control Control { get { return this; } }

		public ExecutionPlanViewer(OracleDatabaseModelBase databaseModel)
		{
			InitializeComponent();
			
			_databaseModel = databaseModel;
		}

		public async Task ExplainAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var rootItem = await _databaseModel.ExplainPlanAsync(executionModel, cancellationToken);
			if (rootItem != null)
			{
				Viewer.Items.Clear();
				Viewer.Items.Add(rootItem);
			}
		}

		private void SaveContentAsPng(string fileName)
		{
			var content = (TreeViewItem)(Viewer.ItemContainerGenerator.ContainerFromItem(Viewer.Items[0]));
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
				Messages.ShowError(Window.GetWindow(this), e.Message);
			}
		}

		private void SaveAsPngCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = Viewer.HasItems;
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

	[DebuggerDisplay("ExecutionPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class ExecutionPlanItem
	{
		private readonly List<ExecutionPlanItem> _childItems = new List<ExecutionPlanItem>();

		public int ExecutionOrder { get; set; }
		
		public int Id { get; set; }

		public int Depth { get; set; }
		
		public bool IsLeaf { get { return _childItems.Count == 0; } }
		
		public string Operation { get; set; }
		
		public string Options { get; set; }

		public string Optimizer { get; set; }
		
		public string ObjectOwner { get; set; }
		
		public string ObjectName { get; set; }
		
		public string ObjectAlias { get; set; }
		
		public string ObjectType { get; set; }

		public long? Cost { get; set; }

		public long? Cardinality { get; set; }
		
		public long? Bytes { get; set; }
		
		public string PartitionStart { get; set; }
		
		public string PartitionStop { get; set; }
		
		public string Distribution { get; set; }
		
		public long? CpuCost { get; set; }
		
		public long? IoCost { get; set; }
		
		public long? TempSpace { get; set; }
		
		public string AccessPredicates { get; set; }
		
		public string FilterPredicates { get; set; }
		
		public TimeSpan? Time { get; set; }
		
		public string QueryBlockName { get; set; }
		
		public XElement Other { get; set; }

		public ExecutionPlanItem Parent { get; private set; }

		public IReadOnlyList<ExecutionPlanItem> ChildItems
		{
			get { return _childItems.AsReadOnly(); }
		}

		public IEnumerable<ExecutionPlanItem> AllChildItems
		{
			get { return _childItems.Concat(_childItems.SelectMany(i => i.AllChildItems)); }
		}

		public void AddChildItem(ExecutionPlanItem childItem)
		{
			_childItems.Add(childItem);
			childItem.Parent = this;
		}

		public bool IsChildFrom(ExecutionPlanItem parent)
		{
			var item = this;
			do
			{
				item = item.Parent;
			} while (item != null && item != parent);

			return item != null;
		}
	}

	class TreeViewLineConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var item = (TreeViewItem)value;
			var itemsControl = ItemsControl.ItemsControlFromItemContainer(item);
			return itemsControl.ItemContainerGenerator.IndexFromContainer(item) == itemsControl.Items.Count - 1;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return false;
		}
	}
}
