using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace SqlPad
{
	public class XmlDataExporter : IDataExporter
	{
		private const string Underscore = "_";
		private static readonly Regex XmlElementNameFormatExpression = new Regex("[^\\w]", RegexOptions.Compiled);
		private static readonly XmlWriterSettings XmlWriterSettings =
			new XmlWriterSettings
			{
				Indent = true,
				Encoding = Encoding.UTF8
			};

		public string FileNameFilter
		{
			get { return "XML files (*.xml)|*.xml|All files (*.*)|*"; }
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
			var columnHeaders = dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.Select(c => FormatColumnHeaderAsXmlElementName(((ColumnHeader)c.Header).Name))
					.ToArray();

			var rows = (IEnumerable)dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(columnHeaders, rows, fileName, dataExportConverter, cancellationToken), cancellationToken);
		}

		private void ExportInternal(IReadOnlyList<string> columnHeaders, IEnumerable rows, string fileName, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
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
						xmlWriter.WriteElementString(columnHeaders[i], FormatXmlValue(rowValues[i], dataExportConverter));
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

		private static string FormatXmlValue(object value, IDataExportConverter dataExportConverter)
		{
			return DataExportHelper.IsNull(value) ? null : dataExportConverter.ToXml(value);
		}

		private static string FormatColumnHeaderAsXmlElementName(string header)
		{
			header = XmlElementNameFormatExpression.Replace(header, Underscore);
			if (Char.IsDigit(header[0]))
			{
				header = header.Insert(0, Underscore);
			}

			return header;
		}
	}
}
