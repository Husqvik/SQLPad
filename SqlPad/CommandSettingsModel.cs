using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace SqlPad
{
	public class CommandSettingsModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged = delegate { };

		private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		private string _value = String.Empty;
		private string _title = String.Empty;
		private string _heading = String.Empty;
		private string _description = String.Empty;

		public string Title
		{
			get { return _title; }
			set
			{
				if (_title == value)
					return;

				_title = value;
				RaisePropertyChanged();
			}
		}

		public string Heading
		{
			get { return _heading; }
			set
			{
				if (_heading == value)
					return;

				_heading = value;
				RaisePropertyChanged();
			}
		}

		public string Description
		{
			get { return _description; }
			set
			{
				if (_description == value)
					return;

				_description = value;
				RaisePropertyChanged();
			}
		}

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