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
		private static readonly XmlWriterSettings XmlWriterSettings =
			new XmlWriterSettings
			{
				Indent = true,
				Encoding = Encoding.UTF8
			};

		public string Name { get; } = "XML";

		public string FileNameFilter { get; } = "XML files (*.xml)|*.xml|All files (*.*)|*";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = (ICollection)resultViewer.ResultGrid.Items;

			return Task.Run(() => ExportInternal(orderedColumns, rows, fileName, dataExportConverter, reportProgress, cancellationToken), cancellationToken);
		}

		private static void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, string fileName, IDataExportConverter dataExportConverter, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			var stringBuilder = new StringBuilder();

			var exportToClipboard = String.IsNullOrEmpty(fileName);

			using (var xmlWriter = exportToClipboard ? XmlWriter.Create(stringBuilder, XmlWriterSettings) : XmlWriter.Create(fileName, XmlWriterSettings))
			{
				var exportContext = new XmlDataExportContext(xmlWriter, orderedColumns, dataExportConverter, rows.Count, reportProgress, cancellationToken);
				DataExportHelper.ExportRowsUsingContext(rows, exportContext);
			}

			if (exportToClipboard)
			{
				Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(stringBuilder.ToString()));
			}
		}
	}

	internal class XmlDataExportContext : DataExportContextBase
	{
		private const string Underscore = "_";

		private static readonly Regex XmlElementNameFormatExpression = new Regex("[^\\w]", RegexOptions.Compiled);

		private readonly XmlWriter _xmlWriter;
		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly string[] _columnHeaders;
		private readonly IDataExportConverter _dataExportConverter;

		public XmlDataExportContext(XmlWriter xmlWriter, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_xmlWriter = xmlWriter;
			_columns = columns;
			_dataExportConverter = dataExportConverter;

			_columnHeaders = columns
				.Select(h => FormatColumnHeaderAsXmlElementName(h.Name))
				.ToArray();
		}

		protected override void InitializeExport()
		{
			_xmlWriter.WriteStartDocument();
			_xmlWriter.WriteStartElement("data");
		}

		protected override void FinalizeExport()
		{
			_xmlWriter.WriteEndElement();
			_xmlWriter.WriteEndDocument();
		}

		protected override void ExportRow(object[] rowValues)
		{
			_xmlWriter.WriteStartElement("row");

			for (var i = 0; i < _columns.Count; i++)
			{
				_xmlWriter.WriteElementString(_columnHeaders[i], FormatXmlValue(rowValues[_columns[i].ColumnIndex]));
			}

			_xmlWriter.WriteEndElement();
		}

		private string FormatXmlValue(object value)
		{
			return IsNull(value) ? null : _dataExportConverter.ToXml(value);
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
