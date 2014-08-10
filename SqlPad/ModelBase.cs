using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SqlPad
{
	public class ModelBase : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged = delegate { };

		protected bool UpdateValueAndRaisePropertyChanged<T>(ref T value, T newValue, [CallerMemberName] string propertyName = null)
		{
			if (Equals(value, newValue))
				return false;

			value = newValue;
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			return true;
		}

		protected void RaisePropertyChanged(string propertyName)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
