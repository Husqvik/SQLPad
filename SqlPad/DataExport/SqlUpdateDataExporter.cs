using System;
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

		public async Task<IDataExportContext> StartExportAsync(ExportOptions options, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			var exportContext = CreateExportContext(options, columns, dataExportConverter);
			await exportContext.InitializeAsync(cancellationToken);
			return exportContext;
		}

		protected abstract SqlDataExportContextBase CreateExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter);
	}

	internal class SqlUpdateDataExporter : SqlBaseDataExporter
	{
		public override string Name { get; } = "SQL update";

		protected override SqlDataExportContextBase CreateExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter)
		{
			return new SqlUpdateExportContext(exportOptions, columns, dataExportConverter);
		}
	}

	internal abstract class SqlDataExportContextBase : DataExportContextBase
	{
		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly IDataExportConverter _dataExportConverter;

		private TextWriter _writer;
		private string _sqlCommandTemplate;

		protected SqlDataExportContextBase(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter)
			: base(exportOptions)
		{
			_columns = columns;
			_dataExportConverter = dataExportConverter;
		}

		protected override void InitializeExport()
		{
			var columnHeaders = _columns.Select(h => _dataExportConverter.ToColumnName(h.Name).Replace("{", "{{").Replace("}", "}}"));
			_sqlCommandTemplate = BuildSqlCommandTemplate(columnHeaders);

			_writer = DataExportHelper.InitializeWriter(ExportOptions.FileName, ClipboardData);
		}

		protected override Task FinalizeExport()
		{
			return _writer.FlushAsync();
		}

		protected override void Dispose(bool disposing)
		{
			_writer?.Dispose();
			base.Dispose(disposing);
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

		public SqlUpdateExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter)
			: base(exportOptions, columns, dataExportConverter)
		{
		}

		protected override string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders)
		{
			return $"UPDATE MY_TABLE SET {String.Join(", ", columnHeaders.Select((h, i) => String.Format(UpdateColumnClauseMask, h, i)))};";
		}
	}
}
