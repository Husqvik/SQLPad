using System.ComponentModel;
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

		[Bindable(true)]
		public TableDetailsModel TableDetails
		{
			get { return (TableDetailsModel)GetValue(TableDetailsProperty); }
			set { SetValue(TableDetailsProperty, value); }
		}

		private static void TableDetailsPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var partitionList = (PartitionList)dependencyObject;
			partitionList.DataContext = args.NewValue;
		}
	}
}
