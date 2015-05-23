using System;
using System.Windows;

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
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _comment, value))
				{
					RaisePropertyChanged("CommentVisibility");
				}
			}
		}

		public Visibility CommentVisibility
		{
			get { return String.IsNullOrEmpty(_comment) ? Visibility.Collapsed : Visibility.Visible; }
		}
	}
}
