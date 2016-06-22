using System;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace SqlPad
{
	public class SearchTextBox : WatermarkTextBox
	{
		private static readonly RoutedCommand ClearPhraseCommand = new RoutedCommand("ClearPhrase", typeof(SearchTextBox), new InputGestureCollection { new KeyGesture(Key.Escape) });

		public SearchTextBox()
		{
			SqlPadTextBox.ConfigureCommands(this);

			CommandBindings.Add(new CommandBinding(ClearPhraseCommand, (s, args) => Text = String.Empty));
		}
	}
}