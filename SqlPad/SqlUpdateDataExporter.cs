using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SqlPad
{
	public abstract class SqlBaseDataExporter : IDataExporter
	{
		private const string ApostropheCharacter = "'";
		private const string EscapedApostrophe = "''";

		public string FileNameFilter
		{
			get { return "SQL files (*.sql)|*.sql|All files (*.*)|*"; }
		}

		public void ExportToClipboard(DataGrid dataGrid)
		{
			ExportToFile(null, dataGrid);
		}

		public void ExportToFile(string fileName, DataGrid dataGrid)
		{
			ExportToFileAsync(fileName, dataGrid, CancellationToken.None).Wait();
		}

		public Task ExportToClipboardAsync(DataGrid dataGrid, CancellationToken cancellationToken)
		{
			return ExportToFileAsync(null, dataGrid, cancellationToken);
		}

		public Task ExportToFileAsync(string fileName, DataGrid dataGrid, CancellationToken cancellationToken)
		{
			var orderedColumns = dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.ToArray();

			var columnHeaders = orderedColumns
				.Select((c, i) => c.Header.ToString().Replace("__", "_"));

			var sqlTemplate = BuildSqlCommandTemplate(columnHeaders);

			var rows = dataGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(sqlTemplate, rows, w, cancellationToken));
		}


		protected abstract string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders);

		private void ExportInternal(string sqlTemplate, IEnumerable rows, TextWriter writer, CancellationToken cancellationToken)
		{
			foreach (object[] rowValues in rows)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var values = rowValues.Select((t, i) => (object)FormatSqlValue(t)).ToArray();
				writer.WriteLine(sqlTemplate, values);
			}
		}

		private static string FormatSqlValue(object value)
		{
			if (DataExportHelper.IsNull(value))
			{
				return "NULL";
			}

			if(value is ValueType)
			{
				return value.ToString();
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), null, CultureInfo.CurrentUICulture).ToString();
			return String.Format("'{0}'", stringValue.Replace(ApostropheCharacter, EscapedApostrophe));
		}
	}

	public class SqlUpdateDataExporter : SqlBaseDataExporter
	{
		private const string UpdateColumnClauseMask = "{0} = {{{1}}}";

		protected override string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders)
		{
			return String.Format("UPDATE MY_TABLE SET {0};", String.Join(", ", columnHeaders.Select((h, i) => String.Format(UpdateColumnClauseMask, h, i))));
		}
	}
}
