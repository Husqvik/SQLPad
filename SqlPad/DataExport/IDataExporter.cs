using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public interface IDataExporter
	{
		string Name { get; }

		string FileNameFilter { get; }

		bool HasAppendSupport { get; }

		Task<IDataExportContext> StartExportAsync(string fileName, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken);

		Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null);

		Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null);
	}

	public interface IDataExportContext : IDisposable
	{
		long CurrentRowIndex { get; }

		void AppendRows(IEnumerable<object[]> rows);

		void Complete();
	}

	public class DataExporters
	{
		public static readonly IDataExporter Csv = new CsvDataExporter();
		public static readonly IDataExporter Tsv = new TsvDataExporter();
		public static readonly IDataExporter Xml = new XmlDataExporter();
		public static readonly IDataExporter Json = new JsonDataExporter();
		public static readonly IDataExporter Html = new HtmlDataExporter();
		public static readonly IDataExporter Excel = new ExcelDataExporter();
		public static readonly IDataExporter SqlInsert = new SqlInsertDataExporter();
		public static readonly IDataExporter SqlUpdate = new SqlUpdateDataExporter();

		public static readonly IDataExporter[] All =
		{
			Csv,
			Tsv,
			Excel,
			Html,
			Json,
			SqlInsert,
			SqlUpdate,
			Xml
		};
	}
}
