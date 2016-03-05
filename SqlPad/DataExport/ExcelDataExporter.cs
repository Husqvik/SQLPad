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

		public void ExportToClipboard(ResultViewer resultViewer, IDataExportConverter dataExportConverter)
		{
			ExportToFile(null, resultViewer, dataExportConverter);
		}

		public void ExportToFile(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter)
		{
			ExportToFileAsync(fileName, resultViewer, dataExportConverter, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = (ICollection)resultViewer.ResultGrid.Items;
			var worksheetName = resultViewer.Title;
			return Task.Factory.StartNew(() => ExportInternal(fileName, worksheetName, orderedColumns, rows, cancellationToken, reportProgress), cancellationToken);
		}

		private static void ExportInternal(string fileName, string worksheetName, IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, CancellationToken cancellationToken, IProgress<int> reportProgress)
		{
			var package = new ExcelPackage(new FileInfo(fileName));
			if (package.Workbook.Worksheets[worksheetName] != null)
			{
				package.Workbook.Worksheets.Delete(worksheetName);
			}

			var worksheet = package.Workbook.Worksheets.Add(worksheetName);
			var rowCount = rows.Count;
			var totalCells = orderedColumns.Count * rowCount;

			for (var i = 0; i < orderedColumns.Count; i++)
			{
				var column = orderedColumns[i];
				var cell = worksheet.Cells[1, i + 1];
				cell.Value = column.Name;
				cell.Style.Font.Bold = true;

				var isNumeric = column.IsNumeric;
				var rowIndex = 2;
				foreach (object[] rowValues in rows)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var progress = (int)Math.Round((i * rowCount + rowIndex - 2) * 100f / totalCells);
					reportProgress?.Report(progress);

					var stringValue = FormatValue(rowValues[i]);
					object value;

					decimal number;
					if (isNumeric && Decimal.TryParse(stringValue, out number))
					{
						value = number;
					}
					else
					{
						value = stringValue;
					}

					worksheet.Cells[rowIndex, i + 1].Value = value;

					rowIndex++;
				}

				var excelColumn = worksheet.Column(i + 1);
				excelColumn.AutoFit();

				var columnCells = worksheet.Cells[2, i + 1, rowIndex, i + 1];
				if (column.DataType == typeof(DateTime))
				{
					columnCells.Style.Numberformat.Format = ConfigurationProvider.Configuration.ResultGrid.DateFormat;
				}
				else if (isNumeric)
				{
					excelColumn.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
				}
			}

			package.Save();

			reportProgress?.Report(100);
		}

		private static string FormatValue(object value)
		{
			return DataExportHelper.IsNull(value)
				? null
				: CellValueConverter.Instance.Convert(value, typeof (String), null, CultureInfo.CurrentUICulture).ToString();
		}
	}
}
