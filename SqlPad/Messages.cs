using System.Windows;

namespace SqlPad
{
	public static class Messages
	{
		public static MessageBoxResult ShowError(string errorMessage, string caption = "Error", Window owner = null)
		{
			if (owner == null)
			{
				owner = App.MainWindow;
			}

			return MessageBox.Show(owner, errorMessage, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}