using System.Threading;
using System.Threading.Tasks;
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

		protected override Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			return ScriptExtractor.ExtractSchemaObjectScriptAsync((OracleSequence)DataContext, cancellationToken);
		}
	}
}
