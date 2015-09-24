using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

		public void ExportToClipboard(ResultViewer resultViewer, IDataExportConverter dataExportConverter)
		{
			ExportToFile(null, resultViewer, dataExportConverter);
		}

		public void ExportToFile(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter)
		{
			ExportToFileAsync(fileName, resultViewer, dataExportConverter, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken);
		}

		public Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var columnHeaders = orderedColumns
				.Select(h => String.Format(MaskWrapByQuote, h.Name.Replace(QuoteCharacter, DoubleQuotes)));

			var headerLine = String.Join(Separator, columnHeaders);

			var rows = (IEnumerable)resultViewer.ResultGrid.Items;

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
