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
		public string Name { get; } = "HTML";

		public string FileNameFilter { get; } = "HTML files (*.html)|*.html|All files (*.*)|*";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, rows, w, reportProgress, cancellationToken));
		}

		private static void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, TextWriter writer, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			var exportContext = new HtmlDataExportContext(writer, orderedColumns, rows.Count, reportProgress, cancellationToken);
			DataExportHelper.ExportRowsUsingContext(rows, exportContext);
		}
	}

	internal class HtmlDataExportContext : DataExportContextBase
	{
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "&quot;";

		private readonly TextWriter _writer;
		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly string _htmlTableRowTemplate;

		public HtmlDataExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_writer = writer;
			_columns = columns;

			_htmlTableRowTemplate = BuildlTableRowTemplate(Enumerable.Range(0, _columns.Count).Select(i => $"<td>{{{i}}}</td>"));
		}

		protected override void InitializeExport()
		{
			_writer.Write("<!DOCTYPE html><html><head><title></title></head><body><table border=\"1\" style=\"border-collapse:collapse\">");

			var columnHeaders = _columns.Select(h => h.Name.Replace(QuoteCharacter, EscapedQuote));
			var headerLine = BuildlTableRowTemplate(columnHeaders.Select(h => $"<th>{h}</th>"));
			_writer.Write(headerLine);
		}

		protected override void FinalizeExport()
		{
			_writer.Write("<table>");
		}

		protected override void ExportRow(object[] rowValues)
		{
			_writer.Write(_htmlTableRowTemplate, _columns.Select(h => (object)FormatHtmlValue(rowValues[h.ColumnIndex])).ToArray());
		}

		private static string BuildlTableRowTemplate(IEnumerable<string> columnValues)
		{
			var htmlTableRowTemplateBuilder = new StringBuilder();
			htmlTableRowTemplateBuilder.Append("<tr>");
			htmlTableRowTemplateBuilder.Append(String.Concat(columnValues));
			htmlTableRowTemplateBuilder.Append("<tr>");
			return htmlTableRowTemplateBuilder.ToString();
		}

		private static string FormatHtmlValue(object value)
		{
			return IsNull(value) ? "NULL" : value.ToString().Replace(">", "&gt;").Replace("<", "&lt;");
		}
	}
}
