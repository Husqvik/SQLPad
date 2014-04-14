using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for EditDialog.xaml
	/// </summary>
	public partial class EditDialog
	{
		private readonly EditDialogViewModel _model;

		public EditDialog(EditDialogViewModel model)
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
				//BindingOperations.GetBindingExpression(TextValue, TextBox.TextProperty).UpdateSource();
			}

			DataContext = _model;

			TextValue.Focus();
		}
	}

	public class EditDialogViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged = delegate { };

		private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		private string _value;

		public string Value
		{
			get { return _value; }
			set
			{
				if (_value == value)
					return;

				_value = value;
				RaisePropertyChanged();
			}
		}

		public ValidationRule ValidationRule { get; set; }
	}
}
