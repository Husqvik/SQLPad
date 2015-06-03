using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
		private PageModel _pageModel;
		private object _previousSelectedTab;
		private DocumentPage _documentPage;

		public event EventHandler FetchNextRows;
		public event EventHandler FetchAllRows;
		public event EventHandler<CompilationErrorArgs> CompilationError;
		public event EventHandler<CanExecuteRoutedEventArgs> CanFetchAllRows;

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

		public OutputViewer()
		{
			InitializeComponent();
		}

		public void Initialize(IEnumerable<ColumnHeader> columnHeaders)
		{
			ResultGrid.Columns.Clear();

			foreach (var columnHeader in columnHeaders)
			{
				var columnTemplate = CreateDataGridTextColumnTemplate(columnHeader);
				ResultGrid.Columns.Add(columnTemplate);
			}

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
			
			_pageModel.GridRowInfoVisibility = Visibility.Visible;
			_pageModel.ResultRowItems.Clear();
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
			_pageModel.AffectedRowCount = -1;
			_pageModel.CurrentRowIndex = 0;
			_pageModel.ResultRowItems.Clear();
			_pageModel.CompilationErrors.Clear();
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

		private void CanFetchAllRowsHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			if (CanFetchAllRows != null)
			{
				CanFetchAllRows(this, canExecuteRoutedEventArgs);
			}

			canExecuteRoutedEventArgs.ContinueRouting = canExecuteRoutedEventArgs.CanExecute;
		}

		private void ExportDataFileHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;
			var dialog = new SaveFileDialog { Filter = dataExporter.FileNameFilter, OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			DocumentPage.SafeActionWithUserError(() => dataExporter.ExportToFile(dialog.FileName, ResultGrid, _documentPage.InfrastructureFactory.DataExportConverter));
		}

		private void ExportDataClipboardHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;

			DocumentPage.SafeActionWithUserError(() => dataExporter.ExportToClipboard(ResultGrid, _documentPage.InfrastructureFactory.DataExportConverter));
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
						sum += Convert.ToDecimal(stringValue, CultureInfo.CurrentCulture);
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

		private void ResultGridScrollChangedHandler(object sender, ScrollChangedEventArgs e)
		{
			if (FetchNextRows == null || e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
			{
				return;
			}

			FetchNextRows(this, EventArgs.Empty);
		}

		private void FetchAllRowsHandler(object sender, ExecutedRoutedEventArgs args)
		{
			if (FetchAllRows != null)
			{
				FetchAllRows(this, EventArgs.Empty);
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
	}
}
