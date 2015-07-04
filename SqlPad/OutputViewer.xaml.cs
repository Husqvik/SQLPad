using System;
using System.Collections.Generic;
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

namespace SqlPad
{
	public partial class OutputViewer
	{
		private bool _isSelectingCells;
		private bool _hasExecutionResult;
		private PageModel _pageModel;
		private object _previousSelectedTab;
		private DocumentPage _documentPage;
		private StatementExecutionResult _executionResult;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IExecutionPlanViewer ExecutionPlanViewer { get; private set; }

		public PageModel DataModel
		{
			get { return (PageModel)DataContext; }
			set
			{
				_pageModel = value;
				DataContext = value;
			}
		}

		// TODO: Remove duplication
		public bool IsBusy
		{
			get { return _pageModel.IsRunning; }
			private set
			{
				_pageModel.IsRunning = value;
				App.MainWindow.NotifyTaskStatus();
			}
		}

		public OutputViewer()
		{
			InitializeComponent();
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
			
			_pageModel.GridRowInfoVisibility = Visibility.Visible;
			_pageModel.ResultRowItems.Clear();

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
			
			ExecutionPlanViewer = documentPage.InfrastructureFactory.CreateExecutionPlanViewer(documentPage.DatabaseModel);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;
		}

		public void Initialize()
		{
			_hasExecutionResult = false;

			_pageModel.AffectedRowCount = -1;
			_pageModel.CurrentRowIndex = 0;
			_pageModel.ResultRowItems.Clear();
			_pageModel.CompilationErrors.Clear();
			_pageModel.MoreRowsExistVisibility = Visibility.Collapsed;
			_pageModel.GridRowInfoVisibility = Visibility.Collapsed;
			_pageModel.ExecutionPlanAvailable = Visibility.Collapsed;
			_pageModel.StatementExecutedSuccessfullyStatusMessageVisibility = Visibility.Collapsed;
			_pageModel.SessionExecutionStatistics.Clear();
			_pageModel.WriteDatabaseOutput(String.Empty);

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

			_pageModel.CurrentRowIndex = ResultGrid.CurrentCell.Item == null
				? 0
				: ResultGrid.Items.IndexOf(ResultGrid.CurrentCell.Item) + 1;

			CalculateSelectedCellStatistics();
		}

		private void CalculateSelectedCellStatistics()
		{
			if (ResultGrid.SelectedCells.Count <= 1)
			{
				_pageModel.SelectedCellInfoVisibility = Visibility.Collapsed;
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

			_pageModel.SelectedCellValueCount = count;

			if (count > 0)
			{
				_pageModel.SelectedCellSum = sum;
				_pageModel.SelectedCellMin = min;
				_pageModel.SelectedCellMax = max;
				_pageModel.SelectedCellAverage = sum / count;
				_pageModel.SelectedCellNumericInfoVisibility = hasOnlyNumericValues ? Visibility.Visible : Visibility.Collapsed;
			}
			else
			{
				_pageModel.SelectedCellNumericInfoVisibility = Visibility.Collapsed;
			}

			_pageModel.SelectedCellInfoVisibility = Visibility.Visible;
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

			_pageModel.CurrentRowIndex = ResultGrid.SelectedCells.Count;

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

			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				await FetchNextRows();

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private async void FetchAllRowsHandler(object sender, ExecutedRoutedEventArgs args)
		{
			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				while (!_statementExecutionCancellationTokenSource.IsCancellationRequested && _executionResult.ConnectionAdapter.CanFetch)
				{
					await FetchNextRows();
				}

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
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
			return !_documentPage.IsBusy && _hasExecutionResult && _executionResult.ConnectionAdapter.CanFetch && !_executionResult.ConnectionAdapter.IsExecuting;
		}

		private async Task FetchNextRows()
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = StatementExecutionModel.DefaultRowBatchSize - _pageModel.ResultRowItems.Count % StatementExecutionModel.DefaultRowBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = _executionResult.ConnectionAdapter.FetchRecordsAsync(batchSize, _statementExecutionCancellationTokenSource.Token));

			if (exception != null)
			{
				Messages.ShowError(exception.Message);
			}
			else
			{
				AppendRows(innerTask.Result);

				if (_executionResult.Statement.GatherExecutionStatistics)
				{
					_pageModel.SessionExecutionStatistics.MergeWith(await _executionResult.ConnectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				}
			}
		}

		private void AppendRows(IEnumerable<object[]> rows)
		{
			_pageModel.ResultRowItems.AddRange(rows);
			_pageModel.MoreRowsExistVisibility = _executionResult.ConnectionAdapter.CanFetch ? Visibility.Visible : Visibility.Collapsed;
		}
	}
}
