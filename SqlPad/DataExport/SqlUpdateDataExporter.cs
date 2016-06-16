using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	internal abstract class SqlBaseDataExporter : IDataExporter
	{
		public abstract string Name { get; }

		public string FileNameFilter { get; } = "SQL files (*.sql)|*.sql|All files (*.*)|*";

		public string FileExtension { get; } = "sql";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public async Task<IDataExportContext> StartExportAsync(string fileName, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = CreateExportContext(File.CreateText(fileName), columns, dataExportConverter, null, null, cancellationToken);
			await exportContext.InitializeAsync();
			return exportContext;
		}

		public Task ExportToFileAsync(string fileName, DataGridResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);
			var rows = resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, rows, w, dataExportConverter, reportProgress, cancellationToken));
		}

		protected abstract SqlDataExportContextBase CreateExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken);


		protected Task ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, TextWriter writer, IDataExportConverter dataExportConverter, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			return DataExportHelper.ExportRowsUsingContext(rows, CreateExportContext(writer, orderedColumns, dataExportConverter, rows.Count, reportProgress, cancellationToken));
		}
	}

	internal class SqlUpdateDataExporter : SqlBaseDataExporter
	{
		public override string Name { get; } = "SQL update";

		protected override SqlDataExportContextBase CreateExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			return new SqlUpdateExportContext(writer, columns, dataExportConverter, totalRows, reportProgress, cancellationToken);
		}
	}

	internal abstract class SqlDataExportContextBase : DataExportContextBase
	{
		private readonly TextWriter _writer;
		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly IDataExportConverter _dataExportConverter;

		private string _sqlCommandTemplate;

		protected SqlDataExportContextBase(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_writer = writer;
			_columns = columns;
			_dataExportConverter = dataExportConverter;
		}

		protected override void InitializeExport()
		{
			var columnHeaders = _columns.Select(h => _dataExportConverter.ToColumnName(h.Name).Replace("{", "{{").Replace("}", "}}"));
			_sqlCommandTemplate = BuildSqlCommandTemplate(columnHeaders);
		}

		protected override void Dispose(bool disposing)
		{
			_writer.Dispose();
		}

		protected override void ExportRow(object[] rowValues)
		{
			_writer.WriteLine(_sqlCommandTemplate, _columns.Select(h => (object)FormatSqlValue(rowValues[h.ColumnIndex])).ToArray());
		}

		protected abstract string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders);

		private string FormatSqlValue(object value)
		{
			return IsNull(value)
				? "NULL"
				: _dataExportConverter.ToSqlValue(value);
		}
	}

	internal class SqlUpdateExportContext : SqlDataExportContextBase
	{
		private const string UpdateColumnClauseMask = "{0} = {{{1}}}";

		public SqlUpdateExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(writer, columns, dataExportConverter, totalRows, reportProgress, cancellationToken)
		{
		}

		protected override string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders)
		{
			return $"UPDATE MY_TABLE SET {String.Join(", ", columnHeaders.Select((h, i) => String.Format(UpdateColumnClauseMask, h, i)))};";
		}
	}
}
