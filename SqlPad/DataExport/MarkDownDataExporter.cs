using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public class MarkdownDataExporter : IDataExporter
	{
		public string Name { get; } = "Markdown";

		public string FileNameFilter { get; } = "Markdown files (*.md)|*.md|All files (*.*)|*";

		public string FileExtension { get; } = "md";

		public bool HasAppendSupport { get; } = false;

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new MarkdownDataExportContext(options, columns);
			await exportContext.InitializeAsync(cancellationToken);
			return exportContext;
		}
	}

	internal class MarkdownDataExportContext : DataExportContextBase
	{
		private const string MarkdownTableColumnSeparator = "|";

		private readonly IReadOnlyList<ColumnHeader> _columns;

		private TextWriter _writer;

		public MarkdownDataExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns)
			: base(exportOptions)
		{
			_columns = columns;
		}

		protected override void InitializeExport()
		{
			_writer = DataExportHelper.InitializeWriter(ExportOptions.FileName, ClipboardData);

			WriteHeader();
		}

		protected override Task FinalizeExport() => _writer.FlushAsync();

		protected override void Dispose(bool disposing)
		{
			_writer?.Dispose();
			base.Dispose(disposing);
		}

		private void WriteHeader()
		{
			var columnHeaders = _columns.Select(h => EscapeIfNeeded(h.Name));
			var headerLine = String.Join(MarkdownTableColumnSeparator, columnHeaders);
			_writer.WriteLine(headerLine);

			var headerSeparatorLine = String.Join(MarkdownTableColumnSeparator, _columns.Select(c => $"---{(c.IsNumeric ? ":" : null)}"));
			_writer.WriteLine(headerSeparatorLine);
		}

		protected override void ExportRow(object[] rowValues)
		{
			_writer.WriteLine(String.Join(MarkdownTableColumnSeparator, _columns.Select(c => FormatCsvValue(rowValues[c.ColumnIndex]))));
		}

		private static string FormatCsvValue(object value)
		{
			if (IsNull(value))
			{
				return null;
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), null, CultureInfo.CurrentUICulture).ToString();
			return EscapeIfNeeded(stringValue);
		}

		private static string EscapeIfNeeded(string value) => value.Replace("|", "&#124;");
	}
}