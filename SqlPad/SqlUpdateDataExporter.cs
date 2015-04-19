using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace SqlPad
{
	public abstract class SqlBaseDataExporter : IDataExporter
	{
		private const string ApostropheCharacter = "'";
		private const string EscapedApostrophe = "''";

		public string FileNameFilter
		{
			get { return "SQL files (*.sql)|*.json|All files (*.*)|*"; }
		}

		public void Export(string fileName, DataGrid dataGrid)
		{
			ExportAsync(fileName, dataGrid, CancellationToken.None).Wait();
		}

		public Task ExportAsync(string fileName, DataGrid dataGrid, CancellationToken cancellationToken)
		{
			var orderedColumns = dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.ToArray();

			var columnHeaders = orderedColumns
				.Select((c, i) => c.Header.ToString().Replace("__", "_"));

			var sqlTemplate = BuildSqlCommandTemplate(columnHeaders);

			var converterParameters = orderedColumns
				.Select(c => ((Binding)((DataGridTextColumn)c).Binding).ConverterParameter)
				.ToArray();

			var rows = dataGrid.Items;

			return Task.Factory.StartNew(() => ExportInternal(sqlTemplate, rows, converterParameters, fileName, cancellationToken), cancellationToken);
		}


		protected abstract string BuildSqlCommandTemplate(IEnumerable<string> columnHeaders);

		private void ExportInternal(string sqlTemplate, IEnumerable rows, IReadOnlyList<object> converterParameters, string fileName, CancellationToken cancellationToken)
		{
			using (var writer = File.CreateText(fileName))
			{
				foreach (object[] rowValues in rows)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var values = rowValues.Select((t, i) => (object)FormatSqlValue(t, converterParameters[i])).ToArray();
					writer.WriteLine(sqlTemplate, values);
				}
			}
		}

		private static string FormatSqlValue(object value, object converterParameter)
		{
			if (value == DBNull.Value)
				return "NULL";

			if (value is ValueType)
			{
				return value.ToString();
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), converterParameter, CultureInfo.CurrentUICulture).ToString();
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
