using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In<T>(this T o, params T[] elements)
		{
			return elements.Any(e => e.Equals(o));
		}

		public static IEnumerable<TItem> Distinct<TItem, TValue>(this IEnumerable<TItem> source, Func<TItem, TValue> selector)
		{
			var uniqueFilter = new HashSet<TValue>();
			return source.Where(i => uniqueFilter.Add(selector(i)));
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

		public static string ToHexString(this byte[] byteArray)
		{
			var characters = new char[byteArray.Length * 2];

			for (var i = 0; i < byteArray.Length; ++i)
			{
				var b = ((byte)(byteArray[i] >> 4));
				var index = 2 * i;
				characters[index] = (char)(b > 9 ? b + 0x37 : b + 0x30);
				b = ((byte)(byteArray[i] & 0xF));
				characters[index + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
			}

			return new string(characters);
		}

		public static string EnsureMaximumLength(this string text, int maximumLength)
		{
			return text.Length > maximumLength
				? text.Substring(0, maximumLength)
				: text;
		}

		public static IEnumerable<T> Distinct<T>(this IEnumerable<T> source, Func<T, object> selector)
		{
			var uniqueObjects = new HashSet<object>();
			return source.Where(o => uniqueObjects.Add(selector(o)));
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
		{
			return new HashSet<T>(source);
		}

		public static Task<IReadOnlyList<T>> EnumerateAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken)
		{
			return Task.Factory.StartNew(
				() => (IReadOnlyList<T>)new List<T>(source.TakeWhile(i => !cancellationToken.IsCancellationRequested)).AsReadOnly(),
				cancellationToken);
		}

		public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
		{
			return new ReadOnlyDictionary<TKey, TValue>(dictionary);
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

			var plural = timeSpan.TotalDays >= 2 ? "s" : null;

			return $"{(int)timeSpan.TotalDays} day{plural} {timeSpan.Hours}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
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

		public static string GetPlainText(this SecureString secureString)
		{
			return Marshal.PtrToStringUni(Marshal.SecureStringToGlobalAllocUnicode(secureString));
		}
	}

	public class StaticResourceBindingExtension : StaticResourceExtension
	{
		public PropertyPath Path { get; set; }
		
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var value = base.ProvideValue(serviceProvider);
			return (Path == null ? value : PathEvaluator.Eval(value, Path));
		}

		class PathEvaluator : DependencyObject
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
