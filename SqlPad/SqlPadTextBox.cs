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

		public static void ConfigureCommands(TextBox textBox)
		{
			var canExecuteRoutedEventHandler = BuildCanExecuteIfWritableAndSelectionNotEmpty(textBox);
			textBox.CommandBindings.Add(new CommandBinding(MakeLowerCaseCommand, (s, args) => LowerCaseExecuted(textBox), canExecuteRoutedEventHandler));
			textBox.CommandBindings.Add(new CommandBinding(MakeUpperCaseCommand, (s, args) => UpperCaseExecuted(textBox), canExecuteRoutedEventHandler));
		}

		public static CanExecuteRoutedEventHandler BuildCanExecuteIfWritableAndSelectionNotEmpty(TextBox textBox)
		{
			return (s, args) => args.CanExecute = CanExecuteModifyCase(textBox);
		}

		public SqlPadTextBox()
		{
			ConfigureCommands(this);
		}

		private static bool CanExecuteModifyCase(TextBox textBox)
		{
			return !textBox.IsReadOnly && textBox.SelectionLength > 0;
		}

		private static void LowerCaseExecuted(TextBox textBox)
		{
			var replacement = textBox.SelectedText.ToLower(CultureInfo.CurrentUICulture);
			ReplaceSelectedText(textBox, replacement);
		}

		private static void UpperCaseExecuted(TextBox textBox)
		{
			var replacement = textBox.SelectedText.ToUpper(CultureInfo.CurrentUICulture);
			ReplaceSelectedText(textBox, replacement);
		}

		private static void ReplaceSelectedText(TextBox textBox, string replacement)
		{
			var originalSelectionStart = textBox.SelectionStart;
			var originalSelectionLength = textBox.SelectionLength;

			textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, replacement);

			textBox.SelectionStart = originalSelectionStart;
			textBox.SelectionLength = originalSelectionLength;
		}
	}
}
