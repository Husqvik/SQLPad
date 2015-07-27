using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SqlPad.DataExport
{
	public class CsvDataExporter : IDataExporter
	{
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";
		private const string CsvSeparator = ";";

		protected virtual string Separator => CsvSeparator;

	    public virtual string FileNameFilter => "CSV files (*.csv)|*.csv|All files (*.*)|*";

	    public void ExportToClipboard(DataGrid dataGrid, IDataExportConverter dataExportConverter)
		{
			ExportToFile(null, dataGrid, dataExportConverter);
		}

		public void ExportToFile(string fileName, DataGrid dataGrid, IDataExportConverter dataExportConverter)
		{
			ExportToFileAsync(fileName, dataGrid, dataExportConverter, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(DataGrid dataGrid, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			return ExportToFileAsync(null, dataGrid, dataExportConverter, cancellationToken);
		}

		public Task ExportToFileAsync(string fileName, DataGrid dataGrid, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(dataGrid);
			var columnHeaders = orderedColumns
				.Select(h => String.Format(MaskWrapByQuote, h.Name.Replace(QuoteCharacter, DoubleQuotes)));

			var headerLine = String.Join(Separator, columnHeaders);

			var rows = (IEnumerable)dataGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, headerLine, rows, w, cancellationToken));
		}

		private void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, string headerLine, IEnumerable rows, TextWriter writer, CancellationToken cancellationToken)
		{
			writer.WriteLine(headerLine);

			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var contentLine = String.Join(Separator, orderedColumns.Select(c => FormatCsvValue(rowValues[c.ColumnIndex])));
				writer.WriteLine(contentLine);
			}
		}

		private static string FormatCsvValue(object value)
		{
			if (DataExportHelper.IsNull(value))
			{
				return null;
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), null, CultureInfo.CurrentUICulture).ToString();
			return String.Format(MaskWrapByQuote, stringValue.Replace(QuoteCharacter, DoubleQuotes));
		}
	}
}
