using System;
using System.Security;
using System.Windows;

namespace SqlPad
{
	public partial class PasswordDialog
	{
		public string Password => TextPassword.Password;

		private PasswordDialog(string label)
		{
			InitializeComponent();

			Label.Text = label;
		}

		public static SecureString AskForPassword(string label, Window owner)
		{
			SecureString secureString = null;
			var passwordDialog = new PasswordDialog(label) { Owner = owner };
			if (passwordDialog.ShowDialog() == true)
			{
				secureString = new SecureString();
				foreach (var character in passwordDialog.Password)
				{
					secureString.AppendChar(character);
				}
			}

			passwordDialog.TextPassword.Password = String.Empty;

			return secureString;
		}

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
