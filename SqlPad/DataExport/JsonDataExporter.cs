using System;
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

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new JsonDataExportContext(options, columns, dataExportConverter, cancellationToken);
			await exportContext.InitializeAsync();
			return exportContext;
		}
	}

	internal class JsonDataExportContext : DataExportContextBase
	{
		private const string MaskJsonValue = "    \"{0}\": {{{1}}}";
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "\\\"";

		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly IDataExportConverter _dataExportConverter;
		private readonly string _jsonRowTemplate;

		private TextWriter _writer;

		public JsonDataExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
			: base(exportOptions, cancellationToken)
		{
			_columns = columns;
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
			_writer = DataExportHelper.InitializeWriter(ExportOptions.FileName, ClipboardData);
			_writer.WriteLine('[');
		}

		protected override async Task FinalizeExport()
		{
			await _writer.WriteLineAsync();
			await _writer.WriteAsync(']');
			await _writer.FlushAsync();
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
			_writer?.Dispose();
			base.Dispose(disposing);
		}

		private string FormatJsonValue(object value)
		{
			return IsNull(value) ? "null" : _dataExportConverter.ToJson(value);
		}
	}
}
