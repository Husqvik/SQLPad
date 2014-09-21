using System;
using System.Diagnostics;
using System.Globalization;

namespace SqlPad
{
	public partial class Configuration
	{
		private const string DefaultNullValuePlaceholder = "(null)";
		private const byte DefaultDataModelRefreshPeriod = 10;

		public static readonly Configuration Default =
			new Configuration
			{
				dataModelField = new ConfigurationDataModel { DataModelRefreshPeriod = DefaultDataModelRefreshPeriod },
				resultGridField = new ConfigurationResultGrid { NullPlaceholder = DefaultNullValuePlaceholder }
			};

		public void Validate()
		{
			if (String.IsNullOrEmpty(resultGridField.DateFormat))
			{
				return;
			}
			
			try
			{
				Trace.WriteLine(String.Format("DateTime format test '{0}' => {1} succeeded. ", resultGridField.DateFormat, DateTime.Now.ToString(resultGridField.DateFormat)));
			}
			catch
			{
				var dateFormat = CultureInfo.CurrentUICulture.DateTimeFormat.UniversalSortableDateTimePattern;
				Trace.WriteLine(String.Format("DateFormat mask '{0}' is invalid. Using system UI culture - {1} ({2}). ", resultGridField.DateFormat, dateFormat, DateTime.Now));
				resultGridField.DateFormat = dateFormat;
			}
		}
	}
}