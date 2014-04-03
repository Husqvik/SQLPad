using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In(this char character, params char[] characters)
		{
			return characters != null && characters.Any(c => c == character);
		}

		public static IEnumerable<ICodeCompletionItem> OrderItems(this IEnumerable<ICodeCompletionItem> codeCompletionItems)
		{
			return codeCompletionItems.OrderBy(i => i.CategoryPriority).ThenBy(i => i.Priority).ThenBy(i => i.Name);
		}
	}
}