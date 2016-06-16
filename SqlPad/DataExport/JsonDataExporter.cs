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
	public class JsonDataExporter : IDataExporter
	{
		public string Name { get; } = "JSON";

		public string FileNameFilter { get; } = "JSON files (*.json)|*.json|All files (*.*)|*";

		public string FileExtension { get; } = "json";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public async Task<IDataExportContext> StartExportAsync(string fileName, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new JsonDataExportContext(File.CreateText(fileName), columns, dataExportConverter, null, null, cancellationToken);
			await exportContext.InitializeAsync();
			return exportContext;
		}

		public Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, rows, w, dataExportConverter, reportProgress, cancellationToken));
		}

		private static Task ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, TextWriter writer, IDataExportConverter dataExportConverter, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			var exportContext = new JsonDataExportContext(writer, orderedColumns, dataExportConverter, rows.Count, reportProgress, cancellationToken);
			return DataExportHelper.ExportRowsUsingContext(rows, exportContext);
		}
	}

	internal class JsonDataExportContext : DataExportContextBase
	{
		private const string MaskJsonValue = "    \"{0}\": {{{1}}}";
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "\\\"";

		private readonly TextWriter _writer;
		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly IDataExportConverter _dataExportConverter;
		private readonly string _jsonRowTemplate;

		public JsonDataExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_columns = columns;
			_writer = writer;
			_dataExportConverter = dataExportConverter;

			var columnHeaders = _columns
				.Select((h, i) => String.Format(MaskJsonValue, h.Name.Replace("{", "{{").Replace("}", "}}").Replace(QuoteCharacter, EscapedQuote), i));

			var jsonTemplateBuilder = new StringBuilder();
			jsonTemplateBuilder.AppendLine("  {{");
			jsonTemplateBuilder.AppendLine(String.Join($",{Environment.NewLine}", columnHeaders));
			jsonTemplateBuilder.Append("  }}");
			_jsonRowTemplate = jsonTemplateBuilder.ToString();
		}

		protected override void InitializeExport()
		{
			_writer.WriteLine('[');
		}

		protected override async Task FinalizeExport()
		{
			await _writer.WriteLineAsync();
			await _writer.WriteAsync(']');
		}

		protected override void ExportRow(object[] rowValues)
		{
			if (CurrentRowIndex > 0)
			{
				_writer.WriteLine(',');
			}

			var values = _columns.Select(h => (object)FormatJsonValue(rowValues[h.ColumnIndex])).ToArray();
			_writer.Write(_jsonRowTemplate, values);
		}

		protected override void Dispose(bool disposing)
		{
			_writer.Dispose();
		}

		private string FormatJsonValue(object value)
		{
			return IsNull(value) ? "null" : _dataExportConverter.ToJson(value);
		}
	}
}
