using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SqlPad.FindReplace
{
	public static class TextSearchHelper
	{
		private static readonly char[] SearchPhraseSeparators = { ' ' };

		public static string[] GetSearchedWords(string searchPhrase)
		{
			return searchPhrase.ToUpperInvariant().Split(SearchPhraseSeparators, StringSplitOptions.RemoveEmptyEntries);
		}

		public static string GetRegexPattern(IEnumerable<string> searchedWords)
		{
			var regexPatterns = searchedWords.Select(w => $"({Regex.Escape(w)})");
			return String.Join("|", regexPatterns);
		}	
	}
}