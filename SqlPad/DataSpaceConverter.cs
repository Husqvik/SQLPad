using System;
using System.Globalization;

namespace SqlPad
{
	public class DataSpaceConverter : ValueConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? ValueNotAvailable : PrettyPrint((long)value);
		}

		public static string PrettyPrint(long bytes)
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
}
