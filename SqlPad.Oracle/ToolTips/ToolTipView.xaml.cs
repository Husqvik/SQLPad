namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipView
	{
		public ToolTipView()
		{
			InitializeComponent();
		}
	}

	public class ViewDetailsModel : ModelWithConstraints, IModelWithComment
	{
		private string _comment;

		public string Title { get; set; }

		public string Comment
		{
			get { return _comment; }
			set { UpdateValueAndRaisePropertyChanged(ref _comment, value); }
		}
	}
}
