using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	/// <summary>
	/// Interaction logic for ToolTipColumn.xaml
	/// </summary>
	public partial class ToolTipColumn : IToolTip
	{
		public ToolTipColumn()
		{
			InitializeComponent();
		}

		public UserControl Control { get { return this; } }
	}
}
