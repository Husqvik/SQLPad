using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public class HtmlDataExporter : IDataExporter
	{
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "&quot;";

		public string FileNameFilter => "HTML files (*.html)|*.html|All files (*.*)|*";

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
				.Select(h => h.Name.Replace(QuoteCharacter, EscapedQuote));

			var headerLine = BuildlTableRowTemplate(columnHeaders.Select(h => $"<th>{h}</th>"));
			var htmlTableRowTemplate = BuildlTableRowTemplate(Enumerable.Range(0, orderedColumns.Count).Select(i => $"<td>{{{i}}}</td>"));

			var rows = resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, headerLine, htmlTableRowTemplate, rows, w, cancellationToken));
		}

		private static string BuildlTableRowTemplate(IEnumerable<string> columnValues)
		{
			var htmlTableRowTemplateBuilder = new StringBuilder();
			htmlTableRowTemplateBuilder.Append("<tr>");
			htmlTableRowTemplateBuilder.Append(String.Concat(columnValues));
			htmlTableRowTemplateBuilder.Append("<tr>");
			return htmlTableRowTemplateBuilder.ToString();
		}

		private void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, string headerLine, string htmlTemplate, ICollection rows, TextWriter writer, CancellationToken cancellationToken)
		{
			writer.Write("<!DOCTYPE html><html><head><title></title></head><body><table border=\"1\" style=\"border-collapse:collapse\">");
			writer.Write(headerLine);

			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var values = orderedColumns.Select(h => (object)FormatHtmlValue(rowValues[h.ColumnIndex])).ToArray();
				writer.Write(htmlTemplate, values);
			}

			writer.Write("<table>");
		}

		private static string FormatHtmlValue(object value)
		{
			return DataExportHelper.IsNull(value) ? "NULL" : value.ToString().Replace(">", "&gt;").Replace("<", "&lt;");
		}
	}
}
