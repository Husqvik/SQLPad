using System;
using System.Globalization;

namespace SqlPad
{
	public class DataSpaceConverter : ValueConverterBase
	{
		public static readonly DataSpaceConverter Instance = new DataSpaceConverter();

		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null
				? parameter?.ToString() ?? ValueNotAvailable
				: PrettyPrint(System.Convert.ToDecimal(value));
		}

		public static string PrettyPrint(decimal bytes)
		{
			if (bytes < 1024)
			{
				return $"{bytes} B";
			}
			
			if (bytes < 1048576)
			{
				return $"{Math.Round(bytes / 1024)} kB";
			}

			if (bytes < 1073741824)
			{
				return $"{Math.Round(bytes / 1048576, 1)} MB";
			}

			if (bytes < 1099511627776)
			{
				return $"{Math.Round(bytes / 1073741824, 2)} GB";
			}

			return $"{Math.Round(bytes / 1099511627776, 2)} TB";
		}
	}
}
