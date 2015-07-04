using System;
using System.Collections.Generic;
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
using System.Windows.Input;
using Microsoft.Win32;
using Timer = System.Timers.Timer;

namespace SqlPad
{
	public partial class OutputViewer : IDisposable
	{
		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();

		private bool _isSelectingCells;
		private bool _hasExecutionResult;
		private object _previousSelectedTab;
		private DocumentPage _documentPage;
		private StatementExecutionResult _executionResult;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IExecutionPlanViewer ExecutionPlanViewer { get; private set; }

		public IConnectionAdapter ConnectionAdapter { get; private set; }

		private bool IsCancellationRequested
		{
			get
			{
				var cancellationTokenSource = _statementExecutionCancellationTokenSource;
				return cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested;
			}
		}

		public OutputViewerModel ViewModel
		{
			get { return (OutputViewerModel)DataContext; }
		}

		public bool IsBusy
		{
			get { return _documentPage.ViewModel.IsRunning; }
			private set
			{
				_documentPage.ViewModel.IsRunning = value;
				App.MainWindow.NotifyTaskStatus();
			}
		}

		public OutputViewer()
		{
			InitializeComponent();
			DataContext = new OutputViewerModel();
			
			_timerExecutionMonitor.Elapsed += delegate { Dispatcher.Invoke(UpdateTimerMessage); };
		}

		public void DisplayResult(StatementExecutionResult executionResult)
		{
			_executionResult = executionResult;
			_hasExecutionResult = true;
			ResultGrid.Columns.Clear();

			foreach (var columnHeader in _executionResult.ColumnHeaders)
			{
				var columnTemplate = CreateDataGridTextColumnTemplate(columnHeader);
				ResultGrid.Columns.Add(columnTemplate);
			}

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.Column;

			ViewModel.GridRowInfoVisibility = Visibility.Visible;
			ViewModel.ResultRowItems.Clear();

			AppendRows(executionResult.InitialResultSet);
		}

		internal static DataGridTextColumn CreateDataGridTextColumnTemplate(ColumnHeader columnHeader)
		{
			var columnTemplate =
				new DataGridTextColumn
				{
					Header = columnHeader.Name.Replace("_", "__"),
					Binding = new Binding(String.Format("[{0}]", columnHeader.ColumnIndex)) { Converter = CellValueConverter.Instance },
					EditingElementStyle = (Style)Application.Current.Resources["CellTextBoxStyleReadOnly"]
				};

			if (columnHeader.DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte)))
			{
				columnTemplate.HeaderStyle = (Style)Application.Current.Resources["HeaderStyleRightAlign"];
				columnTemplate.CellStyle = (Style)Application.Current.Resources["CellStyleRightAlign"];
			}

			return columnTemplate;
		}

		internal static void ShowLargeValueEditor(DataGrid dataGrid)
		{
			var currentRow = (object[])dataGrid.CurrentItem;
			if (currentRow == null || dataGrid.CurrentColumn == null)
				return;

			var cellValue = currentRow[dataGrid.CurrentColumn.DisplayIndex];
			var largeValue = cellValue as ILargeValue;
			if (largeValue != null)
			{
				new LargeValueEditor(dataGrid.CurrentColumn.Header.ToString(), largeValue) { Owner = Window.GetWindow(dataGrid) }.ShowDialog();
			}
		}

		public void Setup(DocumentPage documentPage)
		{
			_documentPage = documentPage;

			ConnectionAdapter = _documentPage.DatabaseModel.CreateConnectionAdapter();

			ExecutionPlanViewer = _documentPage.InfrastructureFactory.CreateExecutionPlanViewer(documentPage.DatabaseModel);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;
		}

		public void Cancel()
		{
			var cancellationTokenSource = _statementExecutionCancellationTokenSource;
			if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
			{
				cancellationTokenSource.Cancel();
			}
		}

		public void Initialize()
		{
			_hasExecutionResult = false;

			ViewModel.AffectedRowCount = -1;
			ViewModel.CurrentRowIndex = 0;
			ViewModel.ResultRowItems.Clear();
			ViewModel.CompilationErrors.Clear();
			ViewModel.MoreRowsExistVisibility = Visibility.Collapsed;
			ViewModel.GridRowInfoVisibility = Visibility.Collapsed;
			ViewModel.ExecutionPlanAvailable = Visibility.Collapsed;
			ViewModel.StatementExecutedSuccessfullyStatusMessageVisibility = Visibility.Collapsed;
			ViewModel.SessionExecutionStatistics.Clear();
			ViewModel.WriteDatabaseOutput(String.Empty);

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.None;

			_previousSelectedTab = TabControlResult.SelectedItem;

			SelectDefaultTabIfNeeded();
		}

		public void SelectDefaultTabIfNeeded()
		{
			if (!IsTabAlwaysVisible(TabControlResult.SelectedItem))
			{
				TabControlResult.SelectedItem = TabResultSet;
			}
		}

		public bool IsPreviousTabAlwaysVisible
		{
			get { return _previousSelectedTab != null && IsTabAlwaysVisible(_previousSelectedTab); }
		}
		
		public void SelectPreviousTab()
		{
			if (_previousSelectedTab != null)
			{
				TabControlResult.SelectedItem = _previousSelectedTab;
			}
		}

		public void ShowExecutionPlan()
		{
			TabControlResult.SelectedItem = TabExecutionPlan;
		}

		public void ShowCompilationErrors()
		{
			TabControlResult.SelectedItem = TabCompilationErrors;
		}

		public async Task<ActionResult> RollbackTransactionAsync()
		{
			IsBusy = true;

			var result = await SafeTimedActionAsync(ConnectionAdapter.RollbackTransaction);
			ViewModel.UpdateTimerMessage(result.Elapsed, false);
			
			IsBusy = false;

			return result;
		}

		public Task ExecuteExplainPlanAsync(StatementExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(() => ExecuteExplainPlanAsyncInternal(executionModel));
		}

		public async Task ExecuteExplainPlanAsyncInternal(StatementExecutionModel executionModel)
		{
			SelectDefaultTabIfNeeded();

			ViewModel.ExecutionPlanAvailable = Visibility.Collapsed;

			var actionResult = await SafeTimedActionAsync(() => ExecutionPlanViewer.ExplainAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (_statementExecutionCancellationTokenSource.Token.IsCancellationRequested)
			{
				ViewModel.NotifyExecutionCanceled();
			}
			else
			{
				ViewModel.UpdateTimerMessage(actionResult.Elapsed, false);

				if (actionResult.IsSuccessful)
				{
					ViewModel.ExecutionPlanAvailable = Visibility.Visible;
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

			Task<StatementExecutionResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = ConnectionAdapter.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(actionResult.Exception.Message);
				return;
			}

			var executionResult = innerTask.Result;
			if (!executionResult.ExecutedSuccessfully)
			{
				ViewModel.NotifyExecutionCanceled();
				return;
			}

			_documentPage.ViewModel.TransactionControlVisibity = ConnectionAdapter.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;
			ViewModel.UpdateTimerMessage(actionResult.Elapsed, false);
			ViewModel.WriteDatabaseOutput(executionResult.DatabaseOutput);

			if (executionResult.Statement.GatherExecutionStatistics)
			{
				await ExecutionPlanViewer.ShowActualAsync(ConnectionAdapter, _statementExecutionCancellationTokenSource.Token);
				ViewModel.ExecutionPlanAvailable = Visibility.Visible;
				ViewModel.SessionExecutionStatistics.MergeWith(await ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				SelectPreviousTab();
			}
			else if (IsPreviousTabAlwaysVisible)
			{
				SelectPreviousTab();
			}

			if (executionResult.CompilationErrors.Count > 0)
			{
				var lineOffset = _documentPage.Editor.GetLineNumberByOffset(executionModel.Statement.SourcePosition.IndexStart);
				foreach (var error in executionResult.CompilationErrors)
				{
					error.Line += lineOffset;
					ViewModel.CompilationErrors.Add(error);
				}

				ShowCompilationErrors();
			}

			if (executionResult.ColumnHeaders.Count == 0)
			{
				if (executionResult.AffectedRowCount == -1)
				{
					ViewModel.StatementExecutedSuccessfullyStatusMessageVisibility = Visibility.Visible;
				}
				else
				{
					ViewModel.AffectedRowCount = executionResult.AffectedRowCount;
				}

				return;
			}

			DisplayResult(executionResult);
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

		private const string ExportClassTemplate =
@"using System;
using System.Data;

public class Query
{{
	private IDbConnection _connection;

	private const string CommandText =
@""{0}"";
{1}	
	private IEnumerable<ResultRow> Execute()
	{{
		using (var command = _connection.CreateCommand())
		{{
			command.CommandText = CommandText;
			{2}			_connection.Open();

			using (var reader = command.ExecuteReader())
			{{
				while (reader.Read())
				{{
					var row =
						new ResultRow
						{{
{3}
						}};

					yield return row;
				}}
			}}

			_connection.Close();
		}}
	}}

	private static T GetReaderValue<T>(object value)
	{{
		return value == DBNull.Value
			? default(T)
			: (T)value;
	}}
}}
";

		private void GenerateCSharpQuery(object sender, ExecutedRoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "C# files (*.cs)|*.cs|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			var columnMapBuilder = new StringBuilder();
			var resultRowPropertyBuilder = new StringBuilder();
			var bindVariableBuilder = new StringBuilder();
			var parameterBuilder = new StringBuilder();

			if (_executionResult.Statement.BindVariables.Count > 0)
			{
				bindVariableBuilder.AppendLine();
				parameterBuilder.AppendLine();
				
				foreach (var bindVariable in _executionResult.Statement.BindVariables)
				{
					bindVariableBuilder.Append("\tpublic ");
					bindVariableBuilder.Append(bindVariable.InputType);
					bindVariableBuilder.Append(" ");
					bindVariableBuilder.Append(bindVariable.Name);
					bindVariableBuilder.AppendLine(" { get; set; }");

					var parameterName = String.Format("parameter{0}", bindVariable.Name);
					parameterBuilder.Append("\t\t\tvar ");
					parameterBuilder.Append(parameterName);
					parameterBuilder.AppendLine(" = command.CreateParameter();");
					parameterBuilder.Append("\t\t\t");
					parameterBuilder.Append(parameterName);
					parameterBuilder.Append(".Value = ");
					parameterBuilder.Append(bindVariable.Name);
					parameterBuilder.AppendLine(";");
					parameterBuilder.Append("\t\t\tcommand.Parameters.Add(");
					parameterBuilder.Append(parameterName);
					parameterBuilder.AppendLine(");");
					parameterBuilder.AppendLine();
				}
			}

			var index = 0;
			foreach (var column in _executionResult.ColumnHeaders)
			{
				index++;

				var dataTypeName = String.Equals(column.DataType.Namespace, "System")
					? column.DataType.Name
					: column.DataType.FullName;

				if (column.DataType.IsValueType)
				{
					dataTypeName = String.Format("{0}?", dataTypeName);
				}

				columnMapBuilder.Append("\t\t\t\t\t\t\t");
				columnMapBuilder.Append(column.Name);
				columnMapBuilder.Append(" = GetReaderValue<");
				columnMapBuilder.Append(dataTypeName);
				columnMapBuilder.Append(">(reader[\"");
				columnMapBuilder.Append(column.Name);
				columnMapBuilder.Append("\"])");

				if (index < ResultGrid.Columns.Count)
				{
					columnMapBuilder.AppendLine(",");
				}

				resultRowPropertyBuilder.Append("\tpublic ");
				resultRowPropertyBuilder.Append(dataTypeName);
				resultRowPropertyBuilder.Append(" ");
				resultRowPropertyBuilder.Append(column.Name);
				resultRowPropertyBuilder.AppendLine(" { get; set; }");
			}

			var statementText = _executionResult.Statement.StatementText.Replace("\"", "\"\"");
			var queryClass = String.Format(ExportClassTemplate, statementText, bindVariableBuilder, parameterBuilder, columnMapBuilder);

			using (var writer = File.CreateText(dialog.FileName))
			{
				writer.WriteLine(queryClass);
				writer.WriteLine("public class ResultRow");
				writer.WriteLine("{");
				writer.Write(resultRowPropertyBuilder);
				writer.WriteLine("}");
			}
		}

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			ShowLargeValueEditor(ResultGrid);
		}

		private void ResultGridSelectedCellsChangedHandler(object sender, SelectedCellsChangedEventArgs e)
		{
			if (_isSelectingCells)
			{
				return;
			}

			ViewModel.CurrentRowIndex = ResultGrid.CurrentCell.Item == null
				? 0
				: ResultGrid.Items.IndexOf(ResultGrid.CurrentCell.Item) + 1;

			CalculateSelectedCellStatistics();
		}

		private void CalculateSelectedCellStatistics()
		{
			if (ResultGrid.SelectedCells.Count <= 1)
			{
				ViewModel.SelectedCellInfoVisibility = Visibility.Collapsed;
				return;
			}

			var sum = 0m;
			var min = Decimal.MaxValue;
			var max = Decimal.MinValue;
			var count = 0;
			var hasOnlyNumericValues = true;
			foreach (var selectedCell in ResultGrid.SelectedCells)
			{
				var cellValue = ((object[])selectedCell.Item)[selectedCell.Column.DisplayIndex];
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

			ViewModel.SelectedCellValueCount = count;

			if (count > 0)
			{
				ViewModel.SelectedCellSum = sum;
				ViewModel.SelectedCellMin = min;
				ViewModel.SelectedCellMax = max;
				ViewModel.SelectedCellAverage = sum / count;
				ViewModel.SelectedCellNumericInfoVisibility = hasOnlyNumericValues ? Visibility.Visible : Visibility.Collapsed;
			}
			else
			{
				ViewModel.SelectedCellNumericInfoVisibility = Visibility.Collapsed;
			}

			ViewModel.SelectedCellInfoVisibility = Visibility.Visible;
		}

		private void ColumnHeaderMouseClickHandler(object sender, RoutedEventArgs e)
		{
			var header = e.OriginalSource as DataGridColumnHeader;
			if (header == null)
			{
				return;
			}

			if (Keyboard.Modifiers != ModifierKeys.Shift)
			{
				ResultGrid.SelectedCells.Clear();
			}

			_isSelectingCells = true;

			var cells = ResultGrid.Items.Cast<object[]>()
				.Select(r => new DataGridCellInfo(r, header.Column));

			foreach (var cell in cells)
			{
				if (!ResultGrid.SelectedCells.Contains(cell))
				{
					ResultGrid.SelectedCells.Add(cell);
				}
			}

			_isSelectingCells = false;

			ViewModel.CurrentRowIndex = ResultGrid.SelectedCells.Count;

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
			while (ConnectionAdapter.CanFetch && !IsCancellationRequested)
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

		private void ResultGridBeginningEditHandler(object sender, DataGridBeginningEditEventArgs e)
		{
			var textCompositionArgs = e.EditingEventArgs as TextCompositionEventArgs;
			if (textCompositionArgs != null)
			{
				e.Cancel = true;
			}
		}

		private void CanFetchAllRowsHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = CanFetchNextRows();
			canExecuteRoutedEventArgs.ContinueRouting = canExecuteRoutedEventArgs.CanExecute;
		}

		private bool CanFetchNextRows()
		{
			return _hasExecutionResult && !_documentPage.IsBusy && ConnectionAdapter.CanFetch && !ConnectionAdapter.IsExecuting;
		}

		private async Task FetchNextRows()
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = StatementExecutionModel.DefaultRowBatchSize - ViewModel.ResultRowItems.Count % StatementExecutionModel.DefaultRowBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = ConnectionAdapter.FetchRecordsAsync(batchSize, _statementExecutionCancellationTokenSource.Token));

			if (exception != null)
			{
				Messages.ShowError(exception.Message);
			}
			else
			{
				AppendRows(innerTask.Result);

				if (_executionResult.Statement.GatherExecutionStatistics)
				{
					ViewModel.SessionExecutionStatistics.MergeWith(await ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				}
			}
		}

		private void AppendRows(IEnumerable<object[]> rows)
		{
			ViewModel.ResultRowItems.AddRange(rows);
			ViewModel.MoreRowsExistVisibility = ConnectionAdapter.CanFetch ? Visibility.Visible : Visibility.Collapsed;
		}

		private void UpdateTimerMessage()
		{
			ViewModel.UpdateTimerMessage(_stopWatch.Elapsed, IsCancellationRequested);
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
			_timerExecutionMonitor.Stop();
			_timerExecutionMonitor.Dispose();
		}
	}
}
