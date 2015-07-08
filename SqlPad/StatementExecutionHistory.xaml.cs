using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

			var entry = (StatementExecutionHistoryEntry)_collectionView.CurrentItem;
			App.MainWindow.ActiveDocument.InsertStatement(entry.StatementText);
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
					var textToSearch = String.Format("{0} {1}", entry.StatementText.ToUpperInvariant(), CellValueConverter.FormatDateTime(entry.ExecutedAt).ToUpperInvariant());
					return searchedWords.All(textToSearch.Contains);
				};

			ListHistoryEntries.UpdateLayout();

			Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => ListHistoryEntries.HighlightTextItems(TextSearchHelper.GetRegexPattern(searchedWords))));
		}
	}
}
