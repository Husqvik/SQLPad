using System;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad
{
	public interface IToolTipProvider
	{
		IToolTip GetToolTip(SqlDocumentRepository sqlDocumentRepository, int cursorPosition);
	}

	public interface IToolTip
	{
		Control Control { get; }

		FrameworkElement InnerContent { get; }

		event EventHandler Pin;
	}
}