using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SqlPad.Oracle.ToolTips
{
	public class StartPointConverter : IValueConverter
	{
		[Obsolete]
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is double && ((double)value > 0.0))
			{
				return new Point((double)value / 2, 0);
			}

			return new Point();
		}

		[Obsolete]
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}

	}

	public class ArcSizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is double && ((double)value > 0.0))
			{
				return new Size((double)value / 2, (double)value / 2);
			}

			return new Point();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}

	public class ArcEndPointConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (!ObjectValueArrayHelper.TryExtractValues(values.Take(4), out var doubles))
			{
				return Binding.DoNothing;
			}

			var actualWidth = doubles[0];
			var value = doubles[1];
			var minimum = doubles[2];
			var maximum = doubles[3];

			if (values.Length == 5)
			{
				var fullIndeterminateScaling = ObjectValueArrayHelper.ExtractDouble(values[4]);
				if (!double.IsNaN(fullIndeterminateScaling) && fullIndeterminateScaling > 0.0)
				{
					value = (maximum - minimum) * fullIndeterminateScaling;
				}
			}

			var percent = maximum <= minimum ? 1.0 : (value - minimum) / (maximum - minimum);
			var degrees = 360 * percent;
			var radians = degrees * (Math.PI / 180);

			var centre = new Point(actualWidth / 2, actualWidth / 2);
			var hypotenuseRadius = (actualWidth / 2);

			var adjacent = Math.Cos(radians) * hypotenuseRadius;
			var opposite = Math.Sin(radians) * hypotenuseRadius;

			return new Point(centre.X + opposite, centre.Y - adjacent);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class LargeArcConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (!ObjectValueArrayHelper.TryExtractValues(values.Take(3), out var doubles))
			{
				return Binding.DoNothing;
			}

			var value = doubles[0];
			var minimum = doubles[1];
			var maximum = doubles[2];

			if (values.Length == 4)
			{
				var fullIndeterminateScaling = ObjectValueArrayHelper.ExtractDouble(values[3]);
				if (!double.IsNaN(fullIndeterminateScaling) && fullIndeterminateScaling > 0.0)
				{
					value = (maximum - minimum) * fullIndeterminateScaling;
				}
			}

			var percent = maximum <= minimum ? 1.0 : (value - minimum) / (maximum - minimum);

			return percent > 0.5;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class RotateTransformConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (!ObjectValueArrayHelper.TryExtractValues(values, out var doubles))
			{
				return Binding.DoNothing;
			}

			var value = doubles[0];
			var minimum = doubles[1];
			var maximum = doubles[2];

			var percent = maximum <= minimum ? 1.0 : (value - minimum) / (maximum - minimum);

			return 360 * percent;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class RotateTransformCentreConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			//value == actual width
			return (double)value / 2;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}

	internal static class ObjectValueArrayHelper
	{
		public static bool TryExtractValues(IEnumerable<object> values, out double[] doubles)
		{
			doubles = values.Select(ExtractDouble).ToArray();
			return !doubles.Any(double.IsNaN);
		}

		public static double ExtractDouble(object value)
		{
			var @double = value as double? ?? double.NaN;
			return double.IsInfinity(@double) ? double.NaN : @double;
		}
	}
}
