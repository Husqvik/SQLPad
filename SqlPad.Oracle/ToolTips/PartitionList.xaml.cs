using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace SqlPad.Oracle.ToolTips
{
	public partial class PartitionList
	{
		public PartitionList()
		{
			InitializeComponent();
		}

		public static readonly DependencyProperty TableDetailsProperty = DependencyProperty.Register("TableDetails", typeof(TableDetailsModel), typeof(PartitionList), new FrameworkPropertyMetadata(TableDetailsPropertyChangedCallbackHandler));

		public TableDetailsModel TableDetails
		{
			get { return (TableDetailsModel)GetValue(TableDetailsProperty); }
			set { SetValue(TableDetailsProperty, value); }
		}

		private static void TableDetailsPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var partitionList = (PartitionList)dependencyObject;
			partitionList.DataContext = (TableDetailsModel)args.NewValue;
		}
	}

	public abstract class PartitionDetailsModelBase : SegmentDetailsModelBase
	{
		public string Name { get; set; }

		public string HighValue { get; set; }
	}

	public class PartitionDetailsModel : PartitionDetailsModelBase
	{
		private readonly ObservableCollection<SubPartitionDetailsModel> _subPartitionDetails = new ObservableCollection<SubPartitionDetailsModel>();

		public ICollection<SubPartitionDetailsModel> SubPartitionDetails { get { return _subPartitionDetails; } }
	}

	public class SubPartitionDetailsModel : PartitionDetailsModelBase
	{
	}
}
