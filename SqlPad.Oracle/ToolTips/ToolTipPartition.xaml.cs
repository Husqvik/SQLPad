namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipPartition
	{
		public ToolTipPartition(PartitionDetailsModelBase dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
		}
	}
}
