using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;

namespace SqlPad
{
	public class CommandSettingsModel : ModelBase
	{
		private string _value = String.Empty;

		public Func<bool> UseDefaultSettings { get; set; }

		public string Title { get; set; } = String.Empty;

		public string Heading { get; set; } = String.Empty;

		public string Description { get; set; } = String.Empty;

		public bool IsTextInputVisible { get; set; } = true;

		public string Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}

		public IDictionary<string, BooleanOption> BooleanOptions { get; } = new Dictionary<string, BooleanOption>();

	    public ValidationRule ValidationRule { get; set; }

		public void AddBooleanOption(BooleanOption option)
		{
			BooleanOptions.Add(option.OptionIdentifier, option);
		}
	}

	[DebuggerDisplay("BooleanOption(OptionIdentifier={OptionIdentifier}; Value={Value})")]
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
