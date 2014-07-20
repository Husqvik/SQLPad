using System.Windows;

namespace SqlPad
{
	public static class Messages
	{
		public static MessageBoxResult ShowError(string errorMessage, string caption = "Error")
		{
			return MessageBox.Show(errorMessage, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}