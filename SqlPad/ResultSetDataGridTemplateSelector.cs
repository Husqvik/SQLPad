using System;
using System.Collections.Generic;
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
		private readonly int _columnIndex;
		private readonly bool _hasReferenceConstraint;
		private readonly IConnectionAdapter _connectionAdapter;
		private readonly ColumnHeader _columnHeader;
		private readonly DataTemplate _textDataTemplate;
		private readonly DataTemplate _hyperLinkDataTemplate;

		public ResultSetDataGridTemplateSelector(IConnectionAdapter connectionAdapter, ColumnHeader columnHeader)
		{
			_connectionAdapter = connectionAdapter;
			_columnHeader = columnHeader;
			_columnIndex = columnHeader.ColumnIndex;
			_hasReferenceConstraint = columnHeader.ReferenceDataSource != null;
			_textDataTemplate = CreateTextDataTemplate();
			_hyperLinkDataTemplate = CreateHyperlinkDataTemplate();
		}

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item == null)
			{
				return null;
			}

			var rowValues = (object[])item;
			return rowValues[_columnIndex] == DBNull.Value || !_hasReferenceConstraint
				? _textDataTemplate
				: _hyperLinkDataTemplate;
		}

		private DataTemplate CreateTextDataTemplate()
		{
			var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
			textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding($"[{_columnIndex}]") { Converter = CellValueConverter.Instance });
			return new DataTemplate(typeof(DependencyObject)) { VisualTree = textBlockFactory };
		}

		private DataTemplate CreateHyperlinkDataTemplate()
		{
			var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
			var hyperlinkFactory = new FrameworkElementFactory(typeof(Hyperlink));
			var runFactory = new FrameworkElementFactory(typeof(Run));
			runFactory.SetBinding(Run.TextProperty, new Binding($"[{_columnIndex}]") { Converter = CellValueConverter.Instance });
			hyperlinkFactory.AppendChild(runFactory);
			hyperlinkFactory.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)CellHyperlinkClickHandler);
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

			var currentRowValues = (object[])hyperlink.DataContext;
			var executionModel = _columnHeader.ReferenceDataSource.CreateExecutionModel();
			executionModel.BindVariables[0].Value = currentRowValues[_columnIndex];

			var row = (DataGridRow)hyperlink.Tag;
		    var cellPresenter = row.FindVisualChild<DataGridCellsPresenter>();
			var cell = (DataGridCell)cellPresenter.ItemContainerGenerator.ContainerFromIndex(_columnIndex);

			StatementExecutionResult result;

			try
			{
				result = await _connectionAdapter.ExecuteChildStatementAsync(executionModel, CancellationToken.None);
			}
			catch (Exception e)
			{
				cell.Content = new TextBlock { Text = e.Message, Background = Brushes.Red };
				return;
			}

			if (result.InitialResultSet.Count == 0)
			{
				cell.Content = "Record not found. ";
				return;
			}

			var firstRow = result.InitialResultSet[0];
			var columnValues = result.ColumnHeaders.Select(
				(t, i) => new CustomTypeAttributeValue
				{
					ColumnHeader = t,
					Value = firstRow[i]
				}).ToArray();

			var record =
				new SingleRecord
				{
					DataTypeName = _columnHeader.ReferenceDataSource.ObjectName,
					Attributes = columnValues
				};

			row.Height = Double.NaN;
			var complexTypeViewer =
				new ComplexTypeViewer
				{
					ComplexType = record,
					GridTitle = "Source object: ",
					RowTitle = "Column name"
				};

            var dataGrid = row.FindParent<DataGrid>();
            var headersPresenter = dataGrid.FindVisualChild<DataGridColumnHeadersPresenter>();

            cell.Content =
				new ScrollViewer
				{
					Content = complexTypeViewer,
					HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = dataGrid.ActualHeight - headersPresenter.ActualHeight
                };
		}
	}

    internal class SingleRecord : IComplexType
	{
		public bool IsNull => false;

	    public string ToSqlLiteral()
		{
			throw new NotImplementedException();
		}

		public string ToXml()
		{
			throw new NotImplementedException();
		}

		public string ToJson()
		{
			throw new NotImplementedException();
		}

		public string DataTypeName { get; set; }

		public bool IsEditable { get { throw new NotImplementedException(); } }

		public long Length { get { throw new NotImplementedException(); } }

		public void Prefetch()
		{
			throw new NotImplementedException();
		}

		public IReadOnlyList<CustomTypeAttributeValue> Attributes { get; set; }
	}
}
