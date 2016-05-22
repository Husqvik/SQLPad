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
		public static Task RunExportActionAsync(string fileName, Action<TextWriter> exportAction)
		{
			var stringBuilder = new StringBuilder();
			var exportTask = Task.Run(() => RunExportActionInternal(fileName, stringBuilder, exportAction));
			var exportToClipboard = String.IsNullOrEmpty(fileName);
			if (exportToClipboard)
			{
				exportTask = exportTask.ContinueWith(t => SetToClipboard(stringBuilder), TaskContinuationOptions.OnlyOnRanToCompletion);
			}

			return exportTask;
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

		public static void ExportRowsUsingContext(ICollection rows, DataExportContextBase exportContext)
		{
			exportContext.Initialize();
			exportContext.AppendRows(rows.Cast<object[]>());
			exportContext.Complete();
		}

		private static void RunExportActionInternal(string fileName, StringBuilder stringBuilder, Action<TextWriter> exportAction)
		{
			var exportToClipboard = String.IsNullOrEmpty(fileName);

			using (var writer = exportToClipboard ? (TextWriter)new StringWriter(stringBuilder) : File.CreateText(fileName))
			{
				exportAction(writer);
			}
		}
	}
}
