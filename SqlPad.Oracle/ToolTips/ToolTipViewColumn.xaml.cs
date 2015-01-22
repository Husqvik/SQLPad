using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipViewColumn : IToolTip
	{
		public ToolTipViewColumn(ColumnDetailsModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
		}

		public UserControl Control { get { return this; } }
	}
}
