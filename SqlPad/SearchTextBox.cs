using System;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace SqlPad
{
	public class SearchTextBox : WatermarkTextBox
	{
		public SearchTextBox()
		{
			KeyDown += KeyDownHandler;
		}

		private void KeyDownHandler(object sender, KeyEventArgs keyEventArgs)
		{
			if (keyEventArgs.Key == Key.Escape)
			{
				Text = String.Empty;
			}
		}
	}
}