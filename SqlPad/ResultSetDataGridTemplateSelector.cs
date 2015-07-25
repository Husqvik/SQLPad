using System;
using System.Collections.Generic;
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

namespace SqlPad
{
	internal class ResultSetDataGridTemplateSelector : DataTemplateSelector
	{
		private static readonly Style ColumnHeaderClickBubbleCancelation = new Style();
		private static readonly DataTemplate EditingTemplate = new DataTemplate(typeof(TextBox));

		private readonly int _columnIndex;
		private readonly bool _hasReferenceConstraint;
		private readonly IStatementValidator _statementValidator;
		private readonly IConnectionAdapter _connectionAdapter;
		private readonly ColumnHeader _columnHeader;
		protected readonly DataTemplate TextDataTemplate;
		protected readonly DataTemplate HyperlinkDataTemplate;

		static ResultSetDataGridTemplateSelector()
		{
			ColumnHeaderClickBubbleCancelation.Setters.Add(new EventSetter(ButtonBase.ClickEvent, new RoutedEventHandler((s, args) => args.Handled = true)));

			var textBoxFactory = new FrameworkElementFactory(typeof (TextBox));
			textBoxFactory.SetValue(FrameworkElement.StyleProperty, Application.Current.Resources["EditingCellTextblockLeftAlign"]);
			textBoxFactory.SetBinding(TextBox.TextProperty, new Binding("Value") {Converter = CellValueConverter.Instance});
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
		}

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item == null)
			{
				return null;
			}

			var rowValues = (object[])item;
			return rowValues[_columnIndex] == DBNull.Value || !_hasReferenceConstraint
				? TextDataTemplate
				: HyperlinkDataTemplate;
		}

		protected virtual IEnumerable<StatementExecutionModel> BuildExecutionModels(DataGridRow row)
		{
			var currentRowValues = (object[])row.DataContext;
			return _columnHeader.ParentReferenceDataSources
				.Select(s => s.CreateExecutionModel(new [] { currentRowValues[_columnIndex] }));
		}

		protected virtual int ColumnIndex => _columnIndex;

		protected virtual IReadOnlyCollection<IReferenceDataSource> GetReferenceDataSources(DataGridRow row) => _columnHeader.ParentReferenceDataSources;

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
			hyperlinkFactory.SetBinding(FrameworkContentElement.TagProperty, new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1) });
			textBlockFactory.AppendChild(hyperlinkFactory);
			return new DataTemplate(typeof(DependencyObject)) { VisualTree = textBlockFactory };
		}

		private async void CellHyperlinkClickHandler(object sender, RoutedEventArgs args)
		{
			var hyperlink = args.OriginalSource as Hyperlink;
			if (hyperlink == null)
			{
				return;
			}

			var row = (DataGridRow)hyperlink.Tag;
			var cellPresenter = row.FindVisualChild<DataGridCellsPresenter>();
			var cell = (DataGridCell)cellPresenter.ItemContainerGenerator.ContainerFromIndex(ColumnIndex);

			var stackPanel = new StackPanel();
			var index = 0;
			var references = GetReferenceDataSources(row).ToArray();
			foreach (var executionModel in BuildExecutionModels(row))
			{
				await BuildParentRecordDataGrid(stackPanel, references[index++].ObjectName, executionModel);
			}

			row.Height = Double.NaN;
			var dataGrid = row.FindParent<DataGrid>();
			var headersPresenter = dataGrid.FindVisualChild<DataGridColumnHeadersPresenter>();

			FrameworkElement contentcontainer = stackPanel;
			var dockPanel = dataGrid.Parent as DockPanel;
			if (dockPanel != null)
			{
				contentcontainer =
					new ScrollViewer
					{
						Content = stackPanel,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
						MaxHeight = dockPanel.ActualHeight - headersPresenter.ActualHeight
					};
			}

			contentcontainer.Tag = cell.Content;
            contentcontainer.KeyDown += ContentContainerKeyDownHandler;

			cell.Content = contentcontainer;
		}

		private async Task BuildParentRecordDataGrid(Panel container, string objectName, StatementExecutionModel executionModel)
		{
			StatementExecutionResult executionResult;

			try
			{
				var cancellationToken = CancellationToken.None;
				executionResult = await _connectionAdapter.ExecuteChildStatementAsync(executionModel, cancellationToken);
				await _statementValidator.ApplyReferenceConstraintsAsync(executionResult, _connectionAdapter.DatabaseModel, cancellationToken);
			}
			catch (Exception e)
			{
				container.Children.Add(new TextBlock { Text = e.Message, Background = Brushes.Red });
				return;
			}

			if (executionResult.InitialResultSet.Count == 0)
			{
				container.Children.Add(new TextBlock { Text = "Record not found. " });
				return;
			}

			var firstRow = executionResult.InitialResultSet[0];
			var columnValues = executionResult.ColumnHeaders.Select(
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
						ItemsSource = columnValues
					};

			referenceDataGrid.BeginningEdit += App.ResultGridBeginningEditCancelTextInputHandlerImplementation;
			//referenceDataGrid.Sorting += (sender, args) => args.Handled = args.Column.DisplayIndex != 0;

			var columnNameTemplate =
				new DataGridTextColumn
				{
					Header = "Column name",
					Binding = new Binding("ColumnHeader.Name"),
					HeaderStyle = ColumnHeaderClickBubbleCancelation,
					ElementStyle = (Style)Application.Current.Resources["CellTextblockLeftAlign"],
					EditingElementStyle = (Style)Application.Current.Resources["EditingCellTextblockLeftAlign"]
				};

			referenceDataGrid.Columns.Add(columnNameTemplate);

			var columnHyperlinkValueTemplate =
				new DataGridTemplateColumn
				{
					Header = "Value",
					HeaderStyle = ColumnHeaderClickBubbleCancelation,
					CellTemplateSelector = new SingleRowDataTemplateSelector(_statementValidator, _connectionAdapter),
					CellEditingTemplate = EditingTemplate
				};

			referenceDataGrid.Columns.Add(columnHyperlinkValueTemplate);

			var textBlock = new TextBlock { Text = $"Source: {objectName}", Margin = new Thickness(2, 0, 2, 0), HorizontalAlignment = HorizontalAlignment.Left };
			container.Children.Add(textBlock);
			container.Children.Add(referenceDataGrid);
		}

		private static void ContentContainerKeyDownHandler(object sender, KeyEventArgs keyEventArgs)
		{
			if (keyEventArgs.Key != Key.Escape)
			{
				return;
			}

			var element = (FrameworkElement)sender;
			var cell = (DataGridCell)element.Parent;
			cell.Content = element.Tag;
			//var row = cell.FindParent<DataGridRow>();
			//row.Height = 21;

			keyEventArgs.Handled = true;
		}

		public static bool CanBeRecycled(UIElement uiElement)
		{
			var row = (DataGridRow)uiElement;
			var cellPresenter = row.FindVisualChild<DataGridCellsPresenter>();
			var columnCount = ((object[])row.DataContext).Length;
			for (var index = 0; index < columnCount; index++)
			{
				var cell = (DataGridCell)cellPresenter.ItemContainerGenerator.ContainerFromIndex(index);
				if (!(cell?.Content is ContentPresenter))
				{
					return true;
				}
			}

			return false;
		}
	}

	internal class SingleRowDataTemplateSelector : ResultSetDataGridTemplateSelector
	{
		public SingleRowDataTemplateSelector(IStatementValidator statementValidator, IConnectionAdapter connectionAdapter)
			: base(statementValidator, connectionAdapter, "Value")
		{
			var textBlockFactory = TextDataTemplate.VisualTree;
			textBlockFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);

			textBlockFactory = HyperlinkDataTemplate.VisualTree;
			textBlockFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		}

		protected override IEnumerable<StatementExecutionModel> BuildExecutionModels(DataGridRow row)
		{
			var customTypeAttributeValue = (CustomTypeAttributeValue)row.DataContext;
			return customTypeAttributeValue.ColumnHeader.ParentReferenceDataSources
				.Select(s => s.CreateExecutionModel(new [] { customTypeAttributeValue.Value }));
		}

		protected override IReadOnlyCollection<IReferenceDataSource> GetReferenceDataSources(DataGridRow row)
		{
			var customTypeAttributeValue = (CustomTypeAttributeValue)row.DataContext;
			return customTypeAttributeValue.ColumnHeader.ParentReferenceDataSources;
		}

		protected override int ColumnIndex => 1;

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
