using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.FindReplace
{
	public static class TextSearchHelper
	{
		private static readonly string[] RegularExpressionEscapeCharacters = { "*", "(", ")", "." };
		private static readonly char[] SearchPhraseSeparators = { ' ' };

		public static string[] GetSearchedWords(string searchPhrase)
		{
			return searchPhrase.ToUpperInvariant().Split(SearchPhraseSeparators, StringSplitOptions.RemoveEmptyEntries);
		}

		public static string GetRegexPattern(IEnumerable<string> searchedWords)
		{
			var regexPatterns = searchedWords.Select(w => String.Format("({0})", RegularExpressionEscapeCharacters.Aggregate(w, (p, c) => p.Replace(c, String.Format("\\{0}", c)))));
			return String.Join("|", regexPatterns);
		}	
	}
}