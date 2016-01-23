using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SqlPad.FindReplace;

namespace SqlPad
{
	public partial class StatementExecutionHistory
	{
		private readonly ObservableCollection<StatementExecutionHistoryEntry> _historyEntries = new ObservableCollection<StatementExecutionHistoryEntry>();

		private readonly DatabaseProviderConfiguration _providerConfiguration;
		private readonly ICollectionView _collectionView;

		public ICollection<StatementExecutionHistoryEntry> ExecutionHistoryEntries => _historyEntries;

		private StatementExecutionHistoryEntry SelectedEntry => (StatementExecutionHistoryEntry)_collectionView.CurrentItem;

		public StatementExecutionHistory(string providerName)
		{
			InitializeComponent();

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);

			_collectionView = CollectionViewSource.GetDefaultView(_historyEntries);
			_collectionView.SortDescriptions.Add(new SortDescription("ExecutedAt", ListSortDirection.Descending));
		}

		private void MouseDownHandler(object sender, MouseButtonEventArgs args)
		{
			if (args.ClickCount != 2)
			{
				return;
			}

			App.MainWindow.ActiveDocument.InsertStatement(SelectedEntry.StatementText);
		}

		private void WindowClosingHandler(object sender, CancelEventArgs args)
		{
			args.Cancel = true;
			SearchPhraseTextBox.Text = String.Empty;
			Hide();
		}

		private void ClearHistoryExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			_providerConfiguration.ClearStatementExecutionHistory();
			_historyEntries.Clear();
		}

		private void RemoveHistoryEntryExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var entry = (StatementExecutionHistoryEntry)args.Parameter;
			_providerConfiguration.RemoveStatementExecutionHistoryEntry(entry);
			_historyEntries.Remove(entry);
		}

		private void SearchTextChangedHandler(object sender, TextChangedEventArgs args)
		{
			var searchedWords = TextSearchHelper.GetSearchedWords(SearchPhraseTextBox.Text);

			_collectionView.Filter = searchedWords.Length == 0
				? (Predicate<object>)null
				: e =>
				{
					var entry = (StatementExecutionHistoryEntry)e;
					var textToSearch = $"{entry.StatementText.ToUpperInvariant()} {CellValueConverter.FormatDateTime(entry.ExecutedAt).ToUpperInvariant()} {entry.Tags?.ToUpperInvariant()}";
					return searchedWords.All(textToSearch.Contains);
				};

			ListHistoryEntries.UpdateLayout();

			HighlightText(searchedWords);
		}

		private void HighlightText(IEnumerable<string> searchedWords)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => ListHistoryEntries.HighlightTextItems(TextSearchHelper.GetRegexPattern(searchedWords))));
		}

		private void CopyExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Clipboard.SetText(SelectedEntry.StatementText);
		}

		private void HistoryViewScrollChangedHandler(object sender, ScrollChangedEventArgs args)
		{
			HighlightText(TextSearchHelper.GetSearchedWords(SearchPhraseTextBox.Text));
		}

		private void WindowKeyDownHandler(object sender, KeyEventArgs args)
		{
			if (args.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
			{
				Close();
			}
		}
	}
}
