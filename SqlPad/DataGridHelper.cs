using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SqlPad
{
	internal class DataGridHelper
	{
		public static readonly Thickness DefaultTextBlockMargin = new Thickness(2, 0, 2, 0);

		public static void InitializeDataGridColumns(DataGrid dataGrid, IEnumerable<ColumnHeader> columnHeaders, IStatementValidator statementValidator, IConnectionAdapter connectionAdapter)
		{
			dataGrid.Columns.Clear();

			foreach (var columnHeader in columnHeaders)
			{
				var columnTemplate = CreateDataGridTemplateColumn(columnHeader, statementValidator, connectionAdapter);
				dataGrid.Columns.Add(columnTemplate);
			}

			dataGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
		}

		public static DataGridColumn CreateDataGridTemplateColumn(ColumnHeader columnHeader, IStatementValidator statementValidator = null, IConnectionAdapter connectionAdapter = null)
		{
			var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
			textBoxFactory.SetValue(TextBoxBase.IsReadOnlyProperty, true);
			textBoxFactory.SetValue(TextBoxBase.IsReadOnlyCaretVisibleProperty, true);
			var valueBinding = new Binding($"[{columnHeader.ColumnIndex}]") { Converter = CellValueConverter.Instance };
			textBoxFactory.SetBinding(TextBox.TextProperty, valueBinding);
			var editingDataTemplate = new DataTemplate(typeof(DependencyObject)) { VisualTree = textBoxFactory };

			var columnTemplate =
				new DataGridTemplateColumn
				{
					Header = columnHeader,
					CellTemplateSelector = new ResultSetDataGridTemplateSelector(statementValidator, connectionAdapter, columnHeader),
					CellEditingTemplate = editingDataTemplate,
					ClipboardContentBinding = valueBinding
				};

			if (columnHeader.DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte)))
			{
				columnTemplate.HeaderStyle = (Style)Application.Current.Resources["HeaderStyleRightAlign"];
				columnTemplate.CellStyle = (Style)Application.Current.Resources["CellStyleRightAlign"];
			}

			return columnTemplate;
		}

		public static bool CanBeRecycled(UIElement uiElement)
		{
			var row = (DataGridRow)uiElement;
			var cellPresenter = row.FindChildVisual<DataGridCellsPresenter>();
			var columnCount = cellPresenter.ItemContainerGenerator.Items.Count;
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

		public static void ShowLargeValueEditor(DataGrid dataGrid)
		{
			var currentRowValues = (object[])dataGrid.CurrentItem;
			if (currentRowValues == null || dataGrid.CurrentColumn == null)
			{
				return;
			}

			var columnIndex = dataGrid.Columns.IndexOf(dataGrid.CurrentColumn);
			if (columnIndex == -1 || columnIndex >= currentRowValues.Length)
			{
				return;
			}

			var cellValue = currentRowValues[columnIndex];
			var largeValue = cellValue as ILargeValue;
			if (largeValue != null)
			{
				new LargeValueEditor(((ColumnHeader)dataGrid.CurrentColumn.Header).Name, largeValue) { Owner = Window.GetWindow(dataGrid) }.ShowDialog();
			}
		}

		public static async void BuildDataGridCellContent(DataGridCell cell, Func<CancellationToken, Task<FrameworkElement>> getContentFunction)
		{
			var originalContent = cell.Content;

			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				FrameworkElement element;

				KeyEventHandler keyDownHandler = (sender, args) => cancellationTokenSource.CancelOnEscape(args.Key);

				try
				{
					cell.KeyDown += keyDownHandler;
					cell.Content = CreateTextBlock("Loading... ");
					element = await getContentFunction(cancellationTokenSource.Token);
				}
				catch (OperationCanceledException)
				{
					cell.Content = originalContent;
					return;
				}
				catch (Exception exception)
				{
					var textBlock = CreateTextBlock(exception.Message);
					textBlock.Background = Brushes.Red;
					element = textBlock;
				}
				finally
				{
					cell.KeyDown -= keyDownHandler;
				}

				cell.Content = ConfigureAndWrapUsingScrollViewerIfNeeded(cell, originalContent, element);
			}
		}

		private static TextBlock CreateTextBlock(string text)
		{
			return new TextBlock { Text = text, TextAlignment = TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = DefaultTextBlockMargin };
		}

		private static FrameworkElement ConfigureAndWrapUsingScrollViewerIfNeeded(Visual cell, object originalContent, FrameworkElement contentContainer)
		{
			var row = cell.FindParentVisual<DataGridRow>();
			var dataGrid = row.FindParentVisual<DataGrid>();
			var dockPanel = dataGrid.Parent as DockPanel;
			if (dockPanel != null)
			{
				var headersPresenter = dataGrid.FindChildVisual<DataGridColumnHeadersPresenter>();

				contentContainer =
					new ScrollViewer
					{
						Content = contentContainer,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
						MaxHeight = dockPanel.ActualHeight - headersPresenter.ActualHeight - SystemParameters.HorizontalScrollBarHeight - 4
					};
			}

			row.Height = Double.NaN;

			contentContainer.Tag = originalContent;
			contentContainer.KeyDown += ContentContainerKeyDownHandler;
			return contentContainer;
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
			//var row = cell.FindParentVisual<DataGridRow>();
			//row.Height = 21;

			keyEventArgs.Handled = true;
		}
	}
}