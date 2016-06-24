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

		string FileExtension { get; }

		bool HasAppendSupport { get; }

		Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken);
	}

	public interface IDataExportContext : IDisposable
	{
		long CurrentRowIndex { get; }

		Task AppendRowsAsync(IEnumerable<object[]> rows);

		Task FinalizeAsync();

		void SetProgress(long totalRowCount, IProgress<int> progress);
	}

	public class ExportOptions
	{
		public static readonly ExportOptions ToClipboard = new ExportOptions(null, null);

		public string FileName { get; }

		public string ExportName { get; }

		public bool IntoClipboard => this == ToClipboard;

		private ExportOptions(string fileName, string exportName)
		{
			FileName = fileName;
			ExportName = exportName;
		}

		public static ExportOptions ToFile(string fileName, string exportName)
		{
			return new ExportOptions(fileName, exportName);
		}
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
