using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SqlPad.DataExport
{
	internal class SqlInsertDataExporter : SqlBaseDataExporter
	{
		public override string Name { get; } = "SQL insert";

		protected override SqlDataExportContextBase CreateExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
		{
			return new SqlInsertExportContext(exportOptions, columns, dataExportConverter, cancellationToken);
		}
	}

	internal class SqlInsertExportContext : SqlDataExportContextBase
	{
		public SqlInsertExportContext(ExportOptions exportOptions, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, CancellationToken cancellationToken)
			: base(exportOptions, columns, dataExportConverter, cancellationToken)
		{
		}

		protected override string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders)
		{
			var headerArray = columnHeaders.ToArray();
			var sqlTemplateBuilder = new StringBuilder("INSERT INTO MY_TABLE (", 32768);
			sqlTemplateBuilder.Append(String.Join(", ", headerArray));
			sqlTemplateBuilder.Append(") VALUES (");
			sqlTemplateBuilder.Append(String.Join(", ", headerArray.Select((c, i) => $"{{{i}}}")));
			sqlTemplateBuilder.Append(");");

			return sqlTemplateBuilder.ToString();
		}
	}
}
