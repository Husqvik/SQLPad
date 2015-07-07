using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SqlPad
{
	public partial class StatementExecutionHistory
	{
		private static readonly char[] SearchPhraseSeparators = { ' ' };

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

		private static void FindListViewItem(DependencyObject target, string regexPattern)
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(target); i++)
			{
				if (target is ListViewItem || target is ListBoxItem)
				{
					HighlightText(target, regexPattern);
				}

				FindListViewItem(VisualTreeHelper.GetChild(target, i), regexPattern);
			}
		}

		private static void HighlightText(DependencyObject dependencyObject, string regexPattern)
		{
			if (dependencyObject == null)
			{
				return;
			}

			var textBlock = dependencyObject as TextBlock;
			if (textBlock == null)
			{
				for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
				{
					HighlightText(VisualTreeHelper.GetChild(dependencyObject, i), regexPattern);
				}
			}
			else
			{
				var text = textBlock.Text;
				if (regexPattern.Length == 0)
				{
					textBlock.Inlines.Clear();
					textBlock.Inlines.Add(text);
					return;
				}

				var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				var substrings = regex.Split(text);
				textBlock.Inlines.Clear();

				foreach (var item in substrings)
				{
					if (regex.Match(item).Success)
					{
						var run = new Run(item) { Background = Brushes.DarkGray };
						textBlock.Inlines.Add(run);
					}
					else
					{
						textBlock.Inlines.Add(item);
					}
				}
			}
		}

		private void SearchTextChangedHandler(object sender, TextChangedEventArgs args)
		{
			var searchedWords = SearchPhraseTextBox.Text.ToUpperInvariant().Split(SearchPhraseSeparators, StringSplitOptions.RemoveEmptyEntries);

			_collectionView.Filter = searchedWords.Length == 0
				? (Predicate<object>)null
				: e =>
				{
					var entry = (StatementExecutionHistoryEntry)e;
					var textToSearch = String.Format("{0} {1}", entry.StatementText.ToUpperInvariant(), CellValueConverter.FormatDateTime(entry.ExecutedAt).ToUpperInvariant());
					return searchedWords.All(textToSearch.Contains);
				};

			ListHistoryEntries.UpdateLayout();

			var regexPatterns = searchedWords.Select(w => String.Format("({0})", RegularExpressionEscapeCharacters.Aggregate(w, (p, c) => p.Replace(c, String.Format("\\{0}", c)))));
			var finalRegexPattern = String.Join("|", regexPatterns);

			FindListViewItem(ListHistoryEntries, finalRegexPattern);
		}

		private static readonly string[] RegularExpressionEscapeCharacters = { "*", "(", ")", "." };
	}
}
