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
		public abstract string Name { get; }

		public string FileNameFilter { get; } = "SQL files (*.sql)|*.sql|All files (*.*)|*";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var columnHeaders = orderedColumns
				.Select(h => dataExportConverter.ToColumnName(h.Name).Replace("{", "{{").Replace("}", "}}"));

			var sqlTemplate = BuildSqlCommandTemplate(columnHeaders);

			var rows = resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, sqlTemplate, rows, w, dataExportConverter, cancellationToken, reportProgress));
		}


		protected abstract string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders);

		private static void ExportInternal(IEnumerable<ColumnHeader> orderedColumns, string sqlTemplate, ICollection rows, TextWriter writer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress)
		{
			DataExportHelper.ExportRows(
				rows,
				(rowValues, isLastRow) => writer.WriteLine(sqlTemplate, orderedColumns.Select(h => (object)FormatSqlValue(rowValues[h.ColumnIndex], dataExportConverter)).ToArray()),
				reportProgress,
				cancellationToken);
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

		public override string Name { get; } = "SQL update";

		protected override string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders)
		{
			return $"UPDATE MY_TABLE SET {String.Join(", ", columnHeaders.Select((h, i) => String.Format(UpdateColumnClauseMask, h, i)))};";
		}
	}
}
