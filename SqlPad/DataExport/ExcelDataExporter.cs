using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace SqlPad.DataExport
{
	public class ExcelDataExporter : IDataExporter
	{
		public string Name { get; } = "Excel";

		public string FileNameFilter { get; } = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*";

		public bool HasAppendSupport { get; } = true;

		public Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = (ICollection)resultViewer.ResultGrid.Items;
			var worksheetName = resultViewer.Title;
			return Task.Run(() => ExportInternal(fileName, worksheetName, orderedColumns, rows, reportProgress, cancellationToken), cancellationToken);
		}

		private static void ExportInternal(string fileName, string worksheetName, IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			var exportContext = new ExcelDataExportContext(fileName, worksheetName, orderedColumns, rows.Count, reportProgress, cancellationToken);
			DataExportHelper.ExportRowsUsingContext(rows, exportContext);
		}
	}

	internal class ExcelDataExportContext : DataExportContextBase
	{
		private readonly string _fileName;
		private readonly string _worksheetName;
		private readonly IReadOnlyList<ColumnHeader> _columns;

		private ExcelPackage _package;
		private ExcelWorksheet _worksheet;
		private int _currentRowIndex;

		public ExcelDataExportContext(string fileName, string worksheetName, IReadOnlyList<ColumnHeader> columns, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_fileName = fileName;
			_worksheetName = worksheetName;
			_columns = columns;
		}

		protected override void InitializeExport()
		{
			_currentRowIndex = 1;

			_package = new ExcelPackage(new FileInfo(_fileName));
			if (_package.Workbook.Worksheets[_worksheetName] != null)
			{
				_package.Workbook.Worksheets.Delete(_worksheetName);
			}

			_worksheet = _package.Workbook.Worksheets.Add(_worksheetName);

			for (var i = 0; i < _columns.Count; i++)
			{
				var column = _columns[i];
				var excelColumn = _worksheet.Column(i + 1);

				if (column.DataType == typeof(DateTime))
				{
					excelColumn.Style.Numberformat.Format = ConfigurationProvider.Configuration.ResultGrid.DateFormat;
				}
				else if (column.IsNumeric)
				{
					excelColumn.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
				}

				var cell = _worksheet.Cells[1, i + 1];
				cell.Value = column.Name;
				cell.Style.Font.Bold = true;
			}
		}

		protected override void ExportRow(object[] rowValues)
		{
			_currentRowIndex++;

			for (var i = 0; i < _columns.Count; i++)
			{
				var stringValue = FormatValue(rowValues[i]);
				object value;

				decimal number;
				if (_columns[i].IsNumeric && Decimal.TryParse(stringValue, out number))
				{
					value = number;
				}
				else
				{
					value = stringValue;
				}

				_worksheet.Cells[_currentRowIndex, i + 1].Value = value;
			}
		}

		protected override void FinalizeExport()
		{
			for (var i = 1; i <= _columns.Count; i++)
			{
				_worksheet.Column(i).AutoFit();
			}

			_package.Save();
		}

		private static string FormatValue(object value)
		{
			return IsNull(value)
				? null
				: CellValueConverter.Instance.Convert(value, typeof(String), null, CultureInfo.CurrentUICulture).ToString();
		}
	}
}
