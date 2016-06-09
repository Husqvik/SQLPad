using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad.DataExport
{
	internal static class DataExportHelper
	{
		public static async Task RunExportActionAsync(string fileName, Func<TextWriter, Task> exportAction)
		{
			var stringBuilder = new StringBuilder();
			await RunExportActionInternal(fileName, stringBuilder, exportAction);
			var exportToClipboard = String.IsNullOrEmpty(fileName);
			if (exportToClipboard)
			{
				SetToClipboard(stringBuilder);
			}
		}

		private static void SetToClipboard(StringBuilder stringBuilder)
		{
			Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(stringBuilder.ToString()));
		}

		public static IReadOnlyList<ColumnHeader> GetOrderedExportableColumns(DataGrid dataGrid)
		{
			return
				dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.Select(c => c.Header as ColumnHeader)
					.Where(h => h != null)
					.ToArray();
		}

		public static async Task ExportRowsUsingContext(ICollection rows, DataExportContextBase exportContext)
		{
			await exportContext.InitializeAsync();
			await exportContext.AppendRowsAsync(rows.Cast<object[]>());
			await exportContext.FinalizeAsync();
		}

		private static async Task RunExportActionInternal(string fileName, StringBuilder stringBuilder, Func<TextWriter, Task> exportAction)
		{
			var exportToClipboard = String.IsNullOrEmpty(fileName);

			using (var writer = exportToClipboard ? (TextWriter)new StringWriter(stringBuilder) : File.CreateText(fileName))
			{
				await exportAction(writer);
			}
		}
	}
}
