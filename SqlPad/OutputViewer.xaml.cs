using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SqlPad.DataExport;
using SqlPad.FindReplace;
using Timer = System.Timers.Timer;

namespace SqlPad
{
	[DebuggerDisplay("OutputViewer (Title={Title})")]
	public partial class OutputViewer : IDisposable
	{
		private const int MaxHistoryEntrySize = 8192;

		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly StringBuilder _databaseOutputBuilder = new StringBuilder();
		private readonly ObservableCollection<object[]> _resultRows = new ObservableCollection<object[]>();
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
	    private readonly DatabaseProviderConfiguration _providerConfiguration;
		private readonly DocumentPage _documentPage;
		private readonly IConnectionAdapter _connectionAdapter;
		private readonly IStatementValidator _statementValidator;

		private bool _isRunning;
		private bool _isSelectingCells;
		private bool _hasExecutionResult;
		private object _previousSelectedTab;
		private StatementExecutionResult _executionResult;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IExecutionPlanViewer ExecutionPlanViewer { get; }
		
		public ITraceViewer TraceViewer { get; }

		private bool IsCancellationRequested
		{
			get
			{
				var cancellationTokenSource = _statementExecutionCancellationTokenSource;
				return cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested;
			}
		}

		public bool KeepDatabaseOutputHistory { get; set; }

		public StatusInfoModel StatusInfo { get; } = new StatusInfoModel();

		public bool IsBusy
		{
			get { return _isRunning; }
			private set
			{
				_isRunning = value;
				_documentPage.NotifyExecutionEvent();
			}
		}

		internal EventHandler<DataGridBeginningEditEventArgs> ResultGridBeginningEditCancelTextInputHandler => App.ResultGridBeginningEditCancelTextInputHandlerImplementation;

		public IReadOnlyList<object[]> ResultRowItems => _resultRows;

		public IReadOnlyList<SessionExecutionStatisticsRecord> SessionExecutionStatistics => _sessionExecutionStatistics;

		public IReadOnlyList<CompilationError> CompilationErrors => _compilationErrors;

		public OutputViewer(DocumentPage documentPage)
		{
			InitializeComponent();
			
			_timerExecutionMonitor.Elapsed += delegate { Dispatcher.Invoke(() => UpdateTimerMessage(_stopWatch.Elapsed, IsCancellationRequested)); };

			Application.Current.Deactivated += ApplicationDeactivatedHandler;

			Initialize();

			_documentPage = documentPage;

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(_documentPage.CurrentConnection.ProviderName);

			_connectionAdapter = _documentPage.DatabaseModel.CreateConnectionAdapter();

			_statementValidator = _documentPage.InfrastructureFactory.CreateStatementValidator();

			ExecutionPlanViewer = _documentPage.InfrastructureFactory.CreateExecutionPlanViewer(_documentPage.DatabaseModel);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;

			TraceViewer = _documentPage.InfrastructureFactory.CreateTraceViewer(_connectionAdapter);
			TabTrace.Content = TraceViewer.Control;
		}

		private void SessionExecutionStatisticsFilterHandler(object sender, FilterEventArgs e)
		{
			e.Accepted = ShowAllSessionExecutionStatistics || ((SessionExecutionStatisticsRecord)e.Item).Value != 0;
		}

		private void DisplayResult()
		{
			DataGridHelper.InitializeDataGridColumns(ResultGrid, _executionResult.ColumnHeaders, _statementValidator, _connectionAdapter);

			StatusInfo.ResultGridAvailable = true;
			_resultRows.Clear();

			AppendRows(_executionResult.InitialResultSet);
		}

		public void Cancel()
		{
			var cancellationTokenSource = _statementExecutionCancellationTokenSource;
			if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
			{
				cancellationTokenSource.Cancel();
			}
		}

		private void Initialize()
		{
			_hasExecutionResult = false;

			_resultRows.Clear();
			_compilationErrors.Clear();
			_sessionExecutionStatistics.Clear();
			TabCompilationErrors.Visibility = Visibility.Collapsed;
			TabStatistics.Visibility = Visibility.Collapsed;
			TabExecutionPlan.Visibility = Visibility.Collapsed;

			StatusInfo.ResultGridAvailable = false;
			StatusInfo.MoreRowsAvailable = false;
			StatusInfo.DdlStatementExecutedSuccessfully = false;
			StatusInfo.AffectedRowCount = -1;
			StatusInfo.SelectedRowIndex = 0;
			
			WriteDatabaseOutput(String.Empty);

			LastStatementText = String.Empty;

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.None;

			_previousSelectedTab = TabControlResult.SelectedItem;

			SelectDefaultTabIfNeeded();
		}

		private void SelectDefaultTabIfNeeded()
		{
			if (!IsTabAlwaysVisible(TabControlResult.SelectedItem))
			{
				TabControlResult.SelectedItem = TabResultSet;
			}
		}

		private bool IsPreviousTabAlwaysVisible => _previousSelectedTab != null && IsTabAlwaysVisible(_previousSelectedTab);

	    private void SelectPreviousTab()
		{
			if (_previousSelectedTab != null)
			{
				TabControlResult.SelectedItem = _previousSelectedTab;
			}
		}

		private void ShowExecutionPlan()
		{
			TabExecutionPlan.Visibility = Visibility.Visible;
			TabControlResult.SelectedItem = TabExecutionPlan;
		}

		private void ShowCompilationErrors()
		{
			TabCompilationErrors.Visibility = Visibility.Visible;
			TabControlResult.SelectedItem = TabCompilationErrors;
		}

		public Task ExecuteExplainPlanAsync(StatementExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(() => ExecuteExplainPlanAsyncInternal(executionModel));
		}

		private async Task ExecuteExplainPlanAsyncInternal(StatementExecutionModel executionModel)
		{
			SelectDefaultTabIfNeeded();

			TabStatistics.Visibility = Visibility.Collapsed;

			var actionResult = await SafeTimedActionAsync(() => ExecutionPlanViewer.ExplainAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (_statementExecutionCancellationTokenSource.Token.IsCancellationRequested)
			{
				NotifyExecutionCanceled();
			}
			else
			{
				UpdateTimerMessage(actionResult.Elapsed, false);

				if (actionResult.IsSuccessful)
				{
					ShowExecutionPlan();
				}
				else
				{
					Messages.ShowError(actionResult.Exception.Message);
				}
			}
		}

		public Task ExecuteDatabaseCommandAsync(StatementExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(() => ExecuteDatabaseCommandAsyncInternal(executionModel));
		}

		private async Task ExecuteDatabaseCommandAsyncInternal(StatementExecutionModel executionModel)
		{
			Initialize();

			_connectionAdapter.EnableDatabaseOutput = EnableDatabaseOutput;

			var executedAt = DateTime.Now;

			Task<StatementExecutionResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = _connectionAdapter.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(actionResult.Exception.Message);
				return;
			}

			var executionHistoryRecord = new StatementExecutionHistoryEntry(executionModel.StatementText, executedAt);
			LastStatementText = executionModel.StatementText;

			if (executionHistoryRecord.StatementText.Length <= MaxHistoryEntrySize)
			{
				_providerConfiguration.AddStatementExecution(executionHistoryRecord);
			}
			else
			{
				Trace.WriteLine($"Executes statement not stored in the execution history. The maximum allowed size is {MaxHistoryEntrySize} characters while the statement has {executionHistoryRecord.StatementText.Length} characters.");
			}

			_executionResult = innerTask.Result;

			if (!_executionResult.ExecutedSuccessfully)
			{
				NotifyExecutionCanceled();
				return;
			}

			TransactionControlVisibity = _connectionAdapter.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;

			UpdateTimerMessage(actionResult.Elapsed, false);
			
			WriteDatabaseOutput(_executionResult.DatabaseOutput);

			if (_executionResult.Statement.GatherExecutionStatistics)
			{
				await ExecutionPlanViewer.ShowActualAsync(_connectionAdapter, _statementExecutionCancellationTokenSource.Token);
				TabStatistics.Visibility = Visibility.Visible;
				TabExecutionPlan.Visibility = Visibility.Visible;
				_sessionExecutionStatistics.MergeWith(await _connectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				SelectPreviousTab();
			}
			else if (IsPreviousTabAlwaysVisible)
			{
				SelectPreviousTab();
			}

			if (_executionResult.CompilationErrors.Count > 0)
			{
				var lineOffset = _documentPage.Editor.GetLineNumberByOffset(executionModel.Statement.SourcePosition.IndexStart);
				foreach (var error in _executionResult.CompilationErrors)
				{
					error.Line += lineOffset;
					_compilationErrors.Add(error);
				}

				ShowCompilationErrors();
			}

			if (_executionResult.ColumnHeaders.Count == 0)
			{
				if (_executionResult.AffectedRowCount == -1)
				{
					StatusInfo.DdlStatementExecutedSuccessfully = true;
				}
				else
				{
					StatusInfo.AffectedRowCount = _executionResult.AffectedRowCount;
				}

				return;
			}

			_hasExecutionResult = true;

			var childReferenceDataSources = await _statementValidator.ApplyReferenceConstraintsAsync(_executionResult, _documentPage.DatabaseModel, _statementExecutionCancellationTokenSource.Token);

			DisplayResult();

			AddChildReferenceColumns(ResultGrid, childReferenceDataSources);
		}

		private void AddChildReferenceColumns(DataGrid dataGrid, IEnumerable<IReferenceDataSource> childReferenceDataSources)
		{
			if (!EnableChildReferenceDataSources)
			{
				return;
			}

			foreach (var childReferenceDataSource in childReferenceDataSources)
			{
				var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
				var hyperlinkFactory = new FrameworkElementFactory(typeof(Hyperlink));
				var runFactory = new FrameworkElementFactory(typeof(Run));
				runFactory.SetValue(Run.TextProperty, "Show child records");
				hyperlinkFactory.AppendChild(runFactory);
				hyperlinkFactory.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)CellHyperlinkExpandChildRecordsClickHandler);
				hyperlinkFactory.SetBinding(FrameworkContentElement.TagProperty, new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1) });
				hyperlinkFactory.SetValue(FrameworkContentElement.DataContextProperty, childReferenceDataSource);
				textBlockFactory.AppendChild(hyperlinkFactory);

				var cellTemplate = new DataTemplate(typeof(DependencyObject)) { VisualTree = textBlockFactory };

				var columnTemplate =
					new DataGridTemplateColumn
					{
						Header = new TextBlock { Text = $"{childReferenceDataSource.ObjectName} ({childReferenceDataSource.ConstraintName})" },
						IsReadOnly = true,
						CellTemplate = cellTemplate
					};

				dataGrid.Columns.Add(columnTemplate);
			}
		}

		private async void CellHyperlinkExpandChildRecordsClickHandler(object sender, RoutedEventArgs args)
		{
			var hyperlink = (Hyperlink)sender;
			var dataSource = (IReferenceDataSource)hyperlink.DataContext;
			var cell = (DataGridCell)hyperlink.Tag;
			var row = cell.FindParent<DataGridRow>();
			var currentRowValues = (object[])row.DataContext;
			var keyValues = dataSource.ColumnHeaders.Select(h => currentRowValues[h.ColumnIndex]).ToArray();

			var executionModel = dataSource.CreateExecutionModel(keyValues);

			FrameworkElement element;

			try
			{
				var cancellationToken = CancellationToken.None;
				var executionResult = await _connectionAdapter.ExecuteChildStatementAsync(executionModel, cancellationToken);
				var childReferenceDataSources = await _statementValidator.ApplyReferenceConstraintsAsync(executionResult, _connectionAdapter.DatabaseModel, cancellationToken);

				var childRecordDataGrid =
					new DataGrid
					{
						RowHeaderWidth = 0,
						Style = (Style)Application.Current.Resources["ResultSetDataGrid"],
						ItemsSource = new ObservableCollection<object[]>(executionResult.InitialResultSet)
					};

				childRecordDataGrid.AddHandler(VirtualizingStackPanel.CleanUpVirtualizedItemEvent, (CleanUpVirtualizedItemEventHandler)CleanUpVirtualizedItemHandler);
				childRecordDataGrid.BeginningEdit += App.ResultGridBeginningEditCancelTextInputHandlerImplementation;
				childRecordDataGrid.MouseDoubleClick += ResultGridMouseDoubleClickHandler;

				DataGridHelper.InitializeDataGridColumns(childRecordDataGrid, executionResult.ColumnHeaders, _statementValidator, _connectionAdapter);
				AddChildReferenceColumns(childRecordDataGrid, childReferenceDataSources);

				foreach (var columnTemplate in childRecordDataGrid.Columns)
				{
					columnTemplate.HeaderStyle = (Style)Application.Current.Resources["ColumnHeaderClickBubbleCancelation"];
				}

				element = childRecordDataGrid;
			}
			catch (Exception exception)
			{
				element = DataGridHelper.CreateErrorText(exception.Message);
			}

			cell.Content = DataGridHelper.ConfigureAndWrapUsingScrollViewerIfNeeded(cell, element);
		}

		private void NotifyExecutionCanceled()
		{
			StatusInfo.ExecutionTimerMessage = "Canceled";
		}

		private async Task ExecuteUsingCancellationToken(Func<Task> function)
		{
			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				await function();

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private void TabControlResultGiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
		{
			e.Handled = true;
		}

		private void CanExportDataHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Items.Count > 0;
		}

		private void CanGenerateCSharpQueryClassHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Columns.Count > 0;
		}

		private void ExportDataFileHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;
			var dialog = new SaveFileDialog { Filter = dataExporter.FileNameFilter, OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			App.SafeActionWithUserError(() => dataExporter.ExportToFile(dialog.FileName, ResultGrid, _documentPage.InfrastructureFactory.DataExportConverter));
		}

		private void ExportDataClipboardHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;

			App.SafeActionWithUserError(() => dataExporter.ExportToClipboard(ResultGrid, _documentPage.InfrastructureFactory.DataExportConverter));
		}

		private void GenerateCSharpQuery(object sender, ExecutedRoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "C# files (*.cs)|*.cs|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			using (var writer = File.CreateText(dialog.FileName))
			{
				CSharpQueryClassGenerator.Generate(_executionResult, writer);
			}
		}

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			var senderDataGrid = (DataGrid)sender;
			var originalDataGrid = ((Visual)e.OriginalSource).FindParent<DataGrid>();
			if (Equals(originalDataGrid, senderDataGrid))
			{
				DataGridHelper.ShowLargeValueEditor(senderDataGrid);
			}
		}

		private void ResultGridSelectedCellsChangedHandler(object sender, SelectedCellsChangedEventArgs e)
		{
			if (_isSelectingCells)
			{
				return;
			}

			StatusInfo.SelectedRowIndex = ResultGrid.CurrentCell.Item == null
				? 0
				: ResultGrid.Items.IndexOf(ResultGrid.CurrentCell.Item) + 1;

			CalculateSelectedCellStatistics();
		}

		private void CalculateSelectedCellStatistics()
		{
			if (ResultGrid.SelectedCells.Count <= 1)
			{
				SelectedCellInfoVisibility = Visibility.Collapsed;
				return;
			}

			var sum = 0m;
			var min = Decimal.MaxValue;
			var max = Decimal.MinValue;
			var count = 0;
			var hasOnlyNumericValues = true;
			foreach (var selectedCell in ResultGrid.SelectedCells)
			{
				var columnHeader = selectedCell.Column.Header as ColumnHeader;
				if (columnHeader == null)
				{
					return;
				}

				var rowValues = (object[])selectedCell.Item;
				var cellValue = rowValues[columnHeader.ColumnIndex];
				var stringValue = cellValue.ToString();
				if (String.IsNullOrEmpty(stringValue))
				{
					continue;
				}

				if (hasOnlyNumericValues)
				{
					try
					{
						var numericValue = Convert.ToDecimal(stringValue, CultureInfo.CurrentCulture);
						sum += numericValue;

						if (numericValue > max)
						{
							max = numericValue;
						}

						if (numericValue < min)
						{
							min = numericValue;
						}
					}
					catch
					{
						hasOnlyNumericValues = false;
					}
				}

				count++;
			}

			SelectedCellValueCount = count;

			if (count > 0)
			{
				SelectedCellSum = sum;
				SelectedCellMin = min;
				SelectedCellMax = max;
				SelectedCellAverage = sum / count;

				SelectedCellNumericInfoVisibility = hasOnlyNumericValues ? Visibility.Visible : Visibility.Collapsed;
			}
			else
			{
				SelectedCellNumericInfoVisibility = Visibility.Collapsed;
			}

			SelectedCellInfoVisibility = Visibility.Visible;
		}

		private void ColumnHeaderMouseClickHandler(object sender, RoutedEventArgs e)
		{
			var header = e.OriginalSource as DataGridColumnHeader;
			if (header == null)
			{
				return;
			}

			var clearCurrentCells = Keyboard.Modifiers != ModifierKeys.Shift;
			if (clearCurrentCells)
			{
				ResultGrid.SelectedCells.Clear();
			}

			_isSelectingCells = true;

			var selectedCells = new HashSet<DataGridCellInfo>(ResultGrid.SelectedCells);
			foreach (object[] rowItems in ResultGrid.Items)
			{
				var cell = new DataGridCellInfo(rowItems, header.Column);
				//if (!ResultGrid.SelectedCells.Contains(cell))
				if (clearCurrentCells || !selectedCells.Contains(cell))
				{
					ResultGrid.SelectedCells.Add(cell);
				}
			}

			_isSelectingCells = false;

			StatusInfo.SelectedRowIndex = ResultGrid.SelectedCells.Count;

			CalculateSelectedCellStatistics();

			ResultGrid.Focus();
		}

		private bool IsTabAlwaysVisible(object tabItem)
		{
			return TabControlResult.Items.IndexOf(tabItem).In(0, 2);
		}

		private async void ResultGridScrollChangedHandler(object sender, ScrollChangedEventArgs e)
		{
			if (e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
			{
				return;
			}

			if (!CanFetchNextRows())
			{
				return;
			}

			await ExecuteUsingCancellationToken(FetchNextRows);
		}

		private async void FetchAllRowsHandler(object sender, ExecutedRoutedEventArgs args)
		{
			await ExecuteUsingCancellationToken(FetchAllRows);
		}

		private async Task FetchAllRows()
		{
			while (_connectionAdapter.CanFetch && !IsCancellationRequested)
			{
				await FetchNextRows();
			}
		}

		private void ErrorListMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			var errorUnderCursor = (CompilationError)((DataGrid)sender).CurrentItem;
			if (errorUnderCursor == null || CompilationError == null)
			{
				return;
			}

			CompilationError(this, new CompilationErrorArgs(errorUnderCursor));
		}

		private void CanFetchAllRowsHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = CanFetchNextRows();
			canExecuteRoutedEventArgs.ContinueRouting = canExecuteRoutedEventArgs.CanExecute;
		}

		private bool CanFetchNextRows()
		{
			return _hasExecutionResult && !IsBusy && _connectionAdapter.CanFetch && !_connectionAdapter.IsExecuting;
		}

		private async Task FetchNextRows()
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = StatementExecutionModel.DefaultRowBatchSize - _resultRows.Count % StatementExecutionModel.DefaultRowBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = _connectionAdapter.FetchRecordsAsync(batchSize, _statementExecutionCancellationTokenSource.Token));

			if (exception != null)
			{
				Messages.ShowError(exception.Message);
			}
			else
			{
				AppendRows(innerTask.Result);

				if (_executionResult.Statement.GatherExecutionStatistics)
				{
					_sessionExecutionStatistics.MergeWith(await _connectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				}
			}
		}

		private void AppendRows(IEnumerable<object[]> rows)
		{
			_resultRows.AddRange(rows);
			StatusInfo.MoreRowsAvailable = _connectionAdapter.CanFetch;
		}

		private async Task<ActionResult> SafeTimedActionAsync(Func<Task> action)
		{
			var actionResult = new ActionResult();

			_stopWatch.Restart();
			_timerExecutionMonitor.Start();

			actionResult.Exception = await App.SafeActionAsync(action);
			actionResult.Elapsed = _stopWatch.Elapsed;

			_timerExecutionMonitor.Stop();
			_stopWatch.Stop();

			return actionResult;
		}

		public void Dispose()
		{
			Application.Current.Deactivated -= ApplicationDeactivatedHandler;
			_timerExecutionMonitor.Stop();
			_timerExecutionMonitor.Dispose();
			_connectionAdapter.Dispose();
		}

		private async void ButtonCommitTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteTransactionOperation(_connectionAdapter.CommitTransaction);
		}

		private async void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteTransactionOperation(_connectionAdapter.RollbackTransaction);
		}

		private async Task ExecuteTransactionOperation(Func<Task> transactionOperation)
		{
			IsTransactionControlEnabled = false;

			IsBusy = true;

			var result = await SafeTimedActionAsync(transactionOperation);
			UpdateTimerMessage(result.Elapsed, false);

			if (result.IsSuccessful)
			{
				TransactionControlVisibity = Visibility.Collapsed;
			}
			else
			{
				Messages.ShowError(result.Exception.Message);
			}

			IsTransactionControlEnabled = true;

			IsBusy = false;

			_documentPage.Editor.Focus();
		}

		private void WriteDatabaseOutput(string output)
		{
			if (!KeepDatabaseOutputHistory)
			{
				_databaseOutputBuilder.Clear();
			}

			if (!String.IsNullOrEmpty(output))
			{
				_databaseOutputBuilder.AppendLine(output);
			}

			DatabaseOutput = _databaseOutputBuilder.ToString();
		}

		private void UpdateTimerMessage(TimeSpan timeSpan, bool isCanceling)
		{
			string formattedValue;
			if (timeSpan.TotalMilliseconds < 1000)
			{
				formattedValue = $"{(int)timeSpan.TotalMilliseconds} ms";
			}
			else if (timeSpan.TotalMilliseconds < 60000)
			{
				formattedValue = $"{Math.Round(timeSpan.TotalMilliseconds / 1000, 2)} s";
			}
			else
			{
				formattedValue = $"{(int)timeSpan.TotalMinutes:00}:{timeSpan.Seconds:00}";
			}

			if (isCanceling)
			{
				formattedValue = $"Canceling... {formattedValue}";
			}

			StatusInfo.ExecutionTimerMessage = formattedValue;
		}

		private struct ActionResult
		{
			public bool IsSuccessful => Exception == null;

		    public Exception Exception { get; set; }

			public TimeSpan Elapsed { get; set; }
		}

		/*private void SearchTextChangedHandler(object sender, TextChangedEventArgs e)
		{
			var searchedWords = TextSearchHelper.GetSearchedWords(SearchPhraseTextBox.Text);
			Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => ResultGrid.HighlightTextItems(TextSearchHelper.GetRegexPattern(searchedWords))));
		}*/

		private void DataGridTabHeaderMouseEnterHandler(object sender, MouseEventArgs e)
		{
			if (String.IsNullOrWhiteSpace(LastStatementText))
			{
				return;
			}
			
			DataGridTabHeaderPopupTextBox.FontFamily = _documentPage.Editor.FontFamily;
			DataGridTabHeaderPopupTextBox.FontSize = _documentPage.Editor.FontSize;
			DataGridTabHeaderPopup.IsOpen = true;
		}

		private void OutputViewerMouseMoveHandler(object sender, MouseEventArgs e)
		{
			if (!DataGridTabHeaderPopup.IsOpen)
			{
				return;
			}

			var position = e.GetPosition(DataGridTabHeaderPopup.Child);

			if (position.Y < 0 || position.Y > DataGridTabHeaderPopup.Child.RenderSize.Height + DataGridTabHeader.RenderSize.Height || position.X < 0 || position.X > DataGridTabHeaderPopup.Child.RenderSize.Width)
			{
				DataGridTabHeaderPopup.IsOpen = false;
			}
		}

		private void CleanUpVirtualizedItemHandler(object sender, CleanUpVirtualizedItemEventArgs e)
		{
			e.Cancel = DataGridHelper.CanBeRecycled(e.UIElement);
		}

		private void ApplicationDeactivatedHandler(object sender, EventArgs eventArgs)
		{
			DataGridTabHeaderPopup.IsOpen = false;
		}

		private void DataGridTabHeaderPopupMouseLeaveHandler(object sender, MouseEventArgs e)
		{
			DataGridTabHeaderPopup.IsOpen = false;
		}

		private void ButtonDebuggerContinueClickHandler(object sender, RoutedEventArgs e)
		{
		}

		private void ButtonDebuggerStepIntoClickHandler(object sender, RoutedEventArgs e)
		{
		}

		private void ButtonDebuggerStepOverClickHandler(object sender, RoutedEventArgs e)
		{
		}

		private void ButtonDebuggerAbortClickHandler(object sender, RoutedEventArgs e)
		{
		}
	}
}
