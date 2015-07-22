using System;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipObject : IToolTip
	{
		public event EventHandler Pin;

		public ToolTipObject()
		{
			InitializeComponent();
		}

		public Control Control => this;

	    public FrameworkElement InnerContent => this;
	}
}
