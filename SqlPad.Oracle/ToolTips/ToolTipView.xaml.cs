using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipView
	{
		public ToolTipView()
		{
			InitializeComponent();
		}

		protected override Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			return ScriptExtractor.ExtractSchemaObjectScriptAsync(((ViewDetailsModel)DataContext).View, cancellationToken);
		}
	}

	public class ViewDetailsModel : ModelWithConstraints, IModelWithComment
	{
		private string _comment;

		public OracleView View { get; set; }

		public string Title { get; set; }

		public string Comment
		{
			get { return _comment; }
			set { UpdateValueAndRaisePropertyChanged(ref _comment, value); }
		}
	}
}
