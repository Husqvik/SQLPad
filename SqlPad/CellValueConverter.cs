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

	public class SelectedIndexConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (int)value + 1;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}