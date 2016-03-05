using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlPad.DataExport
{
	public class SqlInsertDataExporter : SqlBaseDataExporter
	{
		public override string Name { get; } = "SQL insert";

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
