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