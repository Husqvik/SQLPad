using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
	[DebuggerDisplay("OutputViewer (Title={Title})")]
	public partial class OutputViewer : IDisposable
	{
		private const int MaxHistoryEntrySize = 8192;

		#region dependency properties registration
		public static readonly DependencyProperty ShowAllSessionExecutionStatisticsProperty = DependencyProperty.Register("ShowAllSessionExecutionStatistics", typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(ShowAllSessionExecutionStatisticsPropertyChangedCallbackHandler));
		public static readonly DependencyProperty EnableDatabaseOutputProperty = DependencyProperty.Register("EnableDatabaseOutput", typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register("IsPinned", typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty DatabaseOutputProperty = DependencyProperty.Register("DatabaseOutput", typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(String.Empty));

		public static readonly DependencyProperty IsTransactionControlEnabledProperty = DependencyProperty.Register("IsTransactionControlEnabled", typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty TransactionControlVisibityProperty = DependencyProperty.Register("TransactionControlVisibity", typeof(Visibility), typeof(OutputViewer), new FrameworkPropertyMetadata(Visibility.Collapsed));
		
		public static readonly DependencyProperty SelectedCellNumericInfoVisibilityProperty = DependencyProperty.Register("SelectedCellNumericInfoVisibility", typeof(Visibility), typeof(OutputViewer), new FrameworkPropertyMetadata(Visibility.Collapsed));
		public static readonly DependencyProperty SelectedCellInfoVisibilityProperty = DependencyProperty.Register("SelectedCellInfoVisibility", typeof(Visibility), typeof(OutputViewer), new FrameworkPropertyMetadata(Visibility.Collapsed));

		public static readonly DependencyProperty SelectedCellValueCountProperty = DependencyProperty.Register("SelectedCellValueCount", typeof(int), typeof(OutputViewer), new FrameworkPropertyMetadata(0));
		public static readonly DependencyProperty SelectedCellSumProperty = DependencyProperty.Register("SelectedCellSum", typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellAverageProperty = DependencyProperty.Register("SelectedCellAverage", typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellMinProperty = DependencyProperty.Register("SelectedCellMin", typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellMaxProperty = DependencyProperty.Register("SelectedCellMax", typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		#endregion

		#region dependency property accessors
		[Bindable(true)]
		public bool ShowAllSessionExecutionStatistics
		{
			get { return (bool)GetValue(ShowAllSessionExecutionStatisticsProperty); }
			set { SetValue(ShowAllSessionExecutionStatisticsProperty, value); }
		}

		private static void ShowAllSessionExecutionStatisticsPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			((OutputViewer)dependencyObject).ApplySessionExecutionStatisticsFilter();
		}

		[Bindable(true)]
		public Visibility SelectedCellNumericInfoVisibility
		{
			get { return (Visibility)GetValue(SelectedCellNumericInfoVisibilityProperty); }
			private set { SetValue(SelectedCellNumericInfoVisibilityProperty, value); }
		}
		
		[Bindable(true)]
		public Visibility SelectedCellInfoVisibility
		{
			get { return (Visibility)GetValue(SelectedCellInfoVisibilityProperty); }
			private set { SetValue(SelectedCellInfoVisibilityProperty, value); }
		}
		
		[Bindable(true)]
		public Visibility TransactionControlVisibity
		{
			get { return (Visibility)GetValue(TransactionControlVisibityProperty); }
			private set { SetValue(TransactionControlVisibityProperty, value); }
		}

		[Bindable(true)]
		public bool IsTransactionControlEnabled
		{
			get { return (bool)GetValue(IsTransactionControlEnabledProperty); }
			private set { SetValue(IsTransactionControlEnabledProperty, value); }
		}

		[Bindable(true)]
		public int SelectedCellValueCount
		{
			get { return (int)GetValue(SelectedCellValueCountProperty); }
			private set { SetValue(SelectedCellValueCountProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellSum
		{
			get { return (decimal)GetValue(SelectedCellSumProperty); }
			private set { SetValue(SelectedCellSumProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellAverage
		{
			get { return (decimal)GetValue(SelectedCellAverageProperty); }
			private set { SetValue(SelectedCellAverageProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellMin
		{
			get { return (decimal)GetValue(SelectedCellMinProperty); }
			private set { SetValue(SelectedCellMinProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellMax
		{
			get { return (decimal)GetValue(SelectedCellMaxProperty); }
			private set { SetValue(SelectedCellMaxProperty, value); }
		}

		[Bindable(true)]
		public bool EnableDatabaseOutput
		{
			get { return (bool)GetValue(EnableDatabaseOutputProperty); }
			set { SetValue(EnableDatabaseOutputProperty, value); }
		}

		[Bindable(true)]
		public bool IsPinned
		{
			get { return (bool)GetValue(IsPinnedProperty); }
			set { SetValue(IsPinnedProperty, value); }
		}

		[Bindable(true)]
		public string DatabaseOutput
		{
			get { return (string)GetValue(DatabaseOutputProperty); }
			private set { SetValue(DatabaseOutputProperty, value); }
		}

		[Bindable(true)]
		public string Title
		{
			get { return (string)GetValue(TitleProperty); }
			set { SetValue(TitleProperty, value); }
		}
		#endregion

		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly StringBuilder _databaseOutputBuilder = new StringBuilder();
		private readonly ObservableCollection<object[]> _resultRows = new ObservableCollection<object[]>();
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
		private readonly StatusInfoModel _statusInfo = new StatusInfoModel();
		private readonly DatabaseProviderConfiguration _providerConfiguration;
		private readonly DocumentPage _documentPage;
		private readonly IConnectionAdapter _connectionAdapter;

		private bool _isRunning;
		private bool _isSelectingCells;
		private bool _hasExecutionResult;
		private object _previousSelectedTab;
		private StatementExecutionResult _executionResult;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IExecutionPlanViewer ExecutionPlanViewer { get; private set; }

		private bool IsCancellationRequested
		{
			get
			{
				var cancellationTokenSource = _statementExecutionCancellationTokenSource;
				return cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested;
			}
		}

		public bool KeepDatabaseOutputHistory { get; set; }

		public StatusInfoModel StatusInfo
		{
			get { return _statusInfo; }
		}

		public bool IsBusy
		{
			get { return _isRunning; }
			private set
			{
				_isRunning = value;
				_documentPage.ViewModel.NotifyIsRunning();
				App.MainWindow.NotifyTaskStatus();
			}
		}

		public ICollection<object[]> ResultRowItems { get { return _resultRows; } }

		public ICollection<SessionExecutionStatisticsRecord> SessionExecutionStatistics { get { return _sessionExecutionStatistics; } }

		public ICollection<CompilationError> CompilationErrors { get { return _compilationErrors; } }

		public OutputViewer(DocumentPage documentPage)
		{
			InitializeComponent();
			
			_timerExecutionMonitor.Elapsed += delegate { Dispatcher.Invoke(() => UpdateTimerMessage(_stopWatch.Elapsed, IsCancellationRequested)); };

			SetUpSessionExecutionStatisticsView();

			Initialize();

			_documentPage = documentPage;

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(_documentPage.ViewModel.CurrentConnection.ProviderName);

			_connectionAdapter = _documentPage.DatabaseModel.CreateConnectionAdapter();

			ExecutionPlanViewer = _documentPage.InfrastructureFactory.CreateExecutionPlanViewer(_documentPage.DatabaseModel);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;
		}

		private void SetUpSessionExecutionStatisticsView()
		{
			ApplySessionExecutionStatisticsFilter();
			SetUpSessionExecutionStatisticsSorting();
		}

		private void ApplySessionExecutionStatisticsFilter()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.Filter = ShowAllSessionExecutionStatistics
				? (Predicate<object>)null
				: o => ((SessionExecutionStatisticsRecord)o).Value != 0;
		}

		private void SetUpSessionExecutionStatisticsSorting()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
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

			_statusInfo.ResultGridAvailable = true;
			_resultRows.Clear();

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

			_statusInfo.ResultGridAvailable = false;
			_statusInfo.MoreRowsAvailable = false;
			_statusInfo.DdlStatementExecutedSuccessfully = false;
			_statusInfo.AffectedRowCount = -1;
			_statusInfo.SelectedRowIndex = 0;
			
			WriteDatabaseOutput(String.Empty);

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

		private bool IsPreviousTabAlwaysVisible
		{
			get { return _previousSelectedTab != null && IsTabAlwaysVisible(_previousSelectedTab); }
		}
		
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

			var executionHistoryRecord = new StatementExecutionHistoryEntry { StatementText = executionModel.StatementText, ExecutedAt = DateTime.Now };

			Task<StatementExecutionResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = _connectionAdapter.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(actionResult.Exception.Message);
				return;
			}

			if (executionHistoryRecord.StatementText.Length <= MaxHistoryEntrySize)
			{
				_providerConfiguration.AddStatementExecution(executionHistoryRecord);
			}
			else
			{
				Trace.WriteLine(String.Format("Executes statement not stored in the execution history. The maximum allowed size is {0} characters while the statement has {1} characters.", MaxHistoryEntrySize, executionHistoryRecord.StatementText.Length));
			}

			var executionResult = innerTask.Result;
			if (!executionResult.ExecutedSuccessfully)
			{
				NotifyExecutionCanceled();
				return;
			}

			TransactionControlVisibity = _connectionAdapter.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;

			UpdateTimerMessage(actionResult.Elapsed, false);
			
			WriteDatabaseOutput(executionResult.DatabaseOutput);

			if (executionResult.Statement.GatherExecutionStatistics)
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

			if (executionResult.CompilationErrors.Count > 0)
			{
				var lineOffset = _documentPage.Editor.GetLineNumberByOffset(executionModel.Statement.SourcePosition.IndexStart);
				foreach (var error in executionResult.CompilationErrors)
				{
					error.Line += lineOffset;
					_compilationErrors.Add(error);
				}

				ShowCompilationErrors();
			}

			if (executionResult.ColumnHeaders.Count == 0)
			{
				if (executionResult.AffectedRowCount == -1)
				{
					_statusInfo.DdlStatementExecutedSuccessfully = true;
				}
				else
				{
					_statusInfo.AffectedRowCount = executionResult.AffectedRowCount;
				}

				return;
			}

			DisplayResult(executionResult);
		}

		private void NotifyExecutionCanceled()
		{
			_statusInfo.ExecutionTimerMessage = "Canceled";
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

			_statusInfo.SelectedRowIndex = ResultGrid.CurrentCell.Item == null
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

			_statusInfo.SelectedRowIndex = ResultGrid.SelectedCells.Count;

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
			_statusInfo.MoreRowsAvailable = _connectionAdapter.CanFetch;
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
			_connectionAdapter.Dispose();
		}

		private void ButtonCommitTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			App.SafeActionWithUserError(() =>
			{
				_connectionAdapter.CommitTransaction();
				SetValue(TransactionControlVisibityProperty, Visibility.Collapsed);
			});

			_documentPage.Editor.Focus();
		}

		private async void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			IsTransactionControlEnabled = false;

			IsBusy = true;

			var result = await SafeTimedActionAsync(_connectionAdapter.RollbackTransaction);
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

			_statusInfo.ExecutionTimerMessage = formattedValue;
		}

		private struct ActionResult
		{
			public bool IsSuccessful { get { return Exception == null; } }

			public Exception Exception { get; set; }

			public TimeSpan Elapsed { get; set; }
		}
	}
}
