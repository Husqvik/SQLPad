using System.Text.RegularExpressions;

namespace SqlPad
{
	public static class TextHelper
	{
		private static readonly Regex RegexEverythingExceptSpace = new Regex(@"[^\t]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

		public static string ReplaceAllNonBlankCharactersWithSpace(string text)
		{
			return RegexEverythingExceptSpace.Replace(text, " ");
		}
	}
}