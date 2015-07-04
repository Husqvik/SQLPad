using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace SqlPad
{
	public class OutputViewerModel : ModelBase
	{
		private int _selectedCellValueCount;
		private int _currentRowIndex = 1;
		private int _affectedRowCount = -1;

		private decimal _selectedCellSum;
		private decimal _selectedCellAverage;
		private decimal _selectedCellMin;
		private decimal _selectedCellMax;
		private string _executionTimerMessage;
		private bool _showAllSessionExecutionStatistics;
		private bool _enableDatabaseOutput;

		private Visibility _moreRowsExistVisibility = Visibility.Collapsed;
		private Visibility _selectedCellInfoVisibility = Visibility.Collapsed;
		private Visibility _selectedCellNumericInfoVisibility = Visibility.Collapsed;
		private Visibility _gridRowInfoVisibity = Visibility.Collapsed;
		private Visibility _statementExecutedSuccessfullyStatusMessageVisibility = Visibility.Collapsed;
		private Visibility _executionPlanAvailable = Visibility.Collapsed;

		private readonly ObservableCollection<object[]> _resultRows = new ObservableCollection<object[]>();
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
		private readonly StringBuilder _databaseOutputBuilder = new StringBuilder();

		public OutputViewerModel()
		{
			_sessionExecutionStatistics.CollectionChanged += (sender, args) => RaisePropertyChanged("ExecutionStatisticsAvailable");
			_compilationErrors.CollectionChanged += (sender, args) => RaisePropertyChanged("CompilationErrorsVisible");
			SetUpSessionExecutionStatisticsFilter();
			SetUpSessionExecutionStatisticsSorting();
		}

		public ObservableCollection<object[]> ResultRowItems { get { return _resultRows; } }

		public SessionExecutionStatisticsCollection SessionExecutionStatistics { get { return _sessionExecutionStatistics; } }

		public ObservableCollection<CompilationError> CompilationErrors { get { return _compilationErrors; } }

		public int CurrentRowIndex
		{
			get { return _currentRowIndex; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentRowIndex, value); }
		}

		public int SelectedCellValueCount
		{
			get { return _selectedCellValueCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellValueCount, value); }
		}

		public decimal SelectedCellSum
		{
			get { return _selectedCellSum; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellSum, value); }
		}

		public decimal SelectedCellAverage
		{
			get { return _selectedCellAverage; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellAverage, value); }
		}

		public decimal SelectedCellMin
		{
			get { return _selectedCellMin; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellMin, value); }
		}

		public decimal SelectedCellMax
		{
			get { return _selectedCellMax; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellMax, value); }
		}

		public Visibility MoreRowsExistVisibility
		{
			get { return _moreRowsExistVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _moreRowsExistVisibility, value); }
		}

		public Visibility SelectedCellInfoVisibility
		{
			get { return _selectedCellInfoVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellInfoVisibility, value); }
		}

		public Visibility SelectedCellNumericInfoVisibility
		{
			get { return _selectedCellNumericInfoVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectedCellNumericInfoVisibility, value); }
		}

		public Visibility ExecutionPlanAvailable
		{
			get { return _executionPlanAvailable; }
			set { UpdateValueAndRaisePropertyChanged(ref _executionPlanAvailable, value); }
		}

		public string ExecutionTimerMessage
		{
			get { return _executionTimerMessage; }
		}

		public Visibility ExecutionStatisticsAvailable
		{
			get { return _sessionExecutionStatistics.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
		}

		public Visibility CompilationErrorsVisible
		{
			get { return _compilationErrors.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
		}

		public bool EnableDatabaseOutput
		{
			get { return _enableDatabaseOutput; }
			set { UpdateValueAndRaisePropertyChanged(ref _enableDatabaseOutput, value); }
		}

		public bool KeepDatabaseOutputHistory { get; set; }

		public void WriteDatabaseOutput(string output)
		{
			if (!KeepDatabaseOutputHistory)
			{
				_databaseOutputBuilder.Clear();
			}

			if (!String.IsNullOrEmpty(output))
			{
				_databaseOutputBuilder.AppendLine(output);
			}

			RaisePropertyChanged("DatabaseOutput");
		}

		public int AffectedRowCount
		{
			get { return _affectedRowCount; }
			set
			{
				if (!UpdateValueAndRaisePropertyChanged(ref _affectedRowCount, value))
					return;

				RaisePropertyChanged("AffectedRowCountVisibility");
				RaisePropertyChanged("StatementExecutionInfoSeparatorVisibility");
			}
		}

		public Visibility AffectedRowCountVisibility
		{
			get { return _affectedRowCount == -1 ? Visibility.Collapsed : Visibility.Visible; }
		}

		public string DatabaseOutput { get { return _databaseOutputBuilder.ToString(); } }

		public void NotifyExecutionCanceled()
		{
			_executionTimerMessage = "Canceled";

			RaisePropertyChanged("ExecutionTimerMessage");
		}

		public Visibility StatementExecutionInfoSeparatorVisibility
		{
			get { return _gridRowInfoVisibity == Visibility.Collapsed && (AffectedRowCountVisibility == Visibility.Collapsed && StatementExecutedSuccessfullyStatusMessageVisibility == Visibility.Collapsed) ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility StatementExecutedSuccessfullyStatusMessageVisibility
		{
			get { return _statementExecutedSuccessfullyStatusMessageVisibility; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _statementExecutedSuccessfullyStatusMessageVisibility, value))
				{
					RaisePropertyChanged("StatementExecutionInfoSeparatorVisibility");
				}
			}
		}

		public Visibility GridRowInfoVisibility
		{
			get { return _gridRowInfoVisibity; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _gridRowInfoVisibity, value))
				{
					RaisePropertyChanged("StatementExecutionInfoSeparatorVisibility");
				}
			}
		}

		public void UpdateTimerMessage(TimeSpan timeSpan, bool isCanceling)
		{
			string formattedValue;
			if (timeSpan.TotalMilliseconds < 1000)
			{
				formattedValue = String.Format("{0} {1}", (int)timeSpan.TotalMilliseconds, "ms");
			}
			else if (timeSpan.TotalMilliseconds < 60000)
			{
				formattedValue = String.Format("{0} {1}", Math.Round(timeSpan.TotalMilliseconds / 1000, 2), "s");
			}
			else
			{
				formattedValue = String.Format("{0:00}:{1:00}", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
			}

			if (isCanceling)
			{
				formattedValue = String.Format("Canceling... {0}", formattedValue);
			}

			_executionTimerMessage = formattedValue;

			RaisePropertyChanged("ExecutionTimerMessage");
		}

		public bool ShowAllSessionExecutionStatistics
		{
			get { return _showAllSessionExecutionStatistics; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _showAllSessionExecutionStatistics, value))
				{
					SetUpSessionExecutionStatisticsFilter();
				}
			}
		}

		private void SetUpSessionExecutionStatisticsFilter()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.Filter = _showAllSessionExecutionStatistics
				? (Predicate<object>)null
				: ShowActiveSessionExecutionStatisticsFilter;
		}

		private void SetUpSessionExecutionStatisticsSorting()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
		}

		private static bool ShowActiveSessionExecutionStatisticsFilter(object record)
		{
			var statisticsRecord = (SessionExecutionStatisticsRecord)record;
			return statisticsRecord.Value != 0;
		}
	}
}
