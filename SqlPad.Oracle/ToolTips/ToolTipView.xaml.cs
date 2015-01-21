using System;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipView : IToolTip
	{
		public ToolTipView()
		{
			InitializeComponent();
		}

		public UserControl Control { get { return this; } }
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
