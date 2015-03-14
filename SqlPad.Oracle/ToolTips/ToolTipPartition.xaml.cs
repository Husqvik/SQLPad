using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipPartition : IToolTip
	{
		public ToolTipPartition(PartitionDetailsModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
		}

		public UserControl Control { get { return this; } }
	}
}
