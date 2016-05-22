using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SqlPad.DataExport
{
	internal class SqlInsertDataExporter : SqlBaseDataExporter
	{
		public override string Name { get; } = "SQL insert";

		protected override SqlDataExportContextBase CreateExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			return new SqlInsertExportContext(writer, columns, dataExportConverter, totalRows, reportProgress, cancellationToken);
		}
	}

	internal class SqlInsertExportContext : SqlDataExportContextBase
	{
		public SqlInsertExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, IDataExportConverter dataExportConverter, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(writer, columns, dataExportConverter, totalRows, reportProgress, cancellationToken)
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
