using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SqlPad
{
	public interface IDataExporter
	{
		string FileNameFilter { get; }

		void ExportToFile(string fileName, DataGrid dataGrid);

		Task ExportToFileAsync(string fileName, DataGrid dataGrid, CancellationToken cancellationToken);
		
		void ExportToClipboard(DataGrid dataGrid);

		Task ExportToClipboardAsync(DataGrid dataGrid, CancellationToken cancellationToken);
	}

	internal static class DataExportHelper
	{
		public static Task RunExportActionAsync(string fileName, Action<TextWriter> exportAction)
		{
			var stringBuilder = new StringBuilder();

			return Task.Factory
				.StartNew(() => RunExportActionInternal(fileName, stringBuilder, exportAction))
				.ContinueWith(t => SetToClipboard(fileName, stringBuilder));
		}

		private static void SetToClipboard(string fileName, StringBuilder stringBuilder)
		{
			var exportToClipboard = String.IsNullOrEmpty(fileName);
			if (exportToClipboard)
			{
				Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(stringBuilder.ToString()));
			}
		}

		public static bool IsNull(object value)
		{
			var largeValue = value as ILargeValue;
			return value == DBNull.Value ||
			       (largeValue != null && largeValue.IsNull);
		}

		private static void RunExportActionInternal(string fileName, StringBuilder stringBuilder, Action<TextWriter> exportAction)
		{
			var exportToClipboard = String.IsNullOrEmpty(fileName);

			using (var writer = exportToClipboard ? (TextWriter)new StringWriter(stringBuilder) : File.CreateText(fileName))
			{
				exportAction(writer);
			}
		}
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

		public void ExportToClipboard(DataGrid dataGrid)
		{
			ExportToFile(null, dataGrid);
		}

		public void ExportToFile(string fileName, DataGrid dataGrid)
		{
			ExportToFileAsync(fileName, dataGrid, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(DataGrid dataGrid, CancellationToken cancellationToken)
		{
			return ExportToFileAsync(null, dataGrid, cancellationToken);
		}

		public Task ExportToFileAsync(string fileName, DataGrid dataGrid, CancellationToken cancellationToken)
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

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(headerLine, rows, converterParameters, w, cancellationToken));
		}

		private void ExportInternal(string headerLine, IEnumerable rows, IReadOnlyList<object> converterParameters, TextWriter writer, CancellationToken cancellationToken)
		{
			writer.WriteLine(headerLine);

			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var contentLine = String.Join(CsvSeparator, rowValues.Select((t, i) => FormatCsvValue(t, converterParameters[i])));
				writer.WriteLine(contentLine);
			}
		}

		private static string FormatCsvValue(object value, object converterParameter)
		{
			if (DataExportHelper.IsNull(value))
			{
				return null;
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), converterParameter, CultureInfo.CurrentUICulture).ToString();
			return String.Format(MaskWrapByQuote, stringValue.Replace(QuoteCharacter, DoubleQuotes));
		}
	}
}
