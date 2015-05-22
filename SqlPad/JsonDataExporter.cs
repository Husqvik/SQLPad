using System;
using System.Collections;
using System.Globalization;
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
		private const string MaskJsonValue = "\t\"{0}\": {{{1}}}";
		private const string QuoteCharacter = "\"";
		private const string EscapedQuote = "\"";
		private const string ApostropheCharacter = "'";
		private const string EscapedApostrophe = "\\'";

		public string FileNameFilter
		{
			get { return "JSON files (*.json)|*.json|All files (*.*)|*"; }
		}

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
				.Select((c, i) => String.Format(MaskJsonValue, c.Header.ToString().Replace("__", "_").Replace(QuoteCharacter, EscapedQuote), i));

			var jsonTemplateBuilder = new StringBuilder();
			jsonTemplateBuilder.AppendLine("{{");
			jsonTemplateBuilder.AppendLine(String.Join(String.Format(",{0}", Environment.NewLine), columnHeaders));
			jsonTemplateBuilder.Append("}}");

			var rows = dataGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(jsonTemplateBuilder.ToString(), rows, w, cancellationToken));
		}

		private void ExportInternal(string jsonTemplate, ICollection rows, TextWriter writer, CancellationToken cancellationToken)
		{
			writer.Write('[');

			var rowIndex = 1;
			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var values = rowValues.Select((t, i) => (object)FormatJsonValue(t)).ToArray();
				writer.Write(jsonTemplate, values);

				if (rowIndex < rows.Count)
				{
					writer.WriteLine(',');
				}

				rowIndex++;
			}

			writer.WriteLine(']');
		}

		private static string FormatJsonValue(object value)
		{
			if (DataExportHelper.IsNull(value))
			{
				return "null";
			}

			if (value is ValueType)
			{
				return value.ToString();
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), null, CultureInfo.CurrentUICulture).ToString();
			return String.Format("'{0}'", stringValue.Replace(ApostropheCharacter, EscapedApostrophe));
		}
	}
}
