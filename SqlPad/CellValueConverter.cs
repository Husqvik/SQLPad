using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SqlPad
{
	public class CellValueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == DBNull.Value)
				return ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder;

			var columnHeader = (ColumnHeader)parameter;
			var convertedValue = columnHeader.ValueConverter.ConvertToCellValue(value);

			try
			{
				var convertedStringValue = convertedValue as String ?? System.Convert.ToString(convertedValue);

				return String.Empty.Equals(convertedStringValue)
					? ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder
					: convertedStringValue;
			}
			catch (Exception e)
			{
				return String.Format("Data conversion error: {0}", e);
			}
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

	public class ObjectToVisibilityConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return Visibility.Collapsed;
			}

			return ReferenceEquals(value, String.Empty)
				? Visibility.Collapsed
				: Visibility.Visible;
		}
	}

	public class DateTimeLabelConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null || (DateTime)value == DateTime.MinValue ? ValueNotAvailable : CellValueConverter.FormatDateTime((DateTime)value);
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

	public class ColorCodeToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var colorCode = (string)value;
			return String.IsNullOrEmpty(colorCode)
				? null
				: new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class ColorCodeToColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var colorCode = (string)value;
			return String.IsNullOrEmpty(colorCode)
				? Colors.Transparent
				: (Color)ColorConverter.ConvertFromString(colorCode);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value.ToString();
		}
	}

	public class PrettyPrintIntegerConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return System.Convert.ToDecimal(value).ToString("N0");
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
