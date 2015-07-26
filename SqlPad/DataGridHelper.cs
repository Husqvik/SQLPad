using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlPad
{
	internal class DataGridHelper
	{
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
			textBoxFactory.SetBinding(TextBox.TextProperty, new Binding($"[{columnHeader.ColumnIndex}]") { Converter = CellValueConverter.Instance });
			var editingDataTemplate = new DataTemplate(typeof(DependencyObject)) { VisualTree = textBoxFactory };

			var columnTemplate =
				new DataGridTemplateColumn
				{
					Header = columnHeader,
					CellTemplateSelector = new ResultSetDataGridTemplateSelector(statementValidator, connectionAdapter, columnHeader),
					CellEditingTemplate = editingDataTemplate
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

		public static FrameworkElement ConfigureAndWrapUsingScrollViewerIfNeeded(DataGridCell cell, FrameworkElement contentContainer)
		{
			var row = cell.FindParent<DataGridRow>();
			var dataGrid = row.FindParent<DataGrid>();
			var dockPanel = dataGrid.Parent as DockPanel;
			if (dockPanel != null)
			{
				var headersPresenter = dataGrid.FindVisualChild<DataGridColumnHeadersPresenter>();

				contentContainer =
					new ScrollViewer
					{
						Content = contentContainer,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
						MaxHeight = dockPanel.ActualHeight - headersPresenter.ActualHeight
					};
			}

			row.Height = Double.NaN;

			contentContainer.Tag = cell.Content;
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
			//var row = cell.FindParent<DataGridRow>();
			//row.Height = 21;

			keyEventArgs.Handled = true;
		}
	}
}