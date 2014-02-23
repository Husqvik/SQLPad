using System.Linq;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In(this char character, params char[] characters)
		{
			return characters != null && characters.Any(c => c == character);
		}
	}
}