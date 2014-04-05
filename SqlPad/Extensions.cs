using System;
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

		public static IEnumerable<T> TakeWhileInclusive<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			foreach (var item in source)
			{
				if (predicate(item))
				{
					yield return item;
					continue;
				}

				yield return item;
				break;
			}
		}
	}
}