using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipSequence
	{
		public ToolTipSequence(string title, OracleSequence sequence)
		{
			InitializeComponent();

			LabelTitle.Text = title;

			DataContext = sequence;
		}
	}
}
