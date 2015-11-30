using System;
using System.ComponentModel;
using System.Security;
using System.Windows;

namespace SqlPad
{
	public partial class PasswordDialog
	{
		public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PasswordDialog), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty IsCapsLockEnabledProperty = DependencyProperty.Register(nameof(IsCapsLockEnabled), typeof(bool), typeof(PasswordDialog), new FrameworkPropertyMetadata());

		[Bindable(true)]
		public string Label
		{
			get { return (string)GetValue(LabelProperty); }
			private set { SetValue(LabelProperty, value); }
		}

		[Bindable(true)]
		public bool IsCapsLockEnabled
		{
			get { return (bool)GetValue(IsCapsLockEnabledProperty); }
			private set { SetValue(IsCapsLockEnabledProperty, value); }
		}

		public string Password => TextPassword.Password;

		private PasswordDialog(string label)
		{
			InitializeComponent();

			ResolveCapsLockStatus();

			TextPassword.KeyDown += delegate { ResolveCapsLockStatus(); };

			Label = label;
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

		private void ResolveCapsLockStatus()
		{
			IsCapsLockEnabled = Console.CapsLock;
		}
	}
}
