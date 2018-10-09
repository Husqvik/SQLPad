using System;
using System.Collections.Generic;
using System.Globalization;
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
	internal static class DataGridHelper
	{
		public static readonly Object TagHighlight = new Object();
		public static readonly Thickness DefaultTextBlockMargin = new Thickness(2, 0, 2, 0);
		public static double DefaultRowHeight = 21;

		public static void InitializeDataGridColumns(DataGrid dataGrid, IEnumerable<ColumnHeader> columnHeaders, IStatementValidator statementValidator, IConnectionAdapter connectionAdapter)
		{
			dataGrid.Columns.Clear();

			foreach (var columnHeader in columnHeaders)
			{
				var columnTemplate = CreateDataGridTemplateColumn(columnHeader, statementValidator, connectionAdapter);
				dataGrid.Columns.Add(columnTemplate);
			}
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
					CellTemplateSelector = new ResultSetDataGridTemplateSelector(statementValidator, connectionAdapter, columnHeader),
					CellEditingTemplate = editingDataTemplate,
					ClipboardContentBinding = valueBinding
				};

			ApplyColumnStyle(columnTemplate, columnHeader);

			return columnTemplate;
		}

		public static void ApplyColumnStyle(DataGridColumn columnTemplate, ColumnHeader columnHeader)
		{
			columnTemplate.Header = columnHeader;

			if (!columnHeader.IsNumeric)
			{
				return;
			}

			columnTemplate.HeaderStyle = (Style)Application.Current.Resources["HeaderStyleRightAlign"];
			columnTemplate.CellStyle = (Style)Application.Current.Resources["CellStyleRightAlign"];
		}

		public static bool CanBeRecycled(UIElement row)
		{
			var typedRow = (DataGridRow)row;
			var cellPresenter = typedRow.FindChildVisual<DataGridCellsPresenter>();
			var columnCount = cellPresenter.ItemContainerGenerator.Items.Count;
			for (var index = 0; index < columnCount; index++)
			{
				var cell = (DataGridCell)cellPresenter.ItemContainerGenerator.ContainerFromIndex(index);
				if (cell == null)
				{
					return false;
				}

				var contentPresenter = cell.Content as ContentPresenter;
				if (contentPresenter == null || contentPresenter.Tag != TagHighlight)
				{
					return true;
				}
			}

			return false;
		}

		public static void ShowLargeValueEditor(DataGrid dataGrid, Func<object, object[]> getRowValuesFunction = null)
		{
			var currentRowValues = getRowValuesFunction == null
				? (object[])dataGrid.CurrentItem
				: getRowValuesFunction(dataGrid.CurrentItem);

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
			if (cellValue is ILargeValue largeValue)
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

				void KeyDownHandler(object sender, KeyEventArgs args) => cancellationTokenSource.CancelOnEscape(args.Key);

				try
				{
					cell.KeyDown += KeyDownHandler;
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
					cell.KeyDown -= KeyDownHandler;
				}

				cell.Content = ConfigureAndWrapUsingScrollViewerIfNeeded(cell, originalContent, element);
			}
		}

		public static IEnumerable<DataGridRow> GetDataGridRows(this DataGrid dataGrid)
		{
			if (dataGrid.ItemsSource == null)
			{
				yield break;
			}

			foreach (var item in dataGrid.ItemsSource)
			{
				yield return (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
			}
		}

		public static DataGridRow GetRow(this DataGrid dataGrid, int rowIndex)
		{
			dataGrid.UpdateLayout();
			dataGrid.ScrollIntoView(dataGrid.Items[rowIndex]);

			return (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
		}

		public static DataGridCell GetCell(this DataGrid dataGrid, int rowIndex, int columnIndex)
		{
			var rowContainer = dataGrid.GetRow(rowIndex);
			if (rowContainer == null)
			{
				throw new ArgumentException("Row index was not found. ", nameof(rowIndex));
			}

			dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[columnIndex]);
			var presenter = rowContainer.FindChildVisual<DataGridCellsPresenter>();

			return (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);
		}

		private static TextBlock CreateTextBlock(string text)
		{
			return
				new TextBlock
				{
					Text = text,
					TextAlignment = TextAlignment.Left,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = DefaultTextBlockMargin
				};
		}

		private static FrameworkElement ConfigureAndWrapUsingScrollViewerIfNeeded(Visual cell, object originalContent, FrameworkElement contentContainer)
		{
			var row = cell.FindParentVisual<DataGridRow>();
			var dataGrid = row.FindParentVisual<DataGrid>();
			if (dataGrid.Parent is DockPanel dockPanel)
			{
				var headersPresenter = dataGrid.FindChildVisual<DataGridColumnHeadersPresenter>();

				contentContainer =
					new ScrollViewer
					{
						Content = contentContainer,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto
					};

				var scrollViewer = dataGrid.FindChildVisual<ScrollViewer>();

				var binding = new MultiBinding { Converter = ChildContainerHeightConverter.Instance };
				binding.Bindings.Add(new Binding(nameof(dockPanel.ActualHeight)) { Source = dockPanel });
				binding.Bindings.Add(new Binding(nameof(headersPresenter.ActualHeight)) { Source = headersPresenter });
				binding.Bindings.Add(new Binding(nameof(scrollViewer.ComputedHorizontalScrollBarVisibility)) { Source = scrollViewer });

				contentContainer.SetBinding(FrameworkElement.MaxHeightProperty, binding);

				var nestedContainer = (NestedContainer)(row.Tag ?? (row.Tag = new NestedContainer()));
				nestedContainer.Items.Add(contentContainer);
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
			var row = cell.FindParentVisual<DataGridRow>();
			var nestedContainer = (NestedContainer)row.Tag;
			if (nestedContainer != null)
			{
				nestedContainer.Items.Remove(element);
				if (nestedContainer.Items.Count == 0)
				{
					row.Height = DefaultRowHeight;
				}
			}

			keyEventArgs.Handled = true;
		}

		private class ChildContainerHeightConverter : IMultiValueConverter
		{
			public static readonly ChildContainerHeightConverter Instance = new ChildContainerHeightConverter();

			public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			{
				var dockPanelActualHeight = (double)values[0];
				var headersPresenterActualHeight = (double)values[1];
				var dataGridComputedHorizontalScrollBarVisibility = (Visibility)values[2];
				var scrollBarOffsetMultiplier = dataGridComputedHorizontalScrollBarVisibility == Visibility.Collapsed ? 1 : 2;
				return dockPanelActualHeight - headersPresenterActualHeight - scrollBarOffsetMultiplier * SystemParameters.HorizontalScrollBarHeight - 8;
			}

			public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			{
				throw new NotSupportedException();
			}
		}

		private class NestedContainer
		{
			public List<FrameworkElement> Items { get; } = new List<FrameworkElement>();
		}
	}
}