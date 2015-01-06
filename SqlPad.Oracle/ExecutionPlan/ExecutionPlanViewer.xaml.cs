using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;

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
