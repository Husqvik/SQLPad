using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SqlPad.Commands;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for EditDialog.xaml
	/// </summary>
	public partial class EditDialog : ICommandSettingsProvider
	{
		private readonly CommandSettingsModel _model;

		public EditDialog(CommandSettingsModel model)
		{
			if (model == null)
				throw new ArgumentNullException(nameof(model));

			InitializeComponent();

			_model = model;
		}

		private void CloseClickHandler(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void LoadedHandler(object sender, RoutedEventArgs e)
		{
			DataContext = _model;

			if (_model.ValidationRule != null)
			{
				BindingOperations.GetBinding(TextValue, TextBox.TextProperty).ValidationRules.Add(_model.ValidationRule);
				BindingOperations.GetBindingExpression(TextValue, TextBox.TextProperty).UpdateSource();
			}

			TextValue.Focus();
			TextValue.SelectAll();
		}

		public bool GetSettings()
		{
			if (_model.UseDefaultSettings != null && _model.UseDefaultSettings())
				return true;

			var result = ShowDialog();
			return result.HasValue && result.Value;
		}

		public CommandSettingsModel Settings { get { return _model; } }

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
