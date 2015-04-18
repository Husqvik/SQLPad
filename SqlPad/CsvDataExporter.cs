using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace SqlPad
{
	public class CsvDataExporter
	{
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";
		private const string CsvSeparator = ";";
		public void Export(DataGrid dataGrid, TextWriter writer)
		{
			ExportAsync(dataGrid, writer, CancellationToken.None).Wait();
		}

		public Task ExportAsync(DataGrid dataGrid, TextWriter writer, CancellationToken cancellationToken)
		{
			var orderedColumns = dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.ToArray();

			var columnHeaders = orderedColumns
				.Select(c => String.Format(MaskWrapByQuote, c.Header.ToString().Replace("__", "_").Replace(QuoteCharacter, DoubleQuotes)));

			var headerLine = String.Join(CsvSeparator, columnHeaders);
			writer.WriteLine(headerLine);

			var converterParameters = orderedColumns
				.Select(c => ((Binding)((DataGridTextColumn)c).Binding).ConverterParameter)
				.ToArray();

			var rows = (IEnumerable)dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(rows, converterParameters, writer, cancellationToken), cancellationToken);
		}

		private void ExportInternal(IEnumerable rows, IReadOnlyList<object> converterParameters, TextWriter writer, CancellationToken cancellationToken)
		{
			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var contentLine = String.Join(CsvSeparator, rowValues.Select((t, i) => FormatCsvValue(t, converterParameters[i])));
				writer.WriteLine(contentLine);
			}
		}

		private static string FormatCsvValue(object value, object converterParameter)
		{
			if (value == DBNull.Value)
				return null;

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), converterParameter, CultureInfo.CurrentUICulture).ToString();
			return String.Format(MaskWrapByQuote, stringValue.Replace(QuoteCharacter, DoubleQuotes));
		}
	}
}