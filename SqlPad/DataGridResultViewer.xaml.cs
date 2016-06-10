using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
	public partial class DataGridResultViewer
	{
		#region dependency properties registration
		public static readonly DependencyProperty IsSelectedCellLimitInfoVisibleProperty = DependencyProperty.Register(nameof(IsSelectedCellLimitInfoVisible), typeof(bool), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty IsSelectedCellAggregatedInfoVisibleProperty = DependencyProperty.Register(nameof(IsSelectedCellAggregatedInfoVisible), typeof(bool), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty IsSelectedCellInfoVisibleProperty = DependencyProperty.Register(nameof(IsSelectedCellInfoVisible), typeof(bool), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedCellValueCountProperty = DependencyProperty.Register(nameof(SelectedCellValueCount), typeof(long), typeof(DataGridResultViewer), new UIPropertyMetadata(0L));
		public static readonly DependencyProperty SelectedCellDistinctValueCountProperty = DependencyProperty.Register(nameof(SelectedCellDistinctValueCount), typeof(long), typeof(DataGridResultViewer), new UIPropertyMetadata(0L));
		public static readonly DependencyProperty SelectedCellSumProperty = DependencyProperty.Register(nameof(SelectedCellSum), typeof(object), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedCellAverageProperty = DependencyProperty.Register(nameof(SelectedCellAverage), typeof(object), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedCellMinProperty = DependencyProperty.Register(nameof(SelectedCellMin), typeof(object), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedCellMaxProperty = DependencyProperty.Register(nameof(SelectedCellMax), typeof(object), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedCellModeProperty = DependencyProperty.Register(nameof(SelectedCellMode), typeof(object), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedCellMedianProperty = DependencyProperty.Register(nameof(SelectedCellMedian), typeof(object), typeof(DataGridResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty SelectedRowIndexProperty = DependencyProperty.Register(nameof(SelectedRowIndex), typeof(int), typeof(DataGridResultViewer), new UIPropertyMetadata(0));
		public static readonly DependencyProperty AutoRefreshIntervalProperty = DependencyProperty.Register(nameof(AutoRefreshInterval), typeof(TimeSpan), typeof(DataGridResultViewer), new UIPropertyMetadata(TimeSpan.FromSeconds(60), AutoRefreshIntervalChangedCallback));
		public static readonly DependencyProperty AutoRefreshEnabledProperty = DependencyProperty.Register(nameof(AutoRefreshEnabled), typeof(bool), typeof(DataGridResultViewer), new UIPropertyMetadata(false, AutoRefreshEnabledChangedCallback));
		public static readonly DependencyProperty SearchMatchCountProperty = DependencyProperty.Register(nameof(SearchMatchCount), typeof(int?), typeof(DataGridResultViewer), new UIPropertyMetadata());
		#endregion

		#region dependency property accessors
		[Bindable(true)]
		public bool IsSelectedCellLimitInfoVisible
		{
			get { return (bool)GetValue(IsSelectedCellLimitInfoVisibleProperty); }
			private set { SetValue(IsSelectedCellLimitInfoVisibleProperty, value); }
		}

		[Bindable(true)]
		public bool IsSelectedCellAggregatedInfoVisible
		{
			get { return (bool)GetValue(IsSelectedCellAggregatedInfoVisibleProperty); }
			private set { SetValue(IsSelectedCellAggregatedInfoVisibleProperty, value); }
		}

		[Bindable(true)]
		public bool IsSelectedCellInfoVisible
		{
			get { return (bool)GetValue(IsSelectedCellInfoVisibleProperty); }
			private set { SetValue(IsSelectedCellInfoVisibleProperty, value); }
		}

		[Bindable(true)]
		public long SelectedCellValueCount
		{
			get { return (long)GetValue(SelectedCellValueCountProperty); }
			private set { SetValue(SelectedCellValueCountProperty, value); }
		}

		[Bindable(true)]
		public long SelectedCellDistinctValueCount
		{
			get { return (long)GetValue(SelectedCellDistinctValueCountProperty); }
			private set { SetValue(SelectedCellDistinctValueCountProperty, value); }
		}

		[Bindable(true)]
		public object SelectedCellSum
		{
			get { return GetValue(SelectedCellSumProperty); }
			private set { SetValue(SelectedCellSumProperty, value); }
		}

		[Bindable(true)]
		public object SelectedCellAverage
		{
			get { return GetValue(SelectedCellAverageProperty); }
			private set { SetValue(SelectedCellAverageProperty, value); }
		}

		[Bindable(true)]
		public object SelectedCellMode
		{
			get { return GetValue(SelectedCellModeProperty); }
			private set { SetValue(SelectedCellModeProperty, value); }
		}

		[Bindable(true)]
		public object SelectedCellMedian
		{
			get { return GetValue(SelectedCellMedianProperty); }
			private set { SetValue(SelectedCellMedianProperty, value); }
		}

		[Bindable(true)]
		public object SelectedCellMin
		{
			get { return GetValue(SelectedCellMinProperty); }
			private set { SetValue(SelectedCellMinProperty, value); }
		}

		[Bindable(true)]
		public object SelectedCellMax
		{
			get { return GetValue(SelectedCellMaxProperty); }
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
			var resultViewer = (DataGridResultViewer)dependencyObject;
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
			var resultViewer = (DataGridResultViewer)dependencyObject;
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

		[Bindable(true)]
		public int? SearchMatchCount
		{
			get { return (int?)GetValue(SearchMatchCountProperty); }
			private set { SetValue(SearchMatchCountProperty, value); }
		}
		#endregion

		private readonly OutputViewer _outputViewer;
		private readonly ResultInfo _resultInfo;
		private readonly DispatcherTimer _refreshProgressBarTimer;
		private readonly ObservableCollection<object[]> _resultRows = new ObservableCollection<object[]>();
		private readonly StatementExecutionResult _executionResult;

		private DateTime _lastRefresh;
		private bool _searchedTextHighlightUsed;
		private LastSearchedCell _lastSearchedCell = LastSearchedCell.Empty;

		public TabItem TabItem { get; }

		public string Title { get; }

		public IReadOnlyList<object[]> ResultRowItems => _resultRows;

		public string StatementText => _executionResult.StatementModel.StatementText;

		private bool IsBusy => _outputViewer.IsBusy || _outputViewer.ConnectionAdapter.IsExecuting || _outputViewer.IsDebuggerControlVisible;

		private IReadOnlyList<ColumnHeader> ColumnHeaders => _executionResult.ResultInfoColumnHeaders[_resultInfo];

		public DataGridResultViewer(OutputViewer outputViewer, StatementExecutionResult executionResult, ResultInfo resultInfo)
		{
			_outputViewer = outputViewer;
			_executionResult = executionResult;
			_resultInfo = resultInfo;

			Title = resultInfo.Title;

			InitializeComponent();

			var header =
				new HeaderedContentControl
				{
					Content = new AccessText { Text = Title }
				};

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

		private async void RefreshTimerTickHandler(object sender, EventArgs args)
		{
			if (IsBusy)
			{
				return;
			}

			_refreshProgressBarTimer.Stop();

			await _outputViewer.ExecuteUsingCancellationToken(
				async t =>
					await App.SafeActionAsync(
						async () =>
						{
							await _outputViewer.ConnectionAdapter.RefreshResult(_executionResult, t);
							_resultRows.Clear();
							await ApplyReferenceConstraints(t);
							await FetchNextRows(t);
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

		private async void InitializedHandler(object sender, EventArgs args)
		{
			await _outputViewer.ExecuteUsingCancellationToken(ApplyReferenceConstraints);
		}

		private async Task ApplyReferenceConstraints(CancellationToken cancellationToken)
		{
			var childReferenceDataSources = await _outputViewer.StatementValidator.ApplyReferenceConstraintsAsync(_executionResult, _outputViewer.ConnectionAdapter.DatabaseModel, cancellationToken);

			DataGridHelper.InitializeDataGridColumns(ResultGrid, ColumnHeaders, _outputViewer.StatementValidator, _outputViewer.ConnectionAdapter);

			AddChildReferenceColumns(ResultGrid, childReferenceDataSources);
		}

		private EventHandler<DataGridBeginningEditEventArgs> ResultGridBeginningEditCancelTextInputHandler => App.DataGridBeginningEditCancelTextInputHandlerImplementation;

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
				CSharpQueryClassGenerator.Generate(_executionResult.StatementModel, ColumnHeaders, writer);
			}
		}

		private void CleanUpVirtualizedItemHandler(object sender, CleanUpVirtualizedItemEventArgs args)
		{
			args.Cancel = DataGridHelper.CanBeRecycled(args.UIElement);
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
			Dispatcher.Invoke(DispatcherPriority.DataBind, (Action<IEnumerable<object[]>>)(items => _resultRows.AddRange(items)), rows);

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
			childRecordDataGrid.BeginningEdit += App.DataGridBeginningEditCancelTextInputHandlerImplementation;
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

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs args)
		{
			var senderDataGrid = (DataGrid)sender;
			var visual = args.OriginalSource as Visual;
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

				var isOperationCanceledException = exception is OperationCanceledException;
				if (exception != null && !isOperationCanceledException)
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

		private async void ResultGridScrollChangedHandler(object sender, ScrollChangedEventArgs args)
		{
			if (_searchedTextHighlightUsed)
			{
				HighlightSearchedText();
			}

			if (args.VerticalOffset + args.ViewportHeight != args.ExtentHeight ||
				(args.ViewportHeight != 0 && args.VerticalChange == 0))
			{
				return;
			}

			if (!CanFetchNextRows())
			{
				return;
			}

			await _outputViewer.ExecuteUsingCancellationToken(FetchNextRows);
		}

		private void ColumnHeaderMouseClickHandler(object sender, RoutedEventArgs args)
		{
			var header = args.OriginalSource as DataGridColumnHeader;
			if (header == null)
			{
				return;
			}

			var isControlPressed = Keyboard.Modifiers == ModifierKeys.Control;
			var clearCurrentCells = Keyboard.Modifiers != ModifierKeys.Shift && !isControlPressed;
			if (clearCurrentCells)
			{
				ResultGrid.SelectedCells.Clear();
			}

			if (isControlPressed && ResultGrid.SelectedCells.Count(c => c.Column.Equals(header.Column)) == ResultGrid.Items.Count)
			{
				ResultGrid.DeselectRegion(0, header.Column.DisplayIndex, ResultGrid.Items.Count, 1);
			}
			else
			{
				ResultGrid.SelectRegion(0, header.Column.DisplayIndex, ResultGrid.Items.Count, 1);
			}

			SelectedRowIndex = ResultGrid.SelectedCells.Count;

			CalculateSelectedCellStatistics();

			ResultGrid.Focus();
		}

		private void ResultGridSelectedCellsChangedHandler(object sender, SelectedCellsChangedEventArgs args)
		{
			SelectedRowIndex = ResultGrid.CurrentCell.Item == null
				? 0
				: ResultGrid.Items.IndexOf(ResultGrid.CurrentCell.Item) + 1;

			CalculateSelectedCellStatistics();
		}

		private void CalculateSelectedCellStatistics()
		{
			if (ResultGrid.SelectedCells.Count <= 1)
			{
				IsSelectedCellInfoVisible = false;
				return;
			}

			var valueAggregator = _outputViewer.DocumentPage.InfrastructureFactory.CreateValueAggregator();

			foreach (var selectedCell in ResultGrid.SelectedCells)
			{
				var columnHeader = selectedCell.Column.Header as ColumnHeader;
				if (columnHeader == null)
				{
					return;
				}

				var rowValues = (object[])selectedCell.Item;
				var cellValue = rowValues[columnHeader.ColumnIndex];
				valueAggregator.AddValue(cellValue);
			}

			SelectedCellValueCount = valueAggregator.Count;
			SelectedCellDistinctValueCount = valueAggregator.DistinctCount;

			if (valueAggregator.Count > 0)
			{
				SelectedCellSum = valueAggregator.Sum;
				SelectedCellMin = valueAggregator.Minimum;
				SelectedCellMax = valueAggregator.Maximum;
				SelectedCellAverage = valueAggregator.Average;

				IsSelectedCellLimitInfoVisible = valueAggregator.LimitValuesAvailable;
				IsSelectedCellAggregatedInfoVisible = valueAggregator.AggregatedValuesAvailable;
			}
			else
			{
				IsSelectedCellLimitInfoVisible = false;
				IsSelectedCellAggregatedInfoVisible = false;
			}

			IsSelectedCellInfoVisible = true;
		}

		private void DataGridTabHeaderPopupMouseLeaveHandler(object sender, MouseEventArgs args)
		{
			var child = (FrameworkElement)ResultViewTabHeaderPopup.Child;
			var position = Mouse.GetPosition(child);
			if (position.X < 0 || position.Y < 0 || position.X > child.ActualWidth || position.Y > child.ActualHeight)
			{
				ResultViewTabHeaderPopup.IsOpen = false;
			}
		}

		private void SearchTextChangedHandler(object sender, TextChangedEventArgs args)
		{
			SearchAndHighlightMatches();
		}

		private void SearchAndHighlightMatches()
		{
			var regexPattern = TextSearchHelper.GetRegexPattern(SearchPhraseTextBox.Text);

			if (String.IsNullOrEmpty(regexPattern))
			{
				SearchMatchCount = null;
			}
			else
			{
				var totalMatchCount = 0;
				var regex = BuildSearchRegularExpression(regexPattern);
				foreach (var row in _resultRows)
				{
					foreach (var item in row)
					{
						var stringValue = (string)CellValueConverter.Instance.Convert(item, null, null, null);
						totalMatchCount += regex.Matches(stringValue).Count;
					}
				}

				SearchMatchCount = totalMatchCount;
			}

			HighlightSearchedText();
		}

		private static Regex BuildSearchRegularExpression(string regexPattern)
		{
			return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		}

		private void HighlightSearchedText()
		{
			var regexPattern = TextSearchHelper.GetRegexPattern(SearchPhraseTextBox.Text);
			_searchedTextHighlightUsed |= !String.IsNullOrEmpty(regexPattern);

			foreach (var row in ResultGrid.GetDataGridRows())
			{
				if (row == null || !ResultGrid.IsInViewport(row))
				{
					continue;
				}

				row.HighlightTextItems(regexPattern);
			}
		}

		private void SearchPanelCloseClickHandler(object sender, ExecutedRoutedEventArgs args)
		{
			SearchPanel.Visibility = Visibility.Collapsed;
			SearchPhraseTextBox.Text = String.Empty;
			_lastSearchedCell = LastSearchedCell.Empty;
		}

		private void SearchPanelOpenClickHandler(object sender, ExecutedRoutedEventArgs args)
		{
			SearchPanel.Visibility = Visibility.Visible;
			SearchPhraseTextBox.Focus();
		}

		private void ResultViewerDataGridKeyDownHandler(object sender, KeyEventArgs args)
		{
			var reraisedEvent =
				new KeyEventArgs(args.KeyboardDevice, args.InputSource, args.Timestamp, args.Key)
				{
					RoutedEvent = Keyboard.KeyDownEvent,
					Source = args.OriginalSource
				};

			_outputViewer.RaiseEvent(reraisedEvent);
		}

		private void SearchPhraseTextBoxKeyDownHandler(object sender, KeyEventArgs args)
		{
			if (args.Key != Key.Enter)
			{
				return;
			}

			ScrollToNextSearchedCell();
		}

		private void ScrollToNextSearchedCell()
		{
			var regexPattern = TextSearchHelper.GetRegexPattern(SearchPhraseTextBox.Text);
			var regex = BuildSearchRegularExpression(regexPattern);

			var rowIndex = 0;
			foreach (var row in _resultRows)
			{
				var columnIndex = 0;
				foreach (var item in row)
				{
					var stringValue = (string)CellValueConverter.Instance.Convert(item, null, null, null);
					var matchCount = regex.Matches(stringValue).Count;
					if (matchCount > 0 &&
						(rowIndex > _lastSearchedCell.Row || (rowIndex == _lastSearchedCell.Row && columnIndex > _lastSearchedCell.Column)))
					{
						_lastSearchedCell = new LastSearchedCell(rowIndex, columnIndex);
						ResultGrid.GetCell(rowIndex, columnIndex);
						return;
					}

					columnIndex++;
				}

				rowIndex++;
			}
		}

		private struct LastSearchedCell
		{
			public static readonly LastSearchedCell Empty = new LastSearchedCell(-1, -1);

			public readonly int Row;
			public readonly int Column;

			public LastSearchedCell(int row, int column)
			{
				Row = row;
				Column = column;
			}
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
