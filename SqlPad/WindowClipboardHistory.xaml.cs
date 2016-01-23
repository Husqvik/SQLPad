using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlPad
{
	public partial class WindowClipboardHistory
	{
		private static readonly char[] LineBreakCharacters = { '\n', '\r' };

		private readonly ObservableCollection<ClipboardEntry> _entries = new ObservableCollection<ClipboardEntry>();

		public IReadOnlyList<ClipboardEntry> Entries => _entries;

		public WindowClipboardHistory()
		{
			InitializeComponent();
		}

		private void LoadedHandler(object sender, RoutedEventArgs args)
		{
			var entries = EditorNavigationService.ClipboardHistory
				.Take(35)
				.Select((t, i) => new ClipboardEntry { HotKey = GetEntryHotKey(i), Title = GetTitle(t), Text = t });

			_entries.AddRange(entries);
		}

		private void MouseDownHandler(object sender, MouseButtonEventArgs args)
		{
			if (args.ClickCount != 2)
			{
				return;
			}

			PasteSelectedClipboardEntry();
		}

		private static Key GetEntryHotKey(int index)
		{
			return (Key)(index + 34);
		}

		private static string GetTitle(string text)
		{
			var index = text.IndexOfAny(LineBreakCharacters);
			return index == -1
				? text
				: $"{text.Substring(0, index)}{CellValueConverter.Ellipsis}";
		}

		private void PasteClickHandler(object sender, RoutedEventArgs args)
		{
			PasteSelectedClipboardEntry();
		}

		private void PasteSelectedClipboardEntry()
		{
			var collectionView = CollectionViewSource.GetDefaultView(_entries);
			var clipboardEntry = (ClipboardEntry)collectionView.CurrentItem;
			PasteClipboardEntry(clipboardEntry);
		}

		private void PasteClipboardEntry(ClipboardEntry clipboardEntry)
		{
			App.MainWindow.ActiveDocument.InsertText(clipboardEntry.Text);
			Close();
		}

		private void KeyDownHandler(object sender, KeyEventArgs args)
		{
			var key = args.Key;
			if (key >= Key.NumPad0 && key <= Key.NumPad9)
			{
				key -= 40;
			}

			if (key < Key.D0 || key > Key.Z)
			{
				return;
			}

			var clipboardEntry = _entries.SingleOrDefault(e => e.HotKey == key);
			if (clipboardEntry != null)
			{
				PasteClipboardEntry(clipboardEntry);
			}
		}
	}

	public class ClipboardEntry
	{
		public Key HotKey { get; set; }

		public string Title { get; set; }

		public string Text { get; set; }
	}

	internal class KeyToCharacterConverter : ValueConverterBase
	{
		private static readonly string[] Characters =
			{ "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var index = (int)value - 34;
			return Characters[index];
		}
	}
}
