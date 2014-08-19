using System;
using System.Globalization;
using System.Windows.Data;

namespace SqlPad
{
	public class CellValueConverter : IValueConverter
	{
		private const string NullValuePlaceholder = "(null)";

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var columnHeader = (ColumnHeader)parameter;
			if (value == DBNull.Value)
				return NullValuePlaceholder;

			var convertedValue = columnHeader.ValueConverterFunction(columnHeader, value).ToString();
			return String.Empty.Equals(convertedValue)
				? NullValuePlaceholder
				: convertedValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}

	public class SelectedIndexConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (int)value + 1;
		}
	}

	public class DateTimeLabelConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : ((DateTime)value).ToString(CultureInfo.CurrentUICulture);
		}
	}

	public class NumericConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : System.Convert.ToString(value);
		}
	}

	public class BooleanLabelConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : (bool)value ? "Yes" : "No";
		}
	}

	public abstract class ValueConverter : IValueConverter
	{
		public static string ValueNotAvailable = "N/A";

		public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
