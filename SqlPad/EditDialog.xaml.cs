using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SqlPad.Commands;

namespace SqlPad
{
	public partial class EditDialog : ICommandSettingsProvider
	{
		public EditDialog(CommandSettingsModel settings)
		{
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			InitializeComponent();

			Settings = settings;
		}

		private void CloseClickHandler(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void LoadedHandler(object sender, RoutedEventArgs e)
		{
			DataContext = Settings;

			if (Settings.ValidationRule != null)
			{
				BindingOperations.GetBinding(TextValue, TextBox.TextProperty).ValidationRules.Add(Settings.ValidationRule);
				BindingOperations.GetBindingExpression(TextValue, TextBox.TextProperty).UpdateSource();
			}

			TextValue.Focus();
			TextValue.SelectAll();
		}

		public bool GetSettings()
		{
			if (Settings.UseDefaultSettings != null && Settings.UseDefaultSettings())
				return true;

			var result = ShowDialog();
			return result.HasValue && result.Value;
		}

		public CommandSettingsModel Settings { get; }

		private void SelectItemCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			bool? newValue = null;
			foreach (BooleanOption option in ((ListView)sender).SelectedItems)
			{
				if (!newValue.HasValue)
				{
					newValue = !option.Value;
				}

				option.Value = newValue.Value;
			}
		}
	}
}
