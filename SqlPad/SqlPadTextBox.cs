using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad
{
	public class SqlPadTextBox : TextBox
	{
		public static readonly InputGestureCollection MakeLowerCaseDefaultGestures = new InputGestureCollection { new KeyGesture(Key.U, ModifierKeys.Control) };
		public static readonly InputGestureCollection MakeUpperCaseDefaultGestures = new InputGestureCollection { new KeyGesture(Key.U, ModifierKeys.Control | ModifierKeys.Shift) };

		private static readonly RoutedCommand MakeLowerCaseCommand = new RoutedCommand("MakeLowerCase", typeof(SqlPadTextBox), MakeLowerCaseDefaultGestures);
		private static readonly RoutedCommand MakeUpperCaseCommand = new RoutedCommand("MakeUpperCase", typeof(SqlPadTextBox), MakeUpperCaseDefaultGestures);

		private string Replacement => Text.Substring(SelectionStart, SelectionLength);

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			CommandBindings.Add(new CommandBinding(MakeLowerCaseCommand, LowerCaseExecuted, CanExecuteModifyCase));
			CommandBindings.Add(new CommandBinding(MakeUpperCaseCommand, UpperCaseExecuted, CanExecuteModifyCase));
		}

		private void CanExecuteModifyCase(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = !IsReadOnly && SelectionLength > 0;
		}

		private void LowerCaseExecuted(object sender, ExecutedRoutedEventArgs args)
		{
			var replacement = Replacement.ToLower(CultureInfo.CurrentUICulture);
			ReplaceSelectedText(replacement);
		}

		private void UpperCaseExecuted(object sender, ExecutedRoutedEventArgs args)
		{
			var replacement = Replacement.ToUpper(CultureInfo.CurrentUICulture);
			ReplaceSelectedText(replacement);
		}

		private void ReplaceSelectedText(string replacement)
		{
			var originalSelectionStart = SelectionStart;
			var originalSelectionLength = SelectionLength;

			Text = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, replacement);

			SelectionStart = originalSelectionStart;
			SelectionLength = originalSelectionLength;
		}
	}
}
