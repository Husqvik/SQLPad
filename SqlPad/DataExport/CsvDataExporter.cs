using System;
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
		private const string CsvSeparator = ";";

		public virtual string Name { get; } = "Comma separated value";

		protected virtual string Separator { get; } = CsvSeparator;

		public virtual string FileNameFilter { get; } = "CSV files (*.csv)|*.csv|All files (*.*)|*";

		public virtual string FileExtension { get; } = "csv";

		public bool HasAppendSupport { get; } = false;

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new SeparatedValueDataExportContext(options, columns, Separator);
			await exportContext.InitializeAsync(cancellationToken);
			return exportContext;
		}
	}

	internal class SeparatedValueDataExportContext : DataExportContextBase
	{
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";

		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly string _separator;

		private TextWriter _writer;

		public SeparatedValueDataExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, string separator)
			: base(exportOptions)
		{
			_columns = columns;
			_separator = separator;
		}

		protected override void InitializeExport()
		{
			_writer = DataExportHelper.InitializeWriter(ExportOptions.FileName, ClipboardData);

			WriteHeader();
		}

		protected override Task FinalizeExport()
		{
			return _writer.FlushAsync();
		}

		protected override void Dispose(bool disposing)
		{
			_writer?.Dispose();
			base.Dispose(disposing);
		}

		private void WriteHeader()
		{
			var columnHeaders = _columns.Select(h => EscapeIfNeeded(h.Name));
			var headerLine = String.Join(_separator, columnHeaders);
			_writer.WriteLine(headerLine);
		}

		protected override void ExportRow(object[] rowValues)
		{
			_writer.WriteLine(String.Join(_separator, _columns.Select(c => FormatCsvValue(rowValues[c.ColumnIndex]))));
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

		private static string EscapeIfNeeded(string value)
		{
			return String.Format(MaskWrapByQuote, value.Replace(QuoteCharacter, DoubleQuotes));
		}
	}
}
