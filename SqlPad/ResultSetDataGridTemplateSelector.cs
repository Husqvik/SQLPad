using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace SqlPad
{
	internal class ResultSetDataGridTemplateSelector : DataTemplateSelector
	{
		private static readonly DataTemplate EditingTemplate = new DataTemplate(typeof(TextBox));
		protected const string ValueProperty = nameof(CustomTypeAttributeValue.Value);

		private readonly int _columnIndex;
		private readonly bool _hasReferenceConstraint;
		private readonly IStatementValidator _statementValidator;
		private readonly IConnectionAdapter _connectionAdapter;
		private readonly ColumnHeader _columnHeader;
		protected readonly DataTemplate TextDataTemplate;
		protected readonly DataTemplate HyperlinkDataTemplate;
		protected readonly DataTemplate BarChartDataTemplate;

		public bool UseBarChart { get; set; }

		static ResultSetDataGridTemplateSelector()
		{
			var textBoxFactory = new FrameworkElementFactory(typeof (TextBox));
			textBoxFactory.SetValue(FrameworkElement.StyleProperty, Application.Current.Resources["EditingCellTextBox"]);
			textBoxFactory.SetBinding(TextBox.TextProperty, new Binding(ValueProperty) { Converter = CellValueConverter.Instance });
			EditingTemplate.VisualTree = textBoxFactory;
		}

		public ResultSetDataGridTemplateSelector(IStatementValidator statementValidator, IConnectionAdapter connectionAdapter, ColumnHeader columnHeader)
			: this(statementValidator, connectionAdapter, $"[{columnHeader.ColumnIndex}]")
		{
			_columnHeader = columnHeader;
			_columnIndex = columnHeader.ColumnIndex;
			_hasReferenceConstraint = columnHeader.ParentReferenceDataSources?.Count > 0;
		}

		protected ResultSetDataGridTemplateSelector(IStatementValidator statementValidator, IConnectionAdapter connectionAdapter, string bindingPath)
		{
			_statementValidator = statementValidator;
			_connectionAdapter = connectionAdapter;
			TextDataTemplate = CreateTextDataTemplate(bindingPath);
			HyperlinkDataTemplate = CreateHyperlinkDataTemplate(bindingPath, CellHyperlinkClickHandler);
			BarChartDataTemplate = CreateBarChartDataTemplate(bindingPath);
		}

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item == null)
			{
				return null;
			}

			if (UseBarChart)
			{
				return BarChartDataTemplate;
			}

			var rowValues = (object[])item;
			return rowValues[_columnIndex] == DBNull.Value || !_hasReferenceConstraint
				? TextDataTemplate
				: HyperlinkDataTemplate;
		}

		protected virtual IEnumerable<StatementExecutionModel> BuildExecutionModels(DataGridCell cell)
		{
			var currentRowValues = (object[])cell.DataContext;
			return _columnHeader.ParentReferenceDataSources
				.Select(s => s.CreateExecutionModel(new [] { currentRowValues[_columnIndex] }));
		}

		protected virtual IReadOnlyCollection<IReferenceDataSource> GetReferenceDataSources(DataGridCell cell) => _columnHeader.ParentReferenceDataSources;

		private static DataTemplate CreateTextDataTemplate(string bindingPath)
		{
			var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
			textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding(bindingPath) { Converter = CellValueConverter.Instance });
			return new DataTemplate(typeof(DependencyObject)) { VisualTree = textBlockFactory };
		}

		private static DataTemplate CreateHyperlinkDataTemplate(string bindingPath, RoutedEventHandler hyperLinkClickHandler)
		{
			var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
			var hyperlinkFactory = new FrameworkElementFactory(typeof(Hyperlink));
			var runFactory = new FrameworkElementFactory(typeof(Run));
			runFactory.SetBinding(Run.TextProperty, new Binding(bindingPath) { Converter = CellValueConverter.Instance, Mode = BindingMode.OneWay });
			hyperlinkFactory.AppendChild(runFactory);
			hyperlinkFactory.AddHandler(Hyperlink.ClickEvent, hyperLinkClickHandler);
			hyperlinkFactory.SetBinding(FrameworkContentElement.TagProperty, new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1) });
			textBlockFactory.AppendChild(hyperlinkFactory);
			return new DataTemplate(typeof(DependencyObject)) { VisualTree = textBlockFactory };
		}

		private static DataTemplate CreateBarChartDataTemplate(string bindingPath)
		{
			var progressBarFactory = new FrameworkElementFactory(typeof(ProgressBar));
			return new DataTemplate(typeof(DependencyObject)) { VisualTree = progressBarFactory };
		}

		private void CellHyperlinkClickHandler(object sender, RoutedEventArgs args)
		{
			var cell = (DataGridCell)(sender as Hyperlink)?.Tag;
			DataGridHelper.BuildDataGridCellContent(cell, t => BuildParentRecordDataGrids(cell, t));
		}

		private async Task<FrameworkElement> BuildParentRecordDataGrids(DataGridCell cell, CancellationToken cancellationToken)
		{
			var references = GetReferenceDataSources(cell).ToArray();
			var stackPanel = new StackPanel();
			var index = 0;
			foreach (var executionModel in BuildExecutionModels(cell))
			{
				await BuildParentRecordDataGrid(stackPanel, references[index++].ObjectName, executionModel, cancellationToken);
			}

			return stackPanel;
		}

		private async Task BuildParentRecordDataGrid(Panel container, string objectName, StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var executionResult = await _connectionAdapter.ExecuteChildStatementAsync(executionModel, cancellationToken);
			await _statementValidator.ApplyReferenceConstraintsAsync(executionResult, _connectionAdapter.DatabaseModel, cancellationToken);

			var resultInfo = executionResult.ResultInfoColumnHeaders.Keys.Last();
			var resultSet = await _connectionAdapter.FetchRecordsAsync(resultInfo, ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize, cancellationToken);
			if (resultSet.Count == 0)
			{
				container.Children.Add(new TextBlock { Text = "Record not found. " });
				return;
			}

			var firstRow = resultSet[0];
			var columnHeaders = executionResult.ResultInfoColumnHeaders.Values.Last();
			var columnValues = columnHeaders.Select(
				(t, i) => new CustomTypeAttributeValue
				{
					ColumnHeader = t,
					Value = firstRow[i]
				}).ToArray();

			var referenceDataGrid =
				new DataGrid
				{
					Style = (Style)Application.Current.Resources["ResultSetDataGrid"],
					RowHeaderWidth = 0,
					CanUserReorderColumns = false,
					ItemsSource = columnValues,
					CanUserSortColumns = true
				};

			referenceDataGrid.BeginningEdit += App.DataGridBeginningEditCancelTextInputHandlerImplementation;
			referenceDataGrid.Sorting += (sender, args) => args.Handled = args.Column.DisplayIndex != 0;
			referenceDataGrid.MouseDoubleClick += ReferenceDataGridOnMouseDoubleClickHandler;

			var columnNameTemplate =
				new DataGridTextColumn
				{
					Header = "Column name",
					Binding = new Binding("ColumnHeader.Name"),
					HeaderStyle = (Style)Application.Current.Resources["ColumnHeaderClickBubbleCancelation"],
					ElementStyle = (Style)Application.Current.Resources["SingleRecordColumnName"],
					CellStyle = (Style)Application.Current.Resources["SingleRecordCell"],
					EditingElementStyle = (Style)Application.Current.Resources["EditingCellTextBox"]
				};

			referenceDataGrid.Columns.Add(columnNameTemplate);

			var columnHyperlinkValueTemplate =
				new DataGridTemplateColumn
				{
					Header = ValueProperty,
					HeaderStyle = (Style)Application.Current.Resources["ColumnHeaderClickBubbleCancelation"],
					CellTemplateSelector = new SingleRowDataTemplateSelector(_statementValidator, _connectionAdapter),
					CellEditingTemplate = EditingTemplate,
					ClipboardContentBinding = new Binding(ValueProperty) { Converter = CellValueConverter.Instance }
				};

			referenceDataGrid.Columns.Add(columnHyperlinkValueTemplate);

			var textBlock = new TextBlock { Text = $"Source: {objectName}", Margin = DataGridHelper.DefaultTextBlockMargin, HorizontalAlignment = HorizontalAlignment.Left };
			container.Children.Add(textBlock);
			container.Children.Add(referenceDataGrid);
		}

		private static void ReferenceDataGridOnMouseDoubleClickHandler(object sender, MouseButtonEventArgs args)
		{
			var dataGrid = (DataGrid)sender;
			if (dataGrid.CurrentItem == null || dataGrid.CurrentColumn is DataGridTextColumn)
			{
				return;
			}

			var customTypeValue = (CustomTypeAttributeValue)dataGrid.CurrentItem;
			var largeValue = customTypeValue.Value as ILargeValue;
			if (largeValue != null)
			{
				new LargeValueEditor(customTypeValue.ColumnHeader.Name, largeValue) { Owner = Window.GetWindow(dataGrid) }.ShowDialog();
			}
		}
	}

	internal class SingleRowDataTemplateSelector : ResultSetDataGridTemplateSelector
	{
		public SingleRowDataTemplateSelector(IStatementValidator statementValidator, IConnectionAdapter connectionAdapter)
			: base(statementValidator, connectionAdapter, ValueProperty)
		{
			var textBlockFactory = TextDataTemplate.VisualTree;
			textBlockFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);

			textBlockFactory = HyperlinkDataTemplate.VisualTree;
			textBlockFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		}

		protected override IEnumerable<StatementExecutionModel> BuildExecutionModels(DataGridCell cell)
		{
			var customTypeAttributeValue = (CustomTypeAttributeValue)cell.DataContext;
			return customTypeAttributeValue.ColumnHeader.ParentReferenceDataSources
				.Select(s => s.CreateExecutionModel(new [] { customTypeAttributeValue.Value }));
		}

		protected override IReadOnlyCollection<IReferenceDataSource> GetReferenceDataSources(DataGridCell cell)
		{
			var customTypeAttributeValue = (CustomTypeAttributeValue)cell.DataContext;
			return customTypeAttributeValue.ColumnHeader.ParentReferenceDataSources;
		}

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item == null)
			{
				return null;
			}

			var attribute = (CustomTypeAttributeValue)item;
			return attribute.Value == DBNull.Value || attribute.ColumnHeader.ParentReferenceDataSources == null || attribute.ColumnHeader.ParentReferenceDataSources.Count == 0
				? TextDataTemplate
				: HyperlinkDataTemplate;
		}
	}
}
