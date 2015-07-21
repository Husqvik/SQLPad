using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SqlPad
{
	public class JsonDataExporter : IDataExporter
	{
		private const string MaskJsonValue = "    \"{0}\": {{{1}}}";
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "\\\"";

		public string FileNameFilter => "JSON files (*.json)|*.json|All files (*.*)|*";

	    public void ExportToClipboard(DataGrid dataGrid, IDataExportConverter dataExportConverter)
		{
			ExportToFile(null, dataGrid, dataExportConverter);
		}

		public void ExportToFile(string fileName, DataGrid dataGrid, IDataExportConverter dataExportConverter)
		{
			ExportToFileAsync(fileName, dataGrid, dataExportConverter, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(DataGrid dataGrid, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			return ExportToFileAsync(null, dataGrid, dataExportConverter, cancellationToken);
		}

		public Task ExportToFileAsync(string fileName, DataGrid dataGrid, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var orderedColumns = dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.ToArray();

			var columnHeaders = orderedColumns
				.Select((c, i) => String.Format(MaskJsonValue, ((ColumnHeader)c.Header).Name.Replace("{", "{{").Replace("}", "}}").Replace(QuoteCharacter, EscapedQuote), i));

			var jsonTemplateBuilder = new StringBuilder();
			jsonTemplateBuilder.AppendLine("  {{");
			jsonTemplateBuilder.AppendLine(String.Join($",{Environment.NewLine}", columnHeaders));
			jsonTemplateBuilder.Append("  }}");

			var rows = dataGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(jsonTemplateBuilder.ToString(), rows, w, dataExportConverter, cancellationToken));
		}

		private void ExportInternal(string jsonTemplate, ICollection rows, TextWriter writer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			writer.WriteLine('[');

			var rowIndex = 1;
			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var values = rowValues.Select(t => (object)FormatJsonValue(t, dataExportConverter)).ToArray();
				writer.Write(jsonTemplate, values);

				if (rowIndex < rows.Count)
				{
					writer.WriteLine(',');
				}

				rowIndex++;
			}

			writer.WriteLine();
			writer.Write(']');
		}

		private static string FormatJsonValue(object value, IDataExportConverter dataExportConverter)
		{
			return DataExportHelper.IsNull(value) ? "null" : dataExportConverter.ToJson(value);
		}
	}
}
