﻿using System;
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
using System.Windows.Input;
using System.Windows.Threading;

namespace SqlPad
{
	[DebuggerDisplay("OutputViewer (Title={Title})")]
	public partial class OutputViewer : IDisposable
	{
		private const string ExecutionLogTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

		private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(1);

		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly StringBuilder _databaseOutputBuilder = new StringBuilder();
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
		private readonly StringBuilder _executionLogBuilder = new StringBuilder();
		private readonly DispatcherTimer _timerExecutionMonitor;
		private readonly DatabaseProviderConfiguration _providerConfiguration;

		private bool _isRunning;
		private object _previousSelectedTab;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;
		private StatementExecutionBatchResult _executionResult;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IConnectionAdapter ConnectionAdapter { get; }

		public IStatementValidator StatementValidator { get; }

		public IExecutionPlanViewer ExecutionPlanViewer { get; }

		public ITraceViewer TraceViewer { get; }

		public FileResultViewer FileResultViewer { get; }

		private bool IsCancellationRequested
		{
			get
			{
				var cancellationTokenSource = _statementExecutionCancellationTokenSource;
				return cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested;
			}
		}

		private IEnumerable<DataGridResultViewer> DataGridResultViewers =>
			TabControlResult.Items
				.Cast<TabItem>()
				.Select(t => t.Content)
				.OfType<DataGridResultViewer>();

		public bool KeepDatabaseOutputHistory { get; set; }

		public StatusInfoModel StatusInfo { get; } = new StatusInfoModel();

		public bool IsBusy
		{
			get { return _isRunning; }
			private set
			{
				_isRunning = value;
				IsTransactionControlEnabled = !value;
				DocumentPage.NotifyExecutionEvent();
			}
		}

		public IReadOnlyList<SessionExecutionStatisticsRecord> SessionExecutionStatistics => _sessionExecutionStatistics;

		public IReadOnlyList<CompilationError> CompilationErrors => _compilationErrors;

		public DocumentPage DocumentPage { get; }

		private EventHandler<DataGridBeginningEditEventArgs> ResultGridBeginningEditCancelTextInputHandler => App.DataGridBeginningEditCancelTextInputHandlerImplementation;

		public OutputViewer(DocumentPage documentPage)
		{
			DocumentPage = documentPage;

			FileResultViewer = new FileResultViewer(this);

			InitializeComponent();

			_timerExecutionMonitor = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
			_timerExecutionMonitor.Tick += TimerExecutionMonitorTickHandler;

			Application.Current.Deactivated += ApplicationDeactivatedHandler;

			Initialize();

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(DocumentPage.CurrentConnection.ProviderName);

			ConnectionAdapter = DocumentPage.DatabaseModel.CreateConnectionAdapter();

			StatementValidator = DocumentPage.InfrastructureFactory.CreateStatementValidator();

			ExecutionPlanViewer = DocumentPage.InfrastructureFactory.CreateExecutionPlanViewer(this);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;

			TraceViewer = DocumentPage.InfrastructureFactory.CreateTraceViewer(ConnectionAdapter);
			TabTrace.Content = TraceViewer.Control;

			BreakOnExceptions = DocumentPage.WorkDocument.BreakOnExceptions;
		}

		private void TimerExecutionMonitorTickHandler(object sender, EventArgs e)
		{
			if (Equals(DocumentPage.ActiveOutputViewer, this))
			{
				UpdateTimerMessage(_stopWatch.Elapsed, IsCancellationRequested);
			}
		}

		private void SessionExecutionStatisticsFilterHandler(object sender, FilterEventArgs e)
		{
			e.Accepted = ShowAllSessionExecutionStatistics || ((SessionExecutionStatisticsRecord)e.Item).Value != 0;
		}

		private async Task InitializeResultViewers()
		{
			var tabControlIndex = 0;
			foreach (var statementResult in _executionResult.StatementResults)
			{
				foreach (var resultInfoColumnHeaders in statementResult.ResultInfoColumnHeaders.Where(r => r.Key.Type == ResultIdentifierType.UserDefined))
				{
					var resultViewer = CreateResultViewer(statementResult, resultInfoColumnHeaders.Key);
					await resultViewer.Initialize();

					if (ActiveResultViewer == null)
					{
						ActiveResultViewer = resultViewer;
					}

					TabControlResult.Items.Insert(tabControlIndex++, resultViewer.TabItem);
				}
			}
		}

		private DataGridResultViewer CreateResultViewer(StatementExecutionResult statementResult, ResultInfo resultInfo)
		{
			var refreshInterval = DocumentPage.WorkDocument.RefreshInterval;
			if (refreshInterval == TimeSpan.Zero)
			{
				refreshInterval = DefaultRefreshInterval;
			}

			var resultViewer =
				new DataGridResultViewer(this, statementResult, resultInfo)
				{
					AutoRefreshInterval = refreshInterval
				};

			resultViewer.TabItem.AddHandler(Selector.SelectedEvent, (RoutedEventHandler)ResultTabSelectedHandler);

			return resultViewer;
		}

		private void RemoveResultViewers()
		{
			var resultViewers = TabControlResult.Items
				.Cast<TabItem>()
				.Select(i => i.Content as DataGridResultViewer)
				.Where(v => v != null)
				.ToArray();

			foreach (var item in resultViewers)
			{
				item.Close();
			}

			if (ActiveResultViewer == null)
			{
				return;
			}

			DocumentPage.WorkDocument.RefreshInterval = ActiveResultViewer.AutoRefreshInterval;
			ActiveResultViewer = null;
		}

		private void ResultTabSelectedHandler(object sender, RoutedEventArgs args)
		{
			var tabItem = (TabItem)sender;
			ActiveResultViewer = (DataGridResultViewer)tabItem.Content;
			ActiveResultViewer.ResultViewTabHeaderPopup.PlacementTarget = (UIElement)tabItem.Header;
		}

		public void CancelUserAction()
		{
			var cancellationTokenSource = _statementExecutionCancellationTokenSource;
			if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
			{
				cancellationTokenSource.Cancel();
			}
		}

		private void InitializeCommon()
		{
			_previousSelectedTab = (TabItem)TabControlResult.SelectedItem;
			SelectDefaultTabIfNeeded();

			_compilationErrors.Clear();
			_sessionExecutionStatistics.Clear();
		}

		private void Initialize()
		{
			InitializeCommon();

			TabExecutionPlan.Visibility = Visibility.Collapsed;

			StatusInfo.ResultGridAvailable = false;
			StatusInfo.MoreRowsAvailable = false;
			StatusInfo.SuccessfulExecutionMessage = null;

			RemoveResultViewers();

			WriteDatabaseOutput(String.Empty);
		}

		private void SelectDefaultTabIfNeeded()
		{
			if (TabControlResult.SelectedItem.In(null, TabExecutionPlan, TabStatistics, TabCompilationErrors, TabDebugger) ||
				((TabItem)TabControlResult.SelectedItem).Content is DataGridResultViewer)
			{
				TabExecutionLog.IsSelected = true;
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

		public Task ExecuteExplainPlanAsync(StatementExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(t => ExecuteExplainPlanAsyncInternal(executionModel));
		}

		private async Task ExecuteExplainPlanAsyncInternal(StatementExecutionModel executionModel)
		{
			InitializeCommon();

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
					var errorMessage = Messages.GetExceptionErrorMessage(actionResult.Exception);
					AddExecutionLog(actionResult.ExecutedAt, $"Explain plain failed: {errorMessage}");
					Messages.ShowError(errorMessage);
				}
			}
		}

		public Task ExecuteDatabaseCommandAsync(StatementBatchExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(t => ExecuteDatabaseCommandAsyncInternal(executionModel));
		}

		private async Task ExecuteDatabaseCommandAsyncInternal(StatementBatchExecutionModel executionModel)
		{
			if (executionModel.Statements.Count == 0)
			{
				return;
			}

			var beforeExecutionText = DocumentPage.Editor.Text;

			Initialize();

			FileResultViewer.Initialize();

			ConnectionAdapter.EnableDatabaseOutput = EnableDatabaseOutput;

			Task<StatementExecutionBatchResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = ConnectionAdapter.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			HasActiveTransaction = ConnectionAdapter.HasActiveTransaction;
			TransactionIdentifier = ConnectionAdapter.TransanctionIdentifier;

			if (!actionResult.IsSuccessful)
			{
				if (actionResult.Exception is StatementExecutionException executionException)
				{
					UpdateExecutionLog(executionException.BatchResult.StatementResults);
					WriteDatabaseOutput(executionException.BatchResult.DatabaseOutput);

					var lastStatementResult = executionException.BatchResult.StatementResults.Last();
					var errorPosition = lastStatementResult.ErrorPosition;
					if (errorPosition.HasValue && String.Equals(beforeExecutionText, DocumentPage.Editor.Text))
					{
						DocumentPage.Editor.CaretOffset = lastStatementResult.StatementModel.Statement.RootNode.SourcePosition.IndexStart + errorPosition.Value;
					}
				}

				Messages.ShowError(actionResult.Exception.Message);
				return;
			}

			_executionResult = innerTask.Result;

			if (ConnectionAdapter.DebuggerSession != null)
			{
				DebuggerViewer.Initialize(this, ConnectionAdapter.DebuggerSession);
				ConnectionAdapter.DebuggerSession.Attached += delegate { Dispatcher.Invoke(DebuggerSessionSynchronizedHandler); };
				ConnectionAdapter.DebuggerSession.Detached += DebuggerSessionDetachedHandler;
				var exception = await App.SafeActionAsync(() => ConnectionAdapter.DebuggerSession.Start(_statementExecutionCancellationTokenSource.Token));
				if (exception != null)
				{
					Messages.ShowError(exception.Message);
				}

				return;
			}

			UpdateExecutionLog(_executionResult.StatementResults);

			UpdateHistoryEntries();

			if (_executionResult.StatementResults.Last().ExecutedSuccessfully == false)
			{
				NotifyExecutionCanceled();
				return;
			}

			UpdateTimerMessage(actionResult.Elapsed, false);

			await DisplayExecutionResult();
		}

		private void DebuggerSessionDetachedHandler(object sender, EventArgs args)
		{
			Dispatcher.BeginInvoke(new Action(async () => await DisplayExecutionResult()));
		}

		private async Task DisplayExecutionResult()
		{
			WriteDatabaseOutput(_executionResult.DatabaseOutput);

			var keepPreviousSelectedTab = false;
			if (_executionResult.ExecutionModel.GatherExecutionStatistics)
			{
				await ExecutionPlanViewer.ShowActualAsync(ConnectionAdapter, _statementExecutionCancellationTokenSource.Token);
				TabExecutionPlan.Visibility = Visibility.Visible;
				_sessionExecutionStatistics.MergeWith(await ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				SelectPreviousTab();
				keepPreviousSelectedTab = _previousSelectedTab.In(TabExecutionPlan, TabStatistics);
			}
			else if (IsPreviousTabAlwaysVisible)
			{
				SelectPreviousTab();
			}

			var lastStatementResult = _executionResult.StatementResults.Last();
			if (lastStatementResult.CompilationErrors.Count > 0)
			{
				var lineOffset = DocumentPage.Editor.GetLineNumberByOffset(lastStatementResult.StatementModel.ValidationModel.Statement.SourcePosition.IndexStart);
				foreach (var error in lastStatementResult.CompilationErrors)
				{
					error.Line += lineOffset;
					_compilationErrors.Add(error);
				}

				TabControlResult.SelectedItem = TabCompilationErrors;
			}

			if (DataOutputType == DataOutputType.File)
			{
				await FileResultViewer.SaveExecutionResult(_executionResult);
				if (!keepPreviousSelectedTab)
				{
					TabControlResult.SelectedIndex = 0;
				}
			}
			else
			{
				await InitializeResultViewers();

				if (DataGridResultViewers.Any())
				{
					StatusInfo.ResultGridAvailable = true;

					if (!keepPreviousSelectedTab)
					{
						TabControlResult.SelectedIndex = 0;
					}
				}
				else
				{
					StatusInfo.SuccessfulExecutionMessage = lastStatementResult.SuccessfulExecutionMessage;
				}
			}
		}

		private void DebuggerSessionSynchronizedHandler()
		{
			IsDebuggerControlVisible = true;
			TabDebugger.IsSelected = true;
		}

		private void UpdateHistoryEntries()
		{
			var maximumHistoryEntrySize = ConfigurationProvider.Configuration.Miscellaneous.MaximumHistoryEntrySize;

			foreach (var statementResult in _executionResult.StatementResults.Where(r => r.ExecutedAt.HasValue))
			{
				var executionHistoryRecord = new StatementExecutionHistoryEntry(statementResult.StatementModel.StatementText, statementResult.ExecutedAt.Value);

				if (executionHistoryRecord.StatementText.Length <= maximumHistoryEntrySize)
				{
					_providerConfiguration.AddStatementExecution(executionHistoryRecord);
				}
				else
				{
					TraceLog.WriteLine($"Executed statement not stored in the execution history. The maximum allowed size is {maximumHistoryEntrySize} characters while the statement has {executionHistoryRecord.StatementText.Length} characters. ");
				}
			}
		}

		private void UpdateExecutionLog(IEnumerable<StatementExecutionResult> statementResults)
		{
			foreach (var executionResult in statementResults)
			{
				if (executionResult.ExecutedAt.HasValue)
				{
					_executionLogBuilder.Append(executionResult.ExecutedAt.Value.ToString(ExecutionLogTimestampFormat));
					_executionLogBuilder.Append(" - ");
				}

				if (executionResult.ExecutedSuccessfully)
				{
					_executionLogBuilder.Append(executionResult.SuccessfulExecutionMessage);
					_executionLogBuilder.Append("(");
					_executionLogBuilder.Append(executionResult.Duration.Value.ToPrettyString());
					_executionLogBuilder.Append(")");
				}
				else
				{
					_executionLogBuilder.Append("Command execution failed");

					if (executionResult.Duration.HasValue)
					{
						_executionLogBuilder.Append(" (");
						_executionLogBuilder.Append(executionResult.Duration.Value.ToPrettyString());
						_executionLogBuilder.Append(")");
					}

					_executionLogBuilder.Append(": ");
					_executionLogBuilder.Append(executionResult.Exception.Message);
				}

				_executionLogBuilder.AppendLine();
			}

			ExecutionLog = _executionLogBuilder.ToString();
		}

		public void AddExecutionLog(DateTime timestamp, string message)
		{
			_executionLogBuilder.Append(timestamp.ToString(ExecutionLogTimestampFormat));
			_executionLogBuilder.Append(" - ");
			_executionLogBuilder.AppendLine(message);
			ExecutionLog = _executionLogBuilder.ToString();
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

		private bool IsTabAlwaysVisible(object tabItem)
		{
			return ((TabItem)tabItem).Content is DataGridResultViewer || tabItem.In(TabExecutionLog, TabDatabaseOutput, TabTrace);
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
			if (_executionResult.ExecutionModel.GatherExecutionStatistics)
			{
				_sessionExecutionStatistics.MergeWith(await ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
			}
		}

		private async Task<ActionResult> SafeTimedActionAsync(Func<Task> action)
		{
			var actionResult = new ActionResult();

			_stopWatch.Restart();
			_timerExecutionMonitor.Start();

			actionResult.ExecutedAt = DateTime.Now;
			actionResult.Exception = await App.SafeActionAsync(action);
			actionResult.Elapsed = _stopWatch.Elapsed;

			_timerExecutionMonitor.Stop();
			_stopWatch.Stop();

			return actionResult;
		}

		public void Dispose()
		{
			RemoveResultViewers();
			Application.Current.Deactivated -= ApplicationDeactivatedHandler;
			_timerExecutionMonitor.Tick -= TimerExecutionMonitorTickHandler;
			_timerExecutionMonitor.Stop();
		}

		private async void ButtonCommitTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteTransactionOperation(ConnectionAdapter.CommitTransaction, "Commit");
		}

		private async void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteTransactionOperation(ConnectionAdapter.RollbackTransaction, "Rollback");
		}

		private async Task ExecuteTransactionOperation(Func<Task> transactionOperation, string operation)
		{
			IsBusy = true;

			var result = await SafeTimedActionAsync(transactionOperation);

			UpdateTimerMessage(result.Elapsed, false);

			IsBusy = false;

			var elapsedString = result.Elapsed.ToPrettyString();
			if (result.IsSuccessful)
			{
				HasActiveTransaction = false;
				TransactionIdentifier = null;
				var statusMessage = $"{operation} complete. ";
				AddExecutionLog(result.ExecutedAt, $"{statusMessage}({elapsedString})");
				StatusInfo.SuccessfulExecutionMessage = statusMessage;

				DocumentPage.Editor.Focus();
			}
			else
			{
				AddExecutionLog(result.ExecutedAt, $"{operation} failed ({elapsedString}): {result.Exception.Message}");
				Messages.ShowError(result.Exception.Message);
			}
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
			var formattedValue = timeSpan.ToPrettyString();

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

			public DateTime ExecutedAt { get; set; }
		}

		private void OutputViewerMouseMoveHandler(object sender, MouseEventArgs e)
		{
			var selectedItem = (TabItem)TabControlResult.SelectedItem;
			var resultViewer = selectedItem.Content as DataGridResultViewer;
			if (resultViewer == null)
			{
				return;
			}

			if (!resultViewer.ResultViewTabHeaderPopup.IsOpen)
			{
				return;
			}

			var resultViewTabHeader = (HeaderedContentControl)selectedItem.Header;
			var position = e.GetPosition(resultViewer.ResultViewTabHeaderPopup.Child);
			if (position.Y < 0 || position.Y > resultViewer.ResultViewTabHeaderPopup.Child.RenderSize.Height + resultViewTabHeader.RenderSize.Height || position.X < 0 || position.X > resultViewer.ResultViewTabHeaderPopup.Child.RenderSize.Width)
			{
				resultViewer.ResultViewTabHeaderPopup.IsOpen = false;
			}
		}

		private void ApplicationDeactivatedHandler(object sender, EventArgs eventArgs)
		{
			foreach (var viewer in DataGridResultViewers)
			{
				viewer.ResultViewTabHeaderPopup.IsOpen = false;
			}
		}

		private void ClearExecutionLogHandler(object sender, RoutedEventArgs e)
		{
			_executionLogBuilder.Clear();
			ExecutionLog = String.Empty;
		}

		private async Task ExecuteDebuggerAction(Task debuggerAction)
		{
			IsDebuggerControlEnabled = false;
			var exception = await App.SafeActionAsync(() => debuggerAction);

			if (ConnectionAdapter.DebuggerSession == null)
			{
				IsDebuggerControlVisible = false;
				SelectDefaultTabIfNeeded();
				DocumentPage.Editor.Focus();
			}
			else
			{
				await DebuggerViewer.Refresh(_statementExecutionCancellationTokenSource.Token);
			}

			IsDebuggerControlEnabled = true;

			if (exception != null)
			{
				Messages.ShowError(exception.Message);
			}
		}

		private async void ButtonDebuggerContinueClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteUsingCancellationToken(t => ExecuteDebuggerAction(ConnectionAdapter.DebuggerSession.Continue(t)));
		}

		private async void DebuggerStepIntoExecutedHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteUsingCancellationToken(t => ExecuteDebuggerAction(ConnectionAdapter.DebuggerSession.StepInto(t)));
		}

		private async void DebuggerStepOverExecutedHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteUsingCancellationToken(t => ExecuteDebuggerAction(ConnectionAdapter.DebuggerSession.StepOver(t)));
		}

		private async void ButtonDebuggerAbortClickHandler(object sender, RoutedEventArgs e)
		{
			await ExecuteUsingCancellationToken(t => ExecuteDebuggerAction(ConnectionAdapter.DebuggerSession.Abort(t)));
		}

		private void DebuggerActionCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ConnectionAdapter?.DebuggerSession != null;
		}
	}
}
