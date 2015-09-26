using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public interface IDataExporter
	{
		string FileNameFilter { get; }

		bool HasAppendSupport { get; }

		void ExportToFile(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter);

		Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken);

		void ExportToClipboard(ResultViewer resultViewer, IDataExportConverter dataExportConverter);

		Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken);
	}
}
