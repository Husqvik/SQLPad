using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
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

		public async Task<ActionResult> ExplainAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			ExecutionPlanItem rootItem = null;
			var actionResult = new ActionResult();
			var now = DateTime.Now;

			try
			{
				rootItem = await _databaseModel.ExplainPlanAsync(executionModel, cancellationToken);
			}
			catch (Exception e)
			{
				actionResult.Exception = e;
			}

			actionResult.Elapsed = DateTime.Now - now;

			if (rootItem != null)
			{
				Viewer.ItemsSource = new[] { rootItem };
			}

			return actionResult;
		}
	}

	public class ExecutionPlanItem
	{
		private readonly List<ExecutionPlanItem> _childItems = new List<ExecutionPlanItem>();
		
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

		public void AddChildItem(ExecutionPlanItem childItem)
		{
			_childItems.Add(childItem);
			childItem.Parent = this;
		}
	}

	/*class TreeViewLineConverter : IValueConverter
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
	}*/
}
