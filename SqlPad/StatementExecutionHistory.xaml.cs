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

		private static void FindListViewItem(DependencyObject target, string searchedText)
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(target); i++)
			{
				if (target is ListViewItem || target is ListBoxItem)
				{
					HighlightText(target, searchedText);
				}

				FindListViewItem(VisualTreeHelper.GetChild(target, i), searchedText);
			}
		}

		private static void HighlightText(DependencyObject dependencyObject, string searchedText)
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
					HighlightText(VisualTreeHelper.GetChild(dependencyObject, i), searchedText);
				}
			}
			else
			{
				var regex = new Regex(String.Format("({0})", searchedText), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				var text = textBlock.Text;
				if (searchedText.Length == 0)
				{
					textBlock.Inlines.Clear();
					textBlock.Inlines.Add(text);
					return;
				}

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
			var searchedText = SearchPhraseTextBox.Text.ToUpperInvariant();
			
			_collectionView.Filter = String.IsNullOrEmpty(searchedText)
				? (Predicate<object>)null
				: e =>
				{
					var entry = (StatementExecutionHistoryEntry)e;
					return entry.StatementText.ToUpperInvariant().Contains(searchedText) ||
					       CellValueConverter.FormatDateTime(entry.ExecutedAt).ToUpperInvariant().Contains(searchedText);
				};

			ListHistoryEntries.UpdateLayout();

			var regexPattern = RegularExpressionEscapeCharacters.Aggregate(searchedText, (p, c) => p.Replace(c, String.Format("\\{0}", c)));

			FindListViewItem(ListHistoryEntries, regexPattern);
		}

		private static readonly string[] RegularExpressionEscapeCharacters = { "*", "(", ")", "." };
	}
}
