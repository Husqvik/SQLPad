using System.Windows.Controls;

namespace SqlPad.Oracle
{
	public partial class OracleSessionDetailViewer : IDatabaseSessionDetailViewer
	{
		public Control Control => this;

		public OracleSessionDetailViewer()
		{
			InitializeComponent();
		}
	}
}
