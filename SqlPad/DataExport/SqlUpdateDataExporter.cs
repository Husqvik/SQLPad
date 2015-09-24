using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public abstract class SqlBaseDataExporter : IDataExporter
	{
		public string FileNameFilter => "SQL files (*.sql)|*.sql|All files (*.*)|*";

	    public void ExportToClipboard(ResultViewer resultViewer, IDataExportConverter dataExportConverter)
		{
			ExportToFile(null, resultViewer, dataExportConverter);
		}

		public void ExportToFile(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter)
		{
			ExportToFileAsync(fileName, resultViewer, dataExportConverter, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken);
		}

		public Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var columnHeaders = orderedColumns
				.Select(h => dataExportConverter.ToColumnName(h.Name).Replace("{", "{{").Replace("}", "}}"));

			var sqlTemplate = BuildSqlCommandTemplate(columnHeaders);

			var rows = resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, sqlTemplate, rows, w, dataExportConverter, cancellationToken));
		}


		protected abstract string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders);

		private void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, string sqlTemplate, IEnumerable rows, TextWriter writer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var values = orderedColumns.Select(h => (object)FormatSqlValue(rowValues[h.ColumnIndex], dataExportConverter)).ToArray();
				writer.WriteLine(sqlTemplate, values);
			}
		}

		private static string FormatSqlValue(object value, IDataExportConverter dataExportConverter)
		{
			return DataExportHelper.IsNull(value)
				? "NULL"
				: dataExportConverter.ToSqlValue(value);
		}
	}

	public class SqlUpdateDataExporter : SqlBaseDataExporter
	{
		private const string UpdateColumnClauseMask = "{0} = {{{1}}}";

		protected override string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders)
		{
			return $"UPDATE MY_TABLE SET {String.Join(", ", columnHeaders.Select((h, i) => String.Format(UpdateColumnClauseMask, h, i)))};";
		}
	}
}
