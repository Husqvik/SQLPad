using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace SqlPad.DataExport
{
	public class XmlDataExporter : IDataExporter
	{
		public string Name { get; } = "XML";

		public string FileNameFilter { get; } = "XML files (*.xml)|*.xml|All files (*.*)|*";

		public string FileExtension { get; } = "xml";

		public bool HasAppendSupport { get; } = false;

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = new XmlDataExportContext(options, columns, dataExportConverter, cancellationToken);
			await exportContext.InitializeAsync();
			return exportContext;
		}
	}

	internal class XmlDataExportContext : DataExportContextBase
	{
		private const string Underscore = "_";

		private static readonly Regex XmlElementNameFormatExpression = new Regex("[^\\w]", RegexOptions.Compiled);

		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly string[] _columnHeaders;
		private readonly IDataExportConverter _dataExportConverter;
		private static readonly XmlWriterSettings XmlWriterSettings =
			new XmlWriterSettings
			{
				Async = true,
				Indent = true,
				Encoding = Encoding.UTF8
			};

		private XmlWriter _xmlWriter;

		public XmlDataExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
			: base(exportOptions, cancellationToken)
		{
			_columns = columns;
			_dataExportConverter = dataExportConverter;
			_columnHeaders = columns
				.Select(h => FormatColumnHeaderAsXmlElementName(h.Name))
				.ToArray();
		}

		protected override void InitializeExport()
		{
			_xmlWriter = ExportOptions.IntoClipboard ? XmlWriter.Create(ClipboardData, XmlWriterSettings) : XmlWriter.Create(ExportOptions.FileName, XmlWriterSettings);
			_xmlWriter.WriteStartDocument();
			_xmlWriter.WriteStartElement("data");
		}

		protected override void Dispose(bool disposing)
		{
			_xmlWriter?.Dispose();
			base.Dispose(disposing);
		}

		protected override async Task FinalizeExport()
		{
			await _xmlWriter.WriteEndElementAsync();
			await _xmlWriter.WriteEndDocumentAsync();
			await _xmlWriter.FlushAsync();
		}

		protected override void ExportRow(object[] rowValues)
		{
			_xmlWriter.WriteStartElement("row");

			for (var i = 0; i < _columns.Count; i++)
			{
				var value = rowValues[_columns[i].ColumnIndex];
				_xmlWriter.WriteElementString(_columnHeaders[i], FormatXmlValue(value));
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
