using System;
using System.Globalization;

namespace SqlPad
{
	public class DataSpaceConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : PrettyPrint((long)value);
		}

		public static string PrettyPrint(long bytes)
		{
			if (bytes < 1024)
			{
				return $"{bytes} B";
			}
			
			if (bytes < 1048576)
			{
				return $"{Math.Round(bytes / 1024m)} kB";
			}

			if (bytes < 1073741824)
			{
				return $"{Math.Round(bytes / 1048576m, 1)} MB";
			}

			if (bytes < 1099511627776)
			{
				return $"{Math.Round(bytes / 1073741824m, 2)} GB";
			}

			return $"{Math.Round(bytes / 1099511627776m, 2)} TB";
		}
	}
}
