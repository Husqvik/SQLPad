using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";

		private bool _isSelectingCells;
		private PageModel _pageModel;
		private object _previousSelectedTab;

		public event EventHandler FetchNextRows;
		public event EventHandler FetchAllRows;
		public event EventHandler<CompilationErrorArgs> CompilationError;
		public event EventHandler<CanExecuteRoutedEventArgs> CanFetchAllRows;

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
		}

		internal static DataGridTextColumn CreateDataGridTextColumnTemplate(ColumnHeader columnHeader)
		{
			var columnTemplate =
				new DataGridTextColumn
				{
					Header = columnHeader.Name.Replace("_", "__"),
					Binding = new Binding(String.Format("[{0}]", columnHeader.ColumnIndex)) { Converter = CellValueConverter.Instance, ConverterParameter = columnHeader },
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

		public void SetupExecutionPlanViewer(IExecutionPlanViewer executionPlanViewer)
		{
			TabExecutionPlan.Content = executionPlanViewer.Control;
		}

		public void Initialize()
		{
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
				TabControlResult.SelectedIndex = 0;
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
			TabControlResult.SelectedIndex = 3;
		}

		private void TabControlResultGiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
		{
			e.Handled = true;
		}

		private void ReadOnlyGridKeyDownHandler(object sender, KeyEventArgs e)
		{
			var keyCode = e.Key;
			if (!keyCode.In(Key.F4, Key.F5, Key.Escape, Key.System))
			{
				e.Handled = true;
			}
		}

		private void CanExportToCsvHandler(object sender, CanExecuteRoutedEventArgs args)
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

		private void ExportToCsvHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			DocumentPage.SafeActionWithUserError(() =>
			{
				using (var file = File.CreateText(dialog.FileName))
				{
					ExportToCsv(file);
				}
			});
		}

		private void ExportToCsv(TextWriter writer)
		{
			var orderedColumns = ResultGrid.Columns
				.OrderBy(c => c.DisplayIndex)
				.ToArray();

			var columnHeaders = orderedColumns
				.Select(c => String.Format(MaskWrapByQuote, c.Header.ToString().Replace("__", "_").Replace(QuoteCharacter, DoubleQuotes)));

			const string separator = ";";
			var headerLine = String.Join(separator, columnHeaders);
			writer.WriteLine(headerLine);

			var converterParameters = orderedColumns
				.Select(c => ((Binding)((DataGridTextColumn)c).Binding).ConverterParameter)
				.ToArray();

			foreach (object[] rowValues in ResultGrid.Items)
			{
				var contentLine = String.Join(separator, rowValues.Select((t, i) => FormatCsvValue(t, converterParameters[i])));
				writer.WriteLine(contentLine);
			}
		}

		private static string FormatCsvValue(object value, object converterParameter)
		{
			if (value == DBNull.Value)
				return null;

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), converterParameter, CultureInfo.CurrentUICulture).ToString();
			return String.Format(MaskWrapByQuote, stringValue.Replace(QuoteCharacter, DoubleQuotes));
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
			var invalidNumberDetected = false;
			foreach (var selectedCell in ResultGrid.SelectedCells)
			{
				var stringValue = ((object[])selectedCell.Item)[selectedCell.Column.DisplayIndex].ToString();
				if (String.IsNullOrEmpty(stringValue))
				{
					continue;
				}

				decimal value;
				if (Decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
				{
					sum += value;
				}
				else
				{
					invalidNumberDetected = true;
				}

				count++;
			}

			_pageModel.SelectedCellValueCount = count;

			if (count > 0)
			{
				_pageModel.SelectedCellSum = sum;
				_pageModel.SelectedCellAverage = sum / count;
				_pageModel.SelectedCellNumericInfoVisibility = invalidNumberDetected ? Visibility.Collapsed : Visibility.Visible;
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
	}

	public class CompilationErrorArgs : EventArgs
	{
		public CompilationError CompilationError { get; private set; }

		public CompilationErrorArgs(CompilationError compilationError)
		{
			if (compilationError == null)
			{
				throw new ArgumentNullException("compilationError");
			}
			
			CompilationError = compilationError;
		}
	}
}
