using System;
using System.Linq;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In(this char character, params char[] characters)
		{
			return characters != null && characters.Any(c => c == character);
		}

		public static string ToOracleIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
				throw new ArgumentException("");

			return identifier[0] == '"' ? identifier : "\"" + identifier.ToUpperInvariant() + "\"";
		}
	}
}