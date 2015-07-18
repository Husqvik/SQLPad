using System.Windows;

namespace SqlPad
{
	public static class Messages
	{
		public static MessageBoxResult ShowInformation(string errorMessage, string caption = "Information", Window owner = null)
		{
			return ShowMessage(errorMessage, caption, MessageBoxImage.Information, owner);
		}

		public static MessageBoxResult ShowError(string errorMessage, string caption = "Error", Window owner = null)
		{
			return ShowMessage(errorMessage, caption, MessageBoxImage.Error, owner);
		}

		private static MessageBoxResult ShowMessage(string message, string caption, MessageBoxImage image, Window owner)
		{
			if (owner == null)
			{
				owner = App.MainWindow;
			}

			return MessageBox.Show(owner, message, caption, MessageBoxButton.OK, image);
		}
	}
}