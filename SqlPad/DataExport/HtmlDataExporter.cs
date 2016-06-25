using System;
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

		public string FileExtension { get; } = "html";

		public bool HasAppendSupport { get; } = false;

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new HtmlDataExportContext(options, columns);
			await exportContext.InitializeAsync(cancellationToken);
			return exportContext;
		}
	}

	internal class HtmlDataExportContext : DataExportContextBase
	{
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "&quot;";

		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly string _htmlTableRowTemplate;

		private TextWriter _writer;

		public HtmlDataExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns)
			: base(exportOptions)
		{
			_columns = columns;
			_htmlTableRowTemplate = BuildlTableRowTemplate(Enumerable.Range(0, _columns.Count).Select(i => $"<td>{{{i}}}</td>"));
		}

		protected override void InitializeExport()
		{
			_writer = DataExportHelper.InitializeWriter(ExportOptions.FileName, ClipboardData);

			WriteHeader();
		}

		private void WriteHeader()
		{
			_writer.Write("<!DOCTYPE html><html><head><title></title></head><body><table border=\"1\" style=\"border-collapse:collapse\">");

			var columnHeaders = _columns.Select(h => h.Name.Replace(QuoteCharacter, EscapedQuote));
			var headerLine = BuildlTableRowTemplate(columnHeaders.Select(h => $"<th>{h}</th>"));
			_writer.Write(headerLine);
		}

		protected override void Dispose(bool disposing)
		{
			_writer?.Dispose();
			base.Dispose(disposing);
		}

		protected override Task FinalizeExport()
		{
			return _writer.WriteAsync("<table>");
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
