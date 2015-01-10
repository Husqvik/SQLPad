using System;
using System.Collections;
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
			var itemCollection = await _databaseModel.ExplainPlanAsync(executionModel, cancellationToken);
			if (itemCollection.RootItem != null)
			{
				Viewer.Items.Clear();
				Viewer.Items.Add(itemCollection.RootItem);
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

	public class ExecutionPlanItemCollection : ExecutionPlanItemCollectionBase<ExecutionPlanItem> { }

	public abstract class ExecutionPlanItemCollectionBase<T> : IEnumerable<T> where T : ExecutionPlanItem
	{
		private readonly Dictionary<int, T> _allItems = new Dictionary<int, T>();
		private readonly List<ExecutionPlanItem> _leafItems = new List<ExecutionPlanItem>();

		private int _currentExecutionStep;

		public T RootItem { get; private set; }

		public IReadOnlyDictionary<int, T> AllItems { get { return _allItems; } }

		public void Add(T item)
		{
			if (_currentExecutionStep > 0)
			{
				throw new InvalidOperationException("Item cannot be added because the collection is frozen. ");
			}

			if (item.ParentId.HasValue)
			{
				_allItems[item.ParentId.Value].AddChildItem(item);
			}
			else
			{
				if (_allItems.Count > 0)
				{
					throw new InvalidOperationException("Root item can be added only as the first item. ");
				}

				RootItem = item;
			}

			_allItems.Add(item.Id, item);
		}

		public void Freeze()
		{
			if (_allItems.Count == 0)
			{
				return;
			}

			_leafItems.AddRange(_allItems.Values.Where(v => v.IsLeaf));
			var startNode = _leafItems[0];
			_leafItems.RemoveAt(0);

			ResolveExecutionOrder(startNode, null);
		}

		private void ResolveExecutionOrder(ExecutionPlanItem nextItem, ExecutionPlanItem breakAtItem)
		{
			do
			{
				if (nextItem == breakAtItem)
				{
					return;
				}

				nextItem.ExecutionOrder = ++_currentExecutionStep;
				if (nextItem.Parent == null)
				{
					return;
				}

				nextItem = nextItem.Parent;
				var allChildrenExecuted = nextItem.ChildItems.Count(i => i.ExecutionOrder == 0) == 0;
				if (!allChildrenExecuted)
				{
					var otherBranchItemIndex = _leafItems.FindIndex(i => i.IsChildFrom(nextItem));
					if (otherBranchItemIndex == -1)
					{
						return;
					}

					var otherBranchLeafItem = _leafItems[otherBranchItemIndex];
					_leafItems.RemoveAt(otherBranchItemIndex);

					ResolveExecutionOrder(otherBranchLeafItem, nextItem);
				}
			} while (true);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<T> GetEnumerator()
		{
			return _allItems.Values.GetEnumerator();
		}
	}


	[DebuggerDisplay("ExecutionPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class ExecutionPlanItem
	{
		private readonly List<ExecutionPlanItem> _childItems = new List<ExecutionPlanItem>();

		public int ExecutionOrder { get; set; }
		
		public int Id { get; set; }
		
		public int? ParentId { get; set; }

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
