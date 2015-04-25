using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace SqlPad
{
	public class XmlDataExporter : IDataExporter
	{
		private static readonly XmlWriterSettings XmlWriterSettings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };

		public string FileNameFilter
		{
			get { return "XML files (*.xml)|*.xml|All files (*.*)|*"; }
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
				.Select(c => c.Header.ToString().Replace("__", "_"))
				.ToArray();

			var rows = (IEnumerable)dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(columnHeaders, rows, fileName, cancellationToken), cancellationToken);
		}

		private void ExportInternal(IReadOnlyList<string> columnHeaders, IEnumerable rows, string fileName, CancellationToken cancellationToken)
		{
			var columnCount = columnHeaders.Count;

			var stringBuilder = new StringBuilder();

			var exportToClipboard = String.IsNullOrEmpty(fileName);

			using (var xmlWriter = exportToClipboard ? XmlWriter.Create(stringBuilder, XmlWriterSettings) : XmlWriter.Create(fileName, XmlWriterSettings))
			{
				xmlWriter.WriteStartDocument();
				xmlWriter.WriteStartElement("data");

				foreach (object[] rowValues in rows)
				{
					cancellationToken.ThrowIfCancellationRequested();

					xmlWriter.WriteStartElement("row");

					for (var i = 0; i < columnCount; i++)
					{
						xmlWriter.WriteElementString(columnHeaders[i], FormatXmlValue(rowValues[i]));
					}

					xmlWriter.WriteEndElement();
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndDocument();
			}

			if (exportToClipboard)
			{
				Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(stringBuilder.ToString()));
			}
		}

		private static string FormatXmlValue(object value)
		{
			return DataExportHelper.IsNull(value) ? null : value.ToString();
		}
	}
}
