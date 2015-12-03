using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
using Xceed.Wpf.Toolkit;

namespace SqlPad
{
	public partial class ResultViewer
	{
		#region dependency properties registration
		public static readonly DependencyProperty SelectedCellNumericInfoVisibilityProperty = DependencyProperty.Register(nameof(SelectedCellNumericInfoVisibility), typeof(Visibility), typeof(ResultViewer), new UIPropertyMetadata(Visibility.Collapsed));
		public static readonly DependencyProperty SelectedCellInfoVisibilityProperty = DependencyProperty.Register(nameof(SelectedCellInfoVisibility), typeof(Visibility), typeof(ResultViewer), new UIPropertyMetadata(Visibility.Collapsed));
		public static readonly DependencyProperty SelectedCellValueCountProperty = DependencyProperty.Register(nameof(SelectedCellValueCount), typeof(int), typeof(ResultViewer), new UIPropertyMetadata(0));
		public static readonly DependencyProperty SelectedCellSumProperty = DependencyProperty.Register(nameof(SelectedCellSum), typeof(decimal), typeof(ResultViewer), new UIPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellAverageProperty = DependencyProperty.Register(nameof(SelectedCellAverage), typeof(decimal), typeof(ResultViewer), new UIPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellMinProperty = DependencyProperty.Register(nameof(SelectedCellMin), typeof(decimal), typeof(ResultViewer), new UIPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellMaxProperty = DependencyProperty.Register(nameof(SelectedCellMax), typeof(decimal), typeof(ResultViewer), new UIPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedRowIndexProperty = DependencyProperty.Register(nameof(SelectedRowIndex), typeof(int), typeof(ResultViewer), new UIPropertyMetadata(0));
		public static readonly DependencyProperty AutoRefreshIntervalProperty = DependencyProperty.Register(nameof(AutoRefreshInterval), typeof(TimeSpan), typeof(ResultViewer), new UIPropertyMetadata(TimeSpan.FromSeconds(60), AutoRefreshIntervalChangedCallback));
		public static readonly DependencyProperty AutoRefreshEnabledProperty = DependencyProperty.Register(nameof(AutoRefreshEnabled), typeof(bool), typeof(ResultViewer), new UIPropertyMetadata(false, AutoRefreshEnabledChangedCallback));
		#endregion

		#region dependency property accessors
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
		public int SelectedRowIndex
		{
			get { return (int)GetValue(SelectedRowIndexProperty); }
			private set { SetValue(SelectedRowIndexProperty, value); }
		}

		[Bindable(true)]
		public TimeSpan AutoRefreshInterval
		{
			get { return (TimeSpan)GetValue(AutoRefreshIntervalProperty); }
			set { SetValue(AutoRefreshIntervalProperty, value); }
		}

		private static void AutoRefreshIntervalChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var interval = (TimeSpan)args.NewValue;
			var resultViewer = (ResultViewer)dependencyObject;
			if (interval.TotalSeconds < 1)
			{
				interval = TimeSpan.FromSeconds(1);
			}

			resultViewer.AutorefreshProgressBar.Maximum = interval.TotalSeconds;
			resultViewer.RefreshProgressBar();
		}

		[Bindable(true)]
		public bool AutoRefreshEnabled
		{
			get { return (bool)GetValue(AutoRefreshEnabledProperty); }
			set { SetValue(AutoRefreshEnabledProperty, value); }
		}

		private static void AutoRefreshEnabledChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var resultViewer = (ResultViewer)dependencyObject;
			var enable = (bool)args.NewValue;
			if (enable)
			{
				resultViewer._lastRefresh = DateTime.Now;
				resultViewer._refreshProgressBarTimer.Start();
				resultViewer.AutorefreshProgressBar.Maximum = resultViewer.AutorefreshProgressBar.Value = resultViewer.AutoRefreshInterval.TotalSeconds;
				resultViewer.RefreshProgressBar();
			}
			else
			{
				resultViewer._refreshProgressBarTimer.Stop();
			}
		}
		#endregion

		private readonly OutputViewer _outputViewer;
		private readonly ResultInfo _resultInfo;
		private readonly DispatcherTimer _refreshProgressBarTimer;
		private readonly ObservableCollection<object[]> _resultRows = new ObservableCollection<object[]>();
		private readonly StatementExecutionResult _executionResult;

		private IReadOnlyList<ColumnHeader> _columnHeaders;
		private DateTime _lastRefresh;
		private bool _isSelectingCells;
		private bool _searchedTextHighlightUsed;

		public TabItem TabItem { get; }

		public string Title { get; }

		public IReadOnlyList<object[]> ResultRowItems => _resultRows;

		public string StatementText => _executionResult.StatementModel.StatementText;

		private bool IsBusy => _outputViewer.IsBusy || _outputViewer.ConnectionAdapter.IsExecuting || _outputViewer.IsDebuggerControlVisible;

		public ResultViewer(OutputViewer outputViewer, StatementExecutionResult executionResult, ResultInfo resultInfo, IReadOnlyList<ColumnHeader> columnHeaders)
		{
			_outputViewer = outputViewer;
			_executionResult = executionResult;
			_resultInfo = resultInfo;
			_columnHeaders = columnHeaders;

			Title = resultInfo.Title;

			InitializeComponent();

			var header = new HeaderedContentControl { Content = new AccessText { Text = Title } };
			TabItem =
				new TabItem
				{
					Header = header,
					Content = this
				};

			header.MouseEnter += DataGridTabHeaderMouseEnterHandler;

			_refreshProgressBarTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(0.25) };
			_refreshProgressBarTimer.Tick += RefreshTimerProgressBarTickHandler;
		}

		public void Close()
		{
			_refreshProgressBarTimer.Tick -= RefreshTimerProgressBarTickHandler;
			_refreshProgressBarTimer.Stop();
			_outputViewer.TabControlResult.RemoveTabItemWithoutBindingError(TabItem);
		}

		private void RefreshTimerProgressBarTickHandler(object sender, EventArgs eventArgs)
		{
			RefreshProgressBar();
		}

		private void RefreshProgressBar()
		{
			var remainingSeconds = AutorefreshProgressBar.Maximum - (DateTime.Now - _lastRefresh).TotalSeconds;
			if (remainingSeconds < 0)
			{
				if (AutoRefreshEnabled)
				{
					RefreshTimerTickHandler(this, EventArgs.Empty);
				}

				remainingSeconds = 0;
			}
			else
			{
				AdjustProgressBarRefreshInterval(remainingSeconds);
			}

			AutorefreshProgressBar.Value = remainingSeconds;
			if (remainingSeconds > 10)
			{
				remainingSeconds = Math.Round(remainingSeconds);
			}

			AutorefreshProgressBar.ToolTip = remainingSeconds == 0
				? "refreshing... "
				: $"last refresh: {CellValueConverter.FormatDateTime(_lastRefresh)}; remaining: {TimeSpan.FromSeconds(remainingSeconds).ToPrettyString()}";
		}

		private void AdjustProgressBarRefreshInterval(double remainingSeconds)
		{
			const double minimumRefreshIntervalSeconds = 0.1;
			var refreshIntervalSeconds = AutorefreshProgressBar.ActualWidth == 0 ? minimumRefreshIntervalSeconds : Math.Max(remainingSeconds / AutorefreshProgressBar.ActualWidth, minimumRefreshIntervalSeconds);
			var interval = TimeSpan.FromSeconds(Math.Min(refreshIntervalSeconds, 1));
			_refreshProgressBarTimer.Interval = interval;
		}

		private async void RefreshTimerTickHandler(object sender, EventArgs eventArgs)
		{
			if (IsBusy)
			{
				return;
			}

			_refreshProgressBarTimer.Stop();

			await _outputViewer.ExecuteUsingCancellationToken(
				async ct => await App.SafeActionAsync(
					async () =>
					{
						_columnHeaders = await _outputViewer.ConnectionAdapter.RefreshResult(_resultInfo, ct);
						_resultRows.Clear();
						await ApplyReferenceConstraints(ct);
						await FetchNextRows(ct);
					}));

			AutorefreshProgressBar.Value = AutorefreshProgressBar.Maximum;

			_lastRefresh = DateTime.Now;
			_refreshProgressBarTimer.Start();
		}

		private void DataGridTabHeaderMouseEnterHandler(object sender, MouseEventArgs args)
		{
			DataGridTabHeaderPopupTextBox.FontFamily = _outputViewer.DocumentPage.Editor.FontFamily;
			DataGridTabHeaderPopupTextBox.FontSize = _outputViewer.DocumentPage.Editor.FontSize;
			ResultViewTabHeaderPopup.IsOpen = true;
		}

		private async void InitializedHandler(object sender, EventArgs e)
		{
			await _outputViewer.ExecuteUsingCancellationToken(ApplyReferenceConstraints);
		}

		private async Task ApplyReferenceConstraints(CancellationToken cancellationToken)
		{
			var childReferenceDataSources = await _outputViewer.StatementValidator.ApplyReferenceConstraintsAsync(_executionResult, _outputViewer.ConnectionAdapter.DatabaseModel, cancellationToken);

			DataGridHelper.InitializeDataGridColumns(ResultGrid, _columnHeaders, _outputViewer.StatementValidator, _outputViewer.ConnectionAdapter);

			AddChildReferenceColumns(ResultGrid, childReferenceDataSources);
		}

		private EventHandler<DataGridBeginningEditEventArgs> ResultGridBeginningEditCancelTextInputHandler => App.ResultGridBeginningEditCancelTextInputHandlerImplementation;

		private void CanExportDataHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Items.Count > 0;
		}

		private void CanGenerateCSharpQueryClassHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Columns.Count > 0;
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
				CSharpQueryClassGenerator.Generate(_executionResult.StatementModel, _columnHeaders, writer);
			}
		}

		private void CleanUpVirtualizedItemHandler(object sender, CleanUpVirtualizedItemEventArgs e)
		{
			e.Cancel = DataGridHelper.CanBeRecycled(e.UIElement);
		}

		private void CanFetchAllRowsHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = CanFetchNextRows();
			canExecuteRoutedEventArgs.ContinueRouting = canExecuteRoutedEventArgs.CanExecute;
		}

		private void CanRefreshHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = !IsBusy;
		}

		private bool CanFetchNextRows()
		{
			return !IsBusy && _outputViewer.ConnectionAdapter.CanFetch(_resultInfo);
		}

		private async void FetchAllRowsHandler(object sender, ExecutedRoutedEventArgs args)
		{
			await _outputViewer.ExecuteUsingCancellationToken(FetchAllRows);
		}

		private async Task FetchAllRows(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested && _outputViewer.ConnectionAdapter.CanFetch(_resultInfo))
			{
				await FetchNextRows(cancellationToken);
			}
		}

		private async Task FetchNextRows(CancellationToken cancellationToken)
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize - _resultRows.Count % ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = _outputViewer.ConnectionAdapter.FetchRecordsAsync(_resultInfo, batchSize, cancellationToken));
			if (exception != null)
			{
				var errorMessage = Messages.GetExceptionErrorMessage(exception);
				_outputViewer.AddExecutionLog(DateTime.Now, $"Row retrieval failed: {errorMessage}");
				Messages.ShowError(errorMessage);
			}
			else
			{
				AppendRows(innerTask.Result);

				await _outputViewer.UpdateExecutionStatisticsIfEnabled();
			}
		}
		private void AppendRows(IEnumerable<object[]> rows)
		{
			_resultRows.AddRange(rows);
			_outputViewer.StatusInfo.MoreRowsAvailable = _outputViewer.ConnectionAdapter.CanFetch(_resultInfo);
		}

		private void AddChildReferenceColumns(DataGrid dataGrid, IEnumerable<IReferenceDataSource> childReferenceDataSources)
		{
			if (!_outputViewer.EnableChildReferenceDataSources)
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
			var executionResult = await _outputViewer.ConnectionAdapter.ExecuteChildStatementAsync(executionModel, cancellationToken);
			var childReferenceDataSources = await _outputViewer.StatementValidator.ApplyReferenceConstraintsAsync(executionResult, _outputViewer.ConnectionAdapter.DatabaseModel, cancellationToken);
			var resultInfo = executionResult.ResultInfoColumnHeaders.Keys.Last();
			var resultSet = await _outputViewer.ConnectionAdapter.FetchRecordsAsync(resultInfo, ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize, cancellationToken);

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
			DataGridHelper.InitializeDataGridColumns(childRecordDataGrid, columnHeaders, _outputViewer.StatementValidator, _outputViewer.ConnectionAdapter);
			AddChildReferenceColumns(childRecordDataGrid, childReferenceDataSources);

			foreach (var columnTemplate in childRecordDataGrid.Columns)
			{
				columnTemplate.HeaderStyle = (Style)Application.Current.Resources["ColumnHeaderClickBubbleCancelation"];
			}

			return childRecordDataGrid;
		}

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			var senderDataGrid = (DataGrid)sender;
			var visual = e.OriginalSource as Visual;
			var originalDataGrid = visual?.FindParentVisual<DataGrid>();
			if (Equals(originalDataGrid, senderDataGrid))
			{
				DataGridHelper.ShowLargeValueEditor(senderDataGrid);
			}
		}

		private async void ExportDataFileHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;
			var dialog = new SaveFileDialog { Filter = dataExporter.FileNameFilter, OverwritePrompt = !dataExporter.HasAppendSupport };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			await ExportData((token, progress) => dataExporter.ExportToFileAsync(dialog.FileName, this, _outputViewer.DocumentPage.InfrastructureFactory.DataExportConverter, token, progress));
		}

		private static async Task ExportData(Func<CancellationToken, IProgress<int>, Task> getExportTaskFunction)
		{
			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				var operationMonitor = new WindowOperationMonitor(cancellationTokenSource) { IsIndeterminate = false };
				operationMonitor.Show();

				var exception = await App.SafeActionAsync(() => getExportTaskFunction(cancellationTokenSource.Token, operationMonitor));

				operationMonitor.Close();

				if (exception != null && !(exception is OperationCanceledException))
				{
					Messages.ShowError(exception.Message);
				}
			}
		}

		private async void ExportDataClipboardHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;
			await ExportData((token, progress) => dataExporter.ExportToClipboardAsync(this, _outputViewer.DocumentPage.InfrastructureFactory.DataExportConverter, token, progress));
		}

		private async void ResultGridScrollChangedHandler(object sender, ScrollChangedEventArgs e)
		{
			if (_searchedTextHighlightUsed)
			{
				HighlightSearchedText();
			}

			if (e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
			{
				return;
			}

			if (!CanFetchNextRows())
			{
				return;
			}

			await _outputViewer.ExecuteUsingCancellationToken(FetchNextRows);
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

			var selectedCells = ResultGrid.SelectedCells.ToHashSet();
			foreach (object[] rowItems in ResultGrid.Items)
			{
				var cell = new DataGridCellInfo(rowItems, header.Column);
				if (clearCurrentCells || !selectedCells.Contains(cell))
				{
					ResultGrid.SelectedCells.Add(cell);
				}
			}

			_isSelectingCells = false;

			SelectedRowIndex = ResultGrid.SelectedCells.Count;

			CalculateSelectedCellStatistics();

			ResultGrid.Focus();
		}

		private void ResultGridSelectedCellsChangedHandler(object sender, SelectedCellsChangedEventArgs e)
		{
			if (_isSelectingCells)
			{
				return;
			}

			SelectedRowIndex = ResultGrid.CurrentCell.Item == null
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

		private void DataGridTabHeaderPopupMouseLeaveHandler(object sender, MouseEventArgs e)
		{
			var child = (FrameworkElement)ResultViewTabHeaderPopup.Child;
			var position = Mouse.GetPosition(child);
			if (position.X < 0 || position.Y < 0 || position.X > child.ActualWidth || position.Y > child.ActualHeight)
			{
				ResultViewTabHeaderPopup.IsOpen = false;
			}
		}

		private void SearchTextChangedHandler(object sender, TextChangedEventArgs e)
		{
			HighlightSearchedText();
		}

		private void HighlightSearchedText()
		{
			var searchedWords = TextSearchHelper.GetSearchedWords(SearchPhraseTextBox.Text);
			_searchedTextHighlightUsed |= searchedWords.Length > 0;
			var regexPattern = TextSearchHelper.GetRegexPattern(searchedWords);

			foreach (var row in ResultGrid.GetDataGridRows())
			{
				if (row == null)
				{
					break;
				}

				if (!ResultGrid.IsInViewport(row))
				{
					continue;
				}

				row.HighlightTextItems(regexPattern);
			}
		}

		private void SearchPanelCloseClickHandler(object sender, ExecutedRoutedEventArgs e)
		{
			SearchPanel.Visibility = Visibility.Collapsed;
			SearchPhraseTextBox.Text = String.Empty;
		}

		private void SearchPanelOpenClickHandler(object sender, ExecutedRoutedEventArgs e)
		{
			SearchPanel.Visibility = Visibility.Visible;
			SearchPhraseTextBox.Focus();
		}

		private void ResultViewerDataGridKeyDownHandler(object sender, KeyEventArgs e)
		{
			var reraisedEvent =
				new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, e.Key)
				{
					RoutedEvent = Keyboard.KeyDownEvent,
					Source = e.OriginalSource
				};

			_outputViewer.RaiseEvent(reraisedEvent);
		}
	}

	internal class TimeSpanToIntegerSecondConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return System.Convert.ToInt32(((TimeSpan)value).TotalSeconds);
		}

		public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return TimeSpan.FromSeconds(System.Convert.ToDouble(value));
		}
	}

	public class NumericUpDown : IntegerUpDown
	{
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			TextBox.MaxLength = 8;
		}
	}
}
