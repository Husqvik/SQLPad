using System;
using System.Windows;

namespace SqlPad
{
	public partial class PasswordDialog
	{
		public PasswordDialog(string label = null)
		{
			InitializeComponent();

			if (String.IsNullOrEmpty(label))
			{
				Label.Text = label;
			}
		}

		public string Password { get { return TextPassword.Password; } }

		private void ButtonConfirmPasswordClickHandler(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			TextPassword.Focus();
		}
	}
}
