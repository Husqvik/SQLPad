using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipSequence : IToolTip
	{
		public ToolTipSequence(string title, OracleSequence sequence)
		{
			InitializeComponent();

			LabelTitle.Text = title;

			DataContext = sequence;
		}

		public UserControl Control { get { return this; } }
	}
}
