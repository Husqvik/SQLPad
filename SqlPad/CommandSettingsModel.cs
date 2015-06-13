using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad
{
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

		public BooleanOption()
		{
			IsEnabled = true;
		}

		public bool Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}

		public string OptionIdentifier { get; set; }

		public object DescriptionContent { get; set; }

		public object Tag { get; set; }
		
		public bool IsEnabled { get; set; }
	}
}
