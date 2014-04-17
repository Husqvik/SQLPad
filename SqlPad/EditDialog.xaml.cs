using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
				throw new ArgumentNullException("model");

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
			if (_model.ValidationRule != null)
			{
				var binding = BindingOperations.GetBinding(TextValue, TextBox.TextProperty);
				binding.ValidationRules.Add(_model.ValidationRule);
				// TODO: Add initial validation
				var bindingExpression = BindingOperations.GetBindingExpression(TextValue, TextBox.TextProperty);
			}

			DataContext = _model;

			TextValue.Focus();
			TextValue.SelectAll();
		}

		public bool GetSettings()
		{
			var result = ShowDialog();
			return result.HasValue && result.Value;
		}

		public CommandSettingsModel Settings { get { return _model; } }
	}
}
