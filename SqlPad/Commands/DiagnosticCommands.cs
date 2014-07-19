using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	internal static class DiagnosticCommands
	{
		public static RoutedCommand ShowTokenCommand = new RoutedCommand("ShowTokens", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) });
	}
}
