using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In<T>(this T o, params T[] elements)
		{
			return elements.Any(e => e.Equals(o));
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

		public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
		{
			foreach (var item in items)
				collection.Add(item);
		}

		public static void ReplaceTextSegments(this TextEditor editor, ICollection<TextSegment> textSegments)
		{
			editor.Document.BeginUpdate();

			foreach (var textSegment in textSegments.OrderByDescending(s => s.IndextStart).ThenByDescending(s => s.Length))
			{
				editor.Document.Replace(textSegment.IndextStart, textSegment.Length, textSegment.Text);
			}

			editor.Document.EndUpdate();
		}
	}
}