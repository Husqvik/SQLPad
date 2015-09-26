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

		public bool HasAppendSupport { get; } = false;

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
			var columnHeaders = orderedColumns
				.Select(h => String.Format(MaskWrapByQuote, h.Name.Replace(QuoteCharacter, DoubleQuotes)));

			var headerLine = String.Join(Separator, columnHeaders);

			var rows = (ICollection)resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, headerLine, rows, w, cancellationToken, reportProgress));
		}

		private void ExportInternal(IEnumerable<ColumnHeader> orderedColumns, string headerLine, ICollection rows, TextWriter writer, CancellationToken cancellationToken, IProgress<int> reportProgress)
		{
			writer.WriteLine(headerLine);

			DataExportHelper.ExportRows(
				rows,
				(rowValues, isLastRow) => writer.WriteLine(String.Join(Separator, orderedColumns.Select(c => FormatCsvValue(rowValues[c.ColumnIndex])))),
				reportProgress,
				cancellationToken);
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
