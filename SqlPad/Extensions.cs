using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Xml;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In<T>(this T o, params T[] elements) => elements.Any(e => Equals(e, o));

		public static IEnumerable<ICodeCompletionItem> OrderItems(this IEnumerable<ICodeCompletionItem> codeCompletionItems) =>
			codeCompletionItems.OrderBy(i => i.CategoryPriority).ThenBy(i => i.Priority).ThenBy(i => i.Label);

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

		public static bool TryGetFirstValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, out TValue value, params TKey[] keys)
		{
			foreach (var key in keys)
			{
				if (dictionary.TryGetValue(key, out value))
					return true;
			}

			value = default(TValue);
			return false;
		}

		public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : new()
		{
			if (!dictionary.TryGetValue(key, out TValue value))
			{
				dictionary[key] = value = new TValue();
			}

			return value;
		}

		public static bool AddIfNotNull<T>(this ICollection<T> collection, T item)
		{
			if (item == null)
			{
				return false;
			}

			collection.Add(item);
			return true;
		}

		public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
		{
			foreach (var item in items)
			{
				collection.Add(item);
			}
		}

		public static void ForEach<T>(this ICollection<T> collection, Action<T> action)
		{
			foreach (var item in collection)
			{
				action(item);
			}
		}

		private static readonly char[] HexSymbolLookup = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

		public static string ToHexString(this byte[] data)
		{
			var characters = new char[data.Length * 2];
			var targetIndex = 0;

			foreach (var @byte in data)
			{
				characters[targetIndex++] = HexSymbolLookup[@byte >> 4];
				characters[targetIndex++] = HexSymbolLookup[@byte & 0xF];
			}

			return new string(characters);
		}

		public static string EnsureMaximumLength(this string text, int maximumLength)
		{
			return text.Length > maximumLength
				? text.Substring(0, maximumLength)
				: text;
		}

		public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> source, Func<T, object> selector)
		{
			var uniqueObjects = new HashSet<object>();
			return source.Where(o => uniqueObjects.Add(selector(o)));
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new HashSet<T>(source);

		public static Task<IReadOnlyList<T>> EnumerateAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken) =>
			Task.Run(
				() => (IReadOnlyList<T>)source.TakeWhile(i => !cancellationToken.IsCancellationRequested).ToArray(),
				cancellationToken);

		public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) => new ReadOnlyDictionary<TKey, TValue>(dictionary);

		public static void AddToValues<TKey, TCollection, TValue>(this IDictionary<TKey, TCollection> dictionary, TKey key, TValue value) where TCollection : ICollection<TValue>, new()
		{
			if (!dictionary.TryGetValue(key, out var values))
			{
				dictionary.Add(key, values = new TCollection());
			}

			values.Add(value);
		}

		public static string ToPrettyString(this TimeSpan timeSpan)
		{
			if (timeSpan.TotalMilliseconds < 1000)
			{
				return $"{(int)timeSpan.TotalMilliseconds} ms";
			}

			if (timeSpan.TotalSeconds < 60)
			{
				return $"{Math.Round(timeSpan.TotalMilliseconds / 1000, 2)} s";
			}

			if (timeSpan.TotalHours < 1)
			{
				return $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
			}

			if (timeSpan.TotalDays < 1)
			{
				return $"{timeSpan.Hours}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
			}

			if (timeSpan == TimeSpan.MaxValue)
			{
				return "infinity";
			}

			var pluralPostfix = timeSpan.TotalDays >= 2 ? "s" : null;

			return $"{(int)timeSpan.TotalDays:N0} day{pluralPostfix} {timeSpan.Hours}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
		}

		public static async Task<byte[]> ReadAllBytesAsync(this FileStream file, CancellationToken cancellationToken)
		{
			var buffer = new byte[file.Length];
			await file.ReadAsync(buffer, 0, (int)file.Length, cancellationToken);
			return buffer;
		}

		public static async Task<string> ReadAllTextAsync(this FileStream file, CancellationToken cancellationToken)
		{
			using (var reader = new StreamReader(file))
			{
				return await reader.ReadToEndAsync();
			}
		}

		public static string GetPlainText(this SecureString secureString) =>
			Marshal.PtrToStringUni(Marshal.SecureStringToGlobalAllocUnicode(secureString));

		public static string ToPrettyString(this Enum enumValue, string separator = " | ", Func<Enum, string> formatFunction = null)
		{
			var values = Enum.GetValues(enumValue.GetType());

			if (formatFunction == null)
			{
				formatFunction = e => e.ToString();
			}

			var builder = new StringBuilder();
			var flagFound = false;
			foreach (Enum value in values)
			{
				if (!enumValue.HasFlag(value) || !Convert.ToBoolean(value))
				{
					continue;
				}

				if (flagFound)
				{
					builder.Append(separator);
				}

				builder.Append(formatFunction(value));
				flagFound = true;
			}

			if (!flagFound)
			{
				builder.Append(formatFunction(enumValue));
			}

			return builder.ToString();
		}

		public static string SplitCamelCase(this string value, string separator = " ") =>
			Regex.Replace(value, @"(\p{Lu})", $"{separator}$1").Remove(0, separator.Length);

		public static string ToString(this Boolean? value, string @true, string @false, string @null)
		{
			if (value == null)
			{
				return @null;
			}

			return value.Value ? @true : @false;
		}

		public static string ToXmlCompliant(this string text) => new String(text.Where(XmlConvert.IsXmlChar).ToArray());

		public static T PopIfNotEmpty<T>(this Stack<T> stack) =>
			stack.Count == 0
				? default(T)
				: stack.Pop();

		public static void PushIfNotNull<T>(this Stack<T> stack, T item)
		{
			if (item != null)
			{
				stack.Push(item);
			}
		}

		public static void PushMany<T>(this Stack<T> stack, IEnumerable<T> items)
		{
			foreach (var item in items)
			{
				stack.Push(item);
			}
		}
	}

	public class StaticResourceBindingExtension : StaticResourceExtension
	{
		public PropertyPath Path { get; set; }
		
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var value = base.ProvideValue(serviceProvider);
			return Path == null ? value : PathEvaluator.Eval(value, Path);
		}

		private class PathEvaluator : DependencyObject
		{
			private static readonly DependencyProperty DummyProperty = DependencyProperty.Register("Dummy", typeof(object), typeof(PathEvaluator), new UIPropertyMetadata(null));

			public static object Eval(object source, PropertyPath path)
			{
				var pathEvaluator = new PathEvaluator();
				BindingOperations.SetBinding(pathEvaluator, DummyProperty, new Binding(path.Path) { Source = source });
				return pathEvaluator.GetValue(DummyProperty);
			}
		}
	}
}
