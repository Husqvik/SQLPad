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
		private const string CsvSeparator = ";";

		public virtual string Name { get; } = "Comma separated value";

		protected virtual string Separator { get; } = CsvSeparator;

		public virtual string FileNameFilter { get; } = "CSV files (*.csv)|*.csv|All files (*.*)|*";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public async Task<IDataExportContext> StartExportAsync(string fileName, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new CsvDataExportContext(File.CreateText(fileName), columns, Separator, null, null, cancellationToken);
			await exportContext.InitializeAsync();
			return exportContext;
		}

		public Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);

			var rows = (ICollection)resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, rows, w, reportProgress, cancellationToken));
		}

		private Task ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, TextWriter writer, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			var exportContext = new CsvDataExportContext(writer, orderedColumns, Separator, rows.Count, reportProgress, cancellationToken);
			return DataExportHelper.ExportRowsUsingContext(rows, exportContext);
		}
	}

	internal class CsvDataExportContext : DataExportContextBase
	{
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";

		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly TextWriter _writer;
		private readonly string _separator;

		public CsvDataExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, string separator, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_writer = writer;
			_columns = columns;
			_separator = separator;
		}

		protected override void InitializeExport()
		{
			WriteHeader();
		}

		protected override void Dispose(bool disposing)
		{
			_writer.Dispose();
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
