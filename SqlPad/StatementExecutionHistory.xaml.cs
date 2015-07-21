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

		public ICollection<StatementExecutionHistoryEntry> ExecutionHistoryEntries
		{
			get { return _historyEntries; }
		}

		private StatementExecutionHistoryEntry SelectedEntry
		{
			get { return (StatementExecutionHistoryEntry)_collectionView.CurrentItem; }
		}

		public StatementExecutionHistory(string providerName)
		{
			InitializeComponent();

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);

			_collectionView = CollectionViewSource.GetDefaultView(_historyEntries);
			_collectionView.SortDescriptions.Add(new SortDescription("ExecutedAt", ListSortDirection.Descending));
		}

		private void MouseDownHandler(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount != 2)
			{
				return;
			}

			App.MainWindow.ActiveDocument.InsertStatement(SelectedEntry.StatementText);
		}

		private void WindowClosingHandler(object sender, CancelEventArgs e)
		{
			e.Cancel = true;
			SearchPhraseTextBox.Text = String.Empty;
			Hide();
		}

		private void ClearHistoryExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			_providerConfiguration.ClearStatementExecutionHistory();
			_historyEntries.Clear();
		}

		private void RemoveHistoryEntryExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var entry = (StatementExecutionHistoryEntry)e.Parameter;
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
					var textToSearch = $"{entry.StatementText.ToUpperInvariant()} {CellValueConverter.FormatDateTime(entry.ExecutedAt).ToUpperInvariant()}";
					return searchedWords.All(textToSearch.Contains);
				};

			ListHistoryEntries.UpdateLayout();

			HighlightText(searchedWords);
		}

		private void HighlightText(IEnumerable<string> searchedWords)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => ListHistoryEntries.HighlightTextItems(TextSearchHelper.GetRegexPattern(searchedWords))));
		}

		private void CopyExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Clipboard.SetText(SelectedEntry.StatementText);
		}

		private void HistoryViewScrollChangedHandler(object sender, ScrollChangedEventArgs e)
		{
			HighlightText(TextSearchHelper.GetSearchedWords(SearchPhraseTextBox.Text));
		}

		private void WindowKeyDownHandler(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
			{
				Close();
			}
		}
	}
}
