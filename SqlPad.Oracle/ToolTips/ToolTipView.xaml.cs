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
			return ScriptExtractor.ExtractSchemaObjectScriptAsync(((ObjectDetailsModel)DataContext).Object, cancellationToken);
		}
	}

	public class ObjectDetailsModel : ModelWithConstraints, IModelWithComment
	{
		private string _comment;

		public OracleObject Object { get; set; }

		public string Title { get; set; }

		public string Comment
		{
			get { return _comment; }
			set { UpdateValueAndRaisePropertyChanged(ref _comment, value); }
		}
	}
}
