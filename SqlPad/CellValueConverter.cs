using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SqlPad
{
	public class CellValueConverter : IValueConverter
	{
		public static readonly CellValueConverter Instance = new CellValueConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == DBNull.Value)
				return ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder;

			try
			{
				var stringValue = value as String ?? System.Convert.ToString(value);

				return String.Empty.Equals(stringValue)
					? ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder
					: stringValue;
			}
			catch (Exception e)
			{
				return $"Data conversion error: {e}";
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

	public class ObjectToVisibilityConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return Visibility.Collapsed;
			}

			var boolean = value as Boolean?;
			if (boolean.HasValue)
			{
				return boolean.Value
					? Visibility.Visible
					: Visibility.Collapsed;
			}

			return ReferenceEquals(value, String.Empty)
				? Visibility.Collapsed
				: Visibility.Visible;
		}
	}

	public class DateTimeLabelConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null || (DateTime)value == DateTime.MinValue ? ValueNotAvailable : CellValueConverter.FormatDateTime((DateTime)value);
		}
	}

	public class NumericConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : System.Convert.ToString(value);
		}
	}

	public class StringConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return String.IsNullOrEmpty((string)value) ? ValueNotAvailable : value;
		}
	}

	public class BooleanLabelConverter : ValueConverterBase
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

	public class PrettyPrintIntegerConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : System.Convert.ToDecimal(value).ToString("N0");
		}
	}

	public abstract class ValueConverterBase : IValueConverter
	{
		public static string ValueNotAvailable = "N/A";

		public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
