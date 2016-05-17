using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public interface IDataExporter
	{
		string Name { get; }

		string FileNameFilter { get; }

		bool HasAppendSupport { get; }

		Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null);

		Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null);
	}

	public class DataExporters
	{
		public static readonly CsvDataExporter Csv = new CsvDataExporter();
		public static readonly TsvDataExporter Tsv = new TsvDataExporter();
		public static readonly XmlDataExporter Xml = new XmlDataExporter();
		public static readonly JsonDataExporter Json = new JsonDataExporter();
		public static readonly HtmlDataExporter Html = new HtmlDataExporter();
		public static readonly ExcelDataExporter Excel = new ExcelDataExporter();
		public static readonly SqlInsertDataExporter SqlInsert = new SqlInsertDataExporter();
		public static readonly SqlUpdateDataExporter SqlUpdate = new SqlUpdateDataExporter();

		public static readonly IDataExporter[] All =
		{
			Csv,
			Tsv,
			Xml,
			Json,
			Html,
			Excel,
			SqlInsert,
			SqlUpdate
		};
	}
}
