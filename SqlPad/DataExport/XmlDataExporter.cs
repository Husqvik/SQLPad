using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace SqlPad.DataExport
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

		public string Name { get; } = "XML";

		public string FileNameFilter { get; } = "XML files (*.xml)|*.xml|All files (*.*)|*";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = (ICollection)resultViewer.ResultGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(orderedColumns, rows, fileName, dataExportConverter, cancellationToken, reportProgress), cancellationToken);
		}

		private static void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, string fileName, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress)
		{
			var stringBuilder = new StringBuilder();

			var exportToClipboard = String.IsNullOrEmpty(fileName);

			var columnHeaders = orderedColumns
				.Select(h => FormatColumnHeaderAsXmlElementName(h.Name))
				.ToArray();

			using (var xmlWriter = exportToClipboard ? XmlWriter.Create(stringBuilder, XmlWriterSettings) : XmlWriter.Create(fileName, XmlWriterSettings))
			{
				xmlWriter.WriteStartDocument();
				xmlWriter.WriteStartElement("data");

				DataExportHelper.ExportRows(
					rows,
					(rowValues, isLastRow) => ExportRow(xmlWriter, rowValues, orderedColumns, columnHeaders, dataExportConverter),
					reportProgress,
					cancellationToken);

				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndDocument();
			}

			if (exportToClipboard)
			{
				Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(stringBuilder.ToString()));
			}
		}

		private static void ExportRow(XmlWriter xmlWriter, IReadOnlyList<object> rowValues, IReadOnlyList<ColumnHeader> orderedColumns, IReadOnlyList<string> columnHeaders, IDataExportConverter dataExportConverter)
		{
			xmlWriter.WriteStartElement("row");

			for (var i = 0; i < orderedColumns.Count; i++)
			{
				xmlWriter.WriteElementString(columnHeaders[i], FormatXmlValue(rowValues[orderedColumns[i].ColumnIndex], dataExportConverter));
			}

			xmlWriter.WriteEndElement();
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
