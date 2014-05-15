using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	/// <summary>
	/// Interaction logic for ToolTipObject.xaml
	/// </summary>
	public partial class ToolTipObject : IToolTip
	{
		public ToolTipObject()
		{
			InitializeComponent();
		}

		public UserControl Control { get { return this; } }
	}
}
