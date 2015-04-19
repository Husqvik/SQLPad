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
	public interface IDataExporter
	{
		string FileNameFilter { get; }

		void Export(string fileName, DataGrid dataGrid);

		Task ExportAsync(string fileName, DataGrid dataGrid, CancellationToken cancellationToken);
	}

	public class CsvDataExporter : IDataExporter
	{
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";
		private const string CsvSeparator = ";";

		public string FileNameFilter
		{
			get { return "CSV files (*.csv)|*.csv|All files (*.*)|*"; }
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
				.Select(c => String.Format(MaskWrapByQuote, c.Header.ToString().Replace("__", "_").Replace(QuoteCharacter, DoubleQuotes)));

			var headerLine = String.Join(CsvSeparator, columnHeaders);

			var converterParameters = orderedColumns
				.Select(c => ((Binding)((DataGridTextColumn)c).Binding).ConverterParameter)
				.ToArray();

			var rows = (IEnumerable)dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(headerLine, rows, converterParameters, fileName, cancellationToken), cancellationToken);
		}

		private void ExportInternal(string headerLine, IEnumerable rows, IReadOnlyList<object> converterParameters, string fileName, CancellationToken cancellationToken)
		{
			using (var writer = File.CreateText(fileName))
			{
				writer.WriteLine(headerLine);

				foreach (object[] rowValues in rows)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var contentLine = String.Join(CsvSeparator, rowValues.Select((t, i) => FormatCsvValue(t, converterParameters[i])));
					writer.WriteLine(contentLine);
				}
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
