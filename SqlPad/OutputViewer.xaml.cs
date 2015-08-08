using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
		private readonly DatabaseProviderConfiguration _providerConfiguration;

		private bool _isRunning;
		private object _previousSelectedTab;

		private CancellationTokenSource _statementExecutionCancellationTokenSource;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IConnectionAdapter ConnectionAdapter { get; }

		public IStatementValidator StatementValidator { get; }

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
				DocumentPage.NotifyExecutionEvent();
			}
		}

		public IReadOnlyList<SessionExecutionStatisticsRecord> SessionExecutionStatistics => _sessionExecutionStatistics;

		public IReadOnlyList<CompilationError> CompilationErrors => _compilationErrors;

		public DocumentPage DocumentPage { get; }

		internal StatementExecutionResult ExecutionResult { get; private set; }

		private EventHandler<DataGridBeginningEditEventArgs> ResultGridBeginningEditCancelTextInputHandler => App.ResultGridBeginningEditCancelTextInputHandlerImplementation;

		public OutputViewer(DocumentPage documentPage)
		{
			InitializeComponent();
			
			_timerExecutionMonitor.Elapsed += delegate { Dispatcher.Invoke(() => UpdateTimerMessage(_stopWatch.Elapsed, IsCancellationRequested)); };

			Application.Current.Deactivated += ApplicationDeactivatedHandler;

			Initialize();

			DocumentPage = documentPage;

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(DocumentPage.CurrentConnection.ProviderName);

			ConnectionAdapter = DocumentPage.DatabaseModel.CreateConnectionAdapter();

			StatementValidator = DocumentPage.InfrastructureFactory.CreateStatementValidator();

			ExecutionPlanViewer = DocumentPage.InfrastructureFactory.CreateExecutionPlanViewer(DocumentPage.DatabaseModel);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;

			TraceViewer = DocumentPage.InfrastructureFactory.CreateTraceViewer(ConnectionAdapter);
			TabTrace.Content = TraceViewer.Control;
		}

		private void SessionExecutionStatisticsFilterHandler(object sender, FilterEventArgs e)
		{
			e.Accepted = ShowAllSessionExecutionStatistics || ((SessionExecutionStatisticsRecord)e.Item).Value != 0;
		}

		private void InitializeResultViewers()
		{
			var tabControlIndex = 0;
			foreach (var resultInfoColumnHeaders in ExecutionResult.ResultInfoColumnHeaders.Where(r => r.Key.Type == ResultIdentifierType.UserDefined))
			{
				var resultViewer = new ResultViewer(this, resultInfoColumnHeaders.Key, resultInfoColumnHeaders.Value);
				var header = new HeaderedContentControl { Content = new AccessText { Text = resultInfoColumnHeaders.Key.ResultIdentifier } };
				var tabItem =
					new TabItem
					{
						Header = header,
						Content = resultViewer
					};

				header.MouseEnter += DataGridTabHeaderMouseEnterHandler;

				tabItem.AddHandler(Selector.SelectedEvent, (RoutedEventHandler)ResultTabSelectedHandler);

				if (ActiveResultViewer == null)
				{
					ActiveResultViewer = resultViewer;
				}

				TabControlResult.Items.Insert(tabControlIndex++, tabItem);
			}

			StatusInfo.ResultGridAvailable = true;
		}

		private void RemoveResultViewers()
		{
			foreach (var item in TabControlResult.Items.Cast<TabItem>().Where(i => i.Content is ResultViewer).ToArray())
			{
				TabControlResult.Items.Remove(item);
			}

			ActiveResultViewer = null;
		}

		private void ResultTabSelectedHandler(object sender, RoutedEventArgs args)
		{
			var tabItem = (TabItem)sender;
			ActiveResultViewer = (ResultViewer)tabItem.Content;
			ResultViewTabHeaderPopup.PlacementTarget = (UIElement)tabItem.Header;
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
			_previousSelectedTab = (TabItem)TabControlResult.SelectedItem;
			SelectDefaultTabIfNeeded();

			_compilationErrors.Clear();
			_sessionExecutionStatistics.Clear();
			TabCompilationErrors.Visibility = Visibility.Collapsed;
			TabStatistics.Visibility = Visibility.Collapsed;
			TabExecutionPlan.Visibility = Visibility.Collapsed;

			StatusInfo.ResultGridAvailable = false;
			StatusInfo.MoreRowsAvailable = false;
			StatusInfo.StatementExecutedSuccessfully = false;
			StatusInfo.AffectedRowCount = -1;
			
			WriteDatabaseOutput(String.Empty);

			LastStatementText = String.Empty;
		}

		private void SelectDefaultTabIfNeeded()
		{
			if (TabControlResult.SelectedItem == null || TabControlResult.SelectedItem.In(TabExecutionPlan, TabStatistics, TabCompilationErrors))
			{
				TabDatabaseOutput.IsSelected = true;
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
			return ExecuteUsingCancellationToken(t => ExecuteExplainPlanAsyncInternal(executionModel));
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
			return ExecuteUsingCancellationToken(t => ExecuteDatabaseCommandAsyncInternal(executionModel));
		}

		private async Task ExecuteDatabaseCommandAsyncInternal(StatementExecutionModel executionModel)
		{
			Initialize();

			ConnectionAdapter.EnableDatabaseOutput = EnableDatabaseOutput;

			var executedAt = DateTime.Now;

			Task<StatementExecutionResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = ConnectionAdapter.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

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

			ExecutionResult = innerTask.Result;

			if (!ExecutionResult.ExecutedSuccessfully)
			{
				NotifyExecutionCanceled();
				return;
			}

			RemoveResultViewers();

			TransactionControlVisibity = ConnectionAdapter.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;

			UpdateTimerMessage(actionResult.Elapsed, false);
			
			WriteDatabaseOutput(ExecutionResult.DatabaseOutput);

			var keepPreviousSelectedTab = false;
			if (ExecutionResult.Statement.GatherExecutionStatistics)
			{
				await ExecutionPlanViewer.ShowActualAsync(ConnectionAdapter, _statementExecutionCancellationTokenSource.Token);
				TabStatistics.Visibility = Visibility.Visible;
				TabExecutionPlan.Visibility = Visibility.Visible;
				_sessionExecutionStatistics.MergeWith(await ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				SelectPreviousTab();
				keepPreviousSelectedTab = _previousSelectedTab.In(TabExecutionPlan, TabStatistics);
			}
			else if (IsPreviousTabAlwaysVisible)
			{
				SelectPreviousTab();
			}

			if (ExecutionResult.CompilationErrors.Count > 0)
			{
				var lineOffset = DocumentPage.Editor.GetLineNumberByOffset(executionModel.ValidationModel.Statement.SourcePosition.IndexStart);
				foreach (var error in ExecutionResult.CompilationErrors)
				{
					error.Line += lineOffset;
					_compilationErrors.Add(error);
				}

				ShowCompilationErrors();
			}

			if (ExecutionResult.ResultInfoColumnHeaders.Count == 0)
			{
				if (ExecutionResult.AffectedRowCount == -1)
				{
					StatusInfo.StatementExecutedSuccessfully = true;
				}
				else
				{
					StatusInfo.AffectedRowCount = ExecutionResult.AffectedRowCount;
				}

				return;
			}

			InitializeResultViewers();

			if (!keepPreviousSelectedTab)
			{
				TabControlResult.SelectedIndex = 0;
			}
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

		private void CellHyperlinkExpandChildRecordsClickHandler(object sender, RoutedEventArgs args)
		{
			var hyperlink = (Hyperlink)sender;
			var dataSource = (IReferenceDataSource)hyperlink.DataContext;
			var cell = (DataGridCell)hyperlink.Tag;
			var row = cell.FindParentVisual<DataGridRow>();
			var currentRowValues = (object[])row.DataContext;
			var keyValues = dataSource.ColumnHeaders.Select(h => currentRowValues[h.ColumnIndex]).ToArray();

			DataGridHelper.BuildDataGridCellContent(cell, t => BuildChildRecordDataGrid(dataSource, keyValues, t));
		}

		private async Task<FrameworkElement> BuildChildRecordDataGrid(IReferenceDataSource dataSource, object[] keyValues, CancellationToken cancellationToken)
		{
			var executionModel = dataSource.CreateExecutionModel(keyValues);
			var executionResult = await ConnectionAdapter.ExecuteChildStatementAsync(executionModel, cancellationToken);
			var childReferenceDataSources = await StatementValidator.ApplyReferenceConstraintsAsync(executionResult, ConnectionAdapter.DatabaseModel, cancellationToken);
			var resultInfo = executionResult.ResultInfoColumnHeaders.Keys.Last();
            var resultSet = await ConnectionAdapter.FetchRecordsAsync(resultInfo, StatementExecutionModel.DefaultRowBatchSize, cancellationToken);

			var childRecordDataGrid =
				new DataGrid
				{
					RowHeaderWidth = 0,
					Style = (Style)Application.Current.Resources["ResultSetDataGrid"],
					ItemsSource = new ObservableCollection<object[]>(resultSet)
				};

			childRecordDataGrid.AddHandler(VirtualizingStackPanel.CleanUpVirtualizedItemEvent, (CleanUpVirtualizedItemEventHandler)CleanUpVirtualizedItemHandler);
			childRecordDataGrid.BeginningEdit += App.ResultGridBeginningEditCancelTextInputHandlerImplementation;
			childRecordDataGrid.MouseDoubleClick += ResultGridMouseDoubleClickHandler;

			var columnHeaders = executionResult.ResultInfoColumnHeaders.Values.Last();
			DataGridHelper.InitializeDataGridColumns(childRecordDataGrid, columnHeaders, StatementValidator, ConnectionAdapter);
			AddChildReferenceColumns(childRecordDataGrid, childReferenceDataSources);

			foreach (var columnTemplate in childRecordDataGrid.Columns)
			{
				columnTemplate.HeaderStyle = (Style)Application.Current.Resources["ColumnHeaderClickBubbleCancelation"];
			}

			return childRecordDataGrid;
		}

		private void NotifyExecutionCanceled()
		{
			StatusInfo.ExecutionTimerMessage = "Canceled";
		}

		internal async Task ExecuteUsingCancellationToken(Func<CancellationToken, Task> function)
		{
			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				await function(_statementExecutionCancellationTokenSource.Token);

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private void TabControlResultGiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
		{
			e.Handled = true;
		}

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			var senderDataGrid = (DataGrid)sender;
			var originalDataGrid = ((Visual)e.OriginalSource).FindParentVisual<DataGrid>();
			if (Equals(originalDataGrid, senderDataGrid))
			{
				DataGridHelper.ShowLargeValueEditor(senderDataGrid);
			}
		}

		private bool IsTabAlwaysVisible(object tabItem)
		{
			return ((TabItem)tabItem).Content is ResultViewer || tabItem.In(TabDatabaseOutput, TabTrace);
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

		internal async Task UpdateExecutionStatisticsIfEnabled()
		{
			if (ExecutionResult.Statement.GatherExecutionStatistics)
			{
				_sessionExecutionStatistics.MergeWith(await ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
			}
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
			ConnectionAdapter.Dispose();
		}

		private async void ButtonCommitTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteTransactionOperation(ConnectionAdapter.CommitTransaction);
		}

		private async void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteTransactionOperation(ConnectionAdapter.RollbackTransaction);
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

			DocumentPage.Editor.Focus();
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
			
			DataGridTabHeaderPopupTextBox.FontFamily = DocumentPage.Editor.FontFamily;
			DataGridTabHeaderPopupTextBox.FontSize = DocumentPage.Editor.FontSize;
			ResultViewTabHeaderPopup.IsOpen = true;
		}

		private void OutputViewerMouseMoveHandler(object sender, MouseEventArgs e)
		{
			if (!ResultViewTabHeaderPopup.IsOpen)
			{
				return;
			}

			var selectedItem = (TabItem)TabControlResult.SelectedItem;
			var isResultViewer = selectedItem.Content is ResultViewer;
			if (!isResultViewer)
			{
				ResultViewTabHeaderPopup.IsOpen = false;
				return;
			}

			var resultViewTabHeader = (HeaderedContentControl)selectedItem.Header;
			var position = e.GetPosition(ResultViewTabHeaderPopup.Child);
			if (position.Y < 0 || position.Y > ResultViewTabHeaderPopup.Child.RenderSize.Height + resultViewTabHeader.RenderSize.Height || position.X < 0 || position.X > ResultViewTabHeaderPopup.Child.RenderSize.Width)
			{
				ResultViewTabHeaderPopup.IsOpen = false;
			}
		}

		private void CleanUpVirtualizedItemHandler(object sender, CleanUpVirtualizedItemEventArgs e)
		{
			e.Cancel = DataGridHelper.CanBeRecycled(e.UIElement);
		}

		private void ApplicationDeactivatedHandler(object sender, EventArgs eventArgs)
		{
			ResultViewTabHeaderPopup.IsOpen = false;
		}

		private void DataGridTabHeaderPopupMouseLeaveHandler(object sender, MouseEventArgs e)
		{
			ResultViewTabHeaderPopup.IsOpen = false;
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
