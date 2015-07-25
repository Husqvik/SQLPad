using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
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
			_hasReferenceConstraint = columnHeader.ReferenceDataSource != null;
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

		protected virtual StatementExecutionModel BuildExecutionModel(DataGridRow row)
		{
			var currentRowValues = (object[])row.DataContext;
			var executionModel = _columnHeader.ReferenceDataSource.CreateExecutionModel();
			executionModel.BindVariables[0].Value = currentRowValues[_columnIndex];
			return executionModel;
		}

		protected virtual int ColumnIndex => _columnIndex;

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
			var executionModel = BuildExecutionModel(row);
			var cellPresenter = row.FindVisualChild<DataGridCellsPresenter>();
			var cell = (DataGridCell)cellPresenter.ItemContainerGenerator.ContainerFromIndex(ColumnIndex);

			StatementExecutionResult executionResult;

			try
			{
				var cancellationToken = CancellationToken.None;
				executionResult = await _connectionAdapter.ExecuteChildStatementAsync(executionModel, cancellationToken);
				await _statementValidator.ApplyReferenceConstraintsAsync(executionResult, _connectionAdapter.DatabaseModel, cancellationToken);
			}
			catch (Exception e)
			{
				cell.Content = new TextBlock { Text = e.Message, Background = Brushes.Red };
				return;
			}

			if (executionResult.InitialResultSet.Count == 0)
			{
				cell.Content = "Record not found. ";
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

			row.Height = Double.NaN;
			var dataGrid = row.FindParent<DataGrid>();
			var headersPresenter = dataGrid.FindVisualChild<DataGridColumnHeadersPresenter>();

			cell.Content =
				new ScrollViewer
				{
					Content = referenceDataGrid,
					HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
					MaxHeight = ((FrameworkElement)dataGrid.Parent).ActualHeight - headersPresenter.ActualHeight
				};
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

		protected override StatementExecutionModel BuildExecutionModel(DataGridRow row)
		{
			var customTypeAttributeValue = (CustomTypeAttributeValue)row.DataContext;
			var executionModel = customTypeAttributeValue.ColumnHeader.ReferenceDataSource.CreateExecutionModel();
			executionModel.BindVariables[0].Value = customTypeAttributeValue.Value;
			return executionModel;
		}

		protected override int ColumnIndex => 1;

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item == null)
			{
				return null;
			}

			var attribute = (CustomTypeAttributeValue)item;
			return attribute.Value == DBNull.Value || attribute.ColumnHeader.ReferenceDataSource == null
				? TextDataTemplate
				: HyperlinkDataTemplate;
		}
	}
}
