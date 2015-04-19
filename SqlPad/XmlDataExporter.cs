using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml;

namespace SqlPad
{
	public class XmlDataExporter : IDataExporter
	{
		public string FileNameFilter
		{
			get { return "XML files (*.xml)|*.xml|All files (*.*)|*"; }
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
				.Select(c => c.Header.ToString().Replace("__", "_"))
				.ToArray();

			var rows = (IEnumerable)dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(columnHeaders, rows, fileName, cancellationToken), cancellationToken);
		}

		private void ExportInternal(IReadOnlyList<string> columnHeaders, IEnumerable rows, string fileName, CancellationToken cancellationToken)
		{
			var columnCount = columnHeaders.Count;

			using (var writer = XmlWriter.Create(fileName, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
			{
				writer.WriteStartDocument();
				writer.WriteStartElement("data");

				foreach (object[] rowValues in rows)
				{
					cancellationToken.ThrowIfCancellationRequested();

					writer.WriteStartElement("row");

					for (var i = 0; i < columnCount; i++)
					{
						writer.WriteElementString(columnHeaders[i], FormatXmlValue(rowValues[i]));
					}

					writer.WriteEndElement();
				}

				writer.WriteEndElement();
				writer.WriteEndDocument();
			}
		}

		private static string FormatXmlValue(object value)
		{
			return value == DBNull.Value ? null : value.ToString();
		}
	}
}
