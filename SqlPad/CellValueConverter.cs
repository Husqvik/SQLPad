using System;
using System.Globalization;
using System.Windows.Data;

namespace SqlPad
{
	public class CellValueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var columnHeader = (ColumnHeader)parameter;
			if (value == DBNull.Value)
				return ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder;

			var convertedValue = columnHeader.ValueConverter.ConvertToCellValue(value);
			if (convertedValue is DateTime)
			{
				return FormatDateTime((DateTime)value);
			}

			var convertedStringValue = convertedValue as String ?? System.Convert.ToString(convertedValue);

			return String.Empty.Equals(convertedStringValue)
				? ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder
				: convertedStringValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}

		public static string FormatDateTime(DateTime value)
		{
			return String.IsNullOrEmpty(ConfigurationProvider.Configuration.ResultGrid.DateFormat)
					? value.ToString(CultureInfo.CurrentUICulture)
					: value.ToString(ConfigurationProvider.Configuration.ResultGrid.DateFormat);
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
			return value == null || (DateTime)value == DateTime.MinValue ? ValueNotAvailable : ((DateTime)value).ToString(CultureInfo.CurrentUICulture);
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

	public class DataSpaceConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : FormatValue((long)value);
		}

		private static string FormatValue(long bytes)
		{
			if (bytes < 1024)
			{
				return String.Format("{0} B", bytes);
			}
			
			if (bytes < 1048576)
			{
				return String.Format("{0} kB", Math.Round(bytes / 1024m));
			}

			if (bytes < 1073741824)
			{
				return String.Format("{0} MB", Math.Round(bytes / 1048576m, 1));
			}

			if (bytes < 1099511627776)
			{
				return String.Format("{0} GB", Math.Round(bytes / 1073741824m, 2));
			}

			return String.Format("{0} TB", Math.Round(bytes / 1099511627776m, 2));
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
