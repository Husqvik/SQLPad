using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlPad
{
	public partial class StatementExecutionHistory
	{
		private readonly ObservableCollection<StatementExecutionHistoryEntry> _historyEntries = new ObservableCollection<StatementExecutionHistoryEntry>();

		private readonly DatabaseProviderConfiguration _providerConfiguration;

		public ICollection<StatementExecutionHistoryEntry> ExecutionHistoryEntries
		{
			get { return _historyEntries; }
		}

		public StatementExecutionHistory(string providerName)
		{
			InitializeComponent();

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);

			var view = CollectionViewSource.GetDefaultView(_historyEntries);
			view.SortDescriptions.Add(new SortDescription("ExecutedAt", ListSortDirection.Descending));
		}

		private void MouseDownHandler(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount != 2)
			{
				return;
			}

			var view = CollectionViewSource.GetDefaultView(_historyEntries);
			var entry = (StatementExecutionHistoryEntry)view.CurrentItem;
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
	}
}
