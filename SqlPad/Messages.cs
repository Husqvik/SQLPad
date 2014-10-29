using System.Windows;

namespace SqlPad
{
	public static class Messages
	{
		public static MessageBoxResult ShowError(string errorMessage, string caption = "Error")
		{
			return MessageBox.Show(errorMessage, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		public static MessageBoxResult ShowError(Window owner, string errorMessage, string caption = "Error")
		{
			return MessageBox.Show(owner, errorMessage, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}