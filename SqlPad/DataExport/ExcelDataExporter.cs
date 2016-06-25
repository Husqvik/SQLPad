using System;
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

		public string FileExtension { get; } = "xlsx";

		public bool HasAppendSupport { get; } = true;

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new ExcelDataExportContext(options, columns);
			await exportContext.InitializeAsync(cancellationToken);
			return exportContext;
		}
	}

	internal class ExcelDataExportContext : DataExportContextBase
	{
		private readonly IReadOnlyList<ColumnHeader> _columns;

		private ExcelPackage _package;
		private ExcelWorksheet _worksheet;
		private int _currentRowIndex;

		public ExcelDataExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns)
			: base(exportOptions)
		{
			_columns = columns;
		}

		protected override void InitializeExport()
		{
			_currentRowIndex = 1;

			if (ExportOptions.IntoClipboard)
			{
				throw new NotSupportedException("Excel export does not support clipboard. ");
			}

			_package = new ExcelPackage(new FileInfo(ExportOptions.FileName));

			var worksheetName = ExportOptions.ExportName;

			if (_package.Workbook.Worksheets[worksheetName] != null)
			{
				_package.Workbook.Worksheets.Delete(worksheetName);
			}

			_worksheet = _package.Workbook.Worksheets.Add(worksheetName);

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

		protected override Task FinalizeExport()
		{
			return Task.Run((Action)FinalizeInternal);
		}

		private void FinalizeInternal()
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
