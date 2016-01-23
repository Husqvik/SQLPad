using System.Collections.Generic;
using System.Collections.ObjectModel;
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

			PasteSelectedItem();
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
			PasteSelectedItem();
		}

		private void PasteSelectedItem()
		{
			var collectionView = CollectionViewSource.GetDefaultView(_entries);
			var clipboardEntry = (ClipboardEntry)collectionView.CurrentItem;
			App.MainWindow.ActiveDocument.InsertText(clipboardEntry.Text);

			Close();
		}
	}

	public class ClipboardEntry
	{
		public Key HotKey { get; set; }

		public string Title { get; set; }

		public string Text { get; set; }
	}
}
