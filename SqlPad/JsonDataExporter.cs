using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

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

		public void Export(string fileName, DataGrid dataGrid)
		{
			ExportAsync(fileName, dataGrid, CancellationToken.None).Wait();
		}

		public Task ExportAsync(string fileName, DataGrid dataGrid, CancellationToken cancellationToken)
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

			var converterParameters = orderedColumns
				.Select(c => ((Binding)((DataGridTextColumn)c).Binding).ConverterParameter)
				.ToArray();

			var rows = dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(jsonTemplateBuilder.ToString(), rows, converterParameters, fileName, cancellationToken), cancellationToken);
		}

		private void ExportInternal(string jsonTemplate, ICollection rows, IReadOnlyList<object> converterParameters, string fileName, CancellationToken cancellationToken)
		{
			using (var writer = File.CreateText(fileName))
			{
				writer.Write('[');

				var rowIndex = 1;
				foreach (object[] rowValues in rows)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var values = rowValues.Select((t, i) => (object)FormatJsonValue(t, converterParameters[i])).ToArray();
					writer.Write(jsonTemplate, values);

					if (rowIndex < rows.Count)
					{
						writer.WriteLine(',');
					}

					rowIndex++;
				}

				writer.WriteLine(']');
			}
		}

		private static string FormatJsonValue(object value, object converterParameter)
		{
			if (value == DBNull.Value)
				return "null";

			if (value is ValueType)
			{
				return value.ToString();
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), converterParameter, CultureInfo.CurrentUICulture).ToString();
			return String.Format("'{0}'", stringValue.Replace(ApostropheCharacter, EscapedApostrophe));
		}
	}
}
