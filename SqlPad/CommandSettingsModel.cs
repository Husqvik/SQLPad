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

		public CommandSettingsModel()
		{
			Description = String.Empty;
			Heading = String.Empty;
			Title = String.Empty;
		}

		public string Title { get; set; }

		public string Heading { get; set; }

		public string Description { get; set; }

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
