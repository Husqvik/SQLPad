using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SqlPad
{
	public interface IDataExporter
	{
		string FileNameFilter { get; }

		void ExportToFile(string fileName, DataGrid dataGrid, IDataExportConverter dataExportConverter);

		Task ExportToFileAsync(string fileName, DataGrid dataGrid, IDataExportConverter dataExportConverter, CancellationToken cancellationToken);

		void ExportToClipboard(DataGrid dataGrid, IDataExportConverter dataExportConverter);

		Task ExportToClipboardAsync(DataGrid dataGrid, IDataExportConverter dataExportConverter, CancellationToken cancellationToken);
	}
}
