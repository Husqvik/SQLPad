using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

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
	}

	public class CommandSettingsModel : ModelBase
	{
		private string _value = String.Empty;
		private readonly IDictionary<string, BooleanOption> _booleanOptions = new Dictionary<string, BooleanOption>();

		public CommandSettingsModel()
		{
			Description = String.Empty;
			Heading = String.Empty;
			Title = String.Empty;
			TextInputVisibility = Visibility.Visible;
			BooleanOptionsVisibility = Visibility.Collapsed;
		}

		public Func<bool> UseDefaultSettings { get; set; }

		public string Title { get; set; }

		public string Heading { get; set; }

		public string Description { get; set; }

		public Visibility TextInputVisibility { get; set; }

		public string Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}

		public Visibility BooleanOptionsVisibility { get; set; }

		public IDictionary<string, BooleanOption> BooleanOptions { get { return _booleanOptions; } }

		public ValidationRule ValidationRule { get; set; }

		public void AddBooleanOption(BooleanOption option)
		{
			_booleanOptions.Add(option.OptionIdentifier, option);
		}
	}

	[DebuggerDisplay("BooleanOption(OptionIdentifier={OptionIdentifier}; Description={Description}; Value={Value})")]
	public class BooleanOption : ModelBase
	{
		private bool _value;

		public bool Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}

		public string OptionIdentifier { get; set; }

		public string Description { get; set; }
	}
}
