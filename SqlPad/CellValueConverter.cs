using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SqlPad
{
	public class CellValueConverter : IValueConverter
	{
		public const string Ellipsis = "\u2026";

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

	public class VisibilityCollapseIfZeroConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (int)value == 0 ? Visibility.Collapsed : Visibility.Visible;
		}
	}

	public class DateTimeLabelConverter : ValueConverterBase
	{
		private readonly string _valueNotAvailablePlaceholder;

		public DateTimeLabelConverter() : this(ValueNotAvailable)
		{
		}

		public DateTimeLabelConverter(string valueNotAvailablePlaceholder)
		{
			_valueNotAvailablePlaceholder = valueNotAvailablePlaceholder;
		}

		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null || (DateTime)value == DateTime.MinValue ? _valueNotAvailablePlaceholder : CellValueConverter.FormatDateTime((DateTime)value);
		}
	}

	public class NumericConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : System.Convert.ToString(value);
		}
	}

	public class RatioConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var stringValue = (string)parameter;
			var precision = String.IsNullOrWhiteSpace(stringValue)
				? 0
				: System.Convert.ToInt32(stringValue);

			var ratio = System.Convert.ToDecimal(value);
			return value == null ? ValueNotAvailable : $"{Math.Round(ratio * 100, precision, MidpointRounding.AwayFromZero)} %";
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
		public static readonly ColorCodeToBrushConverter Instance = new ColorCodeToBrushConverter();

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

	public class ListAggregationConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var values = (IEnumerable<object>)value;
			var maximumItemCount = (int?)parameter ?? 3;
			var topItems = values.Take(maximumItemCount + 1).ToArray();
			if (topItems.Length > maximumItemCount)
			{
				topItems[maximumItemCount] = CellValueConverter.Ellipsis;
			}

			return String.Join(", ", topItems);
		}
	}

	public abstract class ValueConverterBase : IValueConverter
	{
		public const string ValueNotAvailable = "N/A";

		public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

		public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class EqualValueToBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value.Equals(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value.Equals(true) ? parameter : Binding.DoNothing;
		}
	}
}
