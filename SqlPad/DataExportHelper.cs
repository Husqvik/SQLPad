using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad
{
	internal static class DataExportHelper
	{
		public static Task RunExportActionAsync(string fileName, Action<TextWriter> exportAction)
		{
			var stringBuilder = new StringBuilder();

			return Task.Factory
				.StartNew(() => RunExportActionInternal(fileName, stringBuilder, exportAction))
				.ContinueWith(t => SetToClipboard(fileName, stringBuilder));
		}

		private static void SetToClipboard(string fileName, StringBuilder stringBuilder)
		{
			var exportToClipboard = String.IsNullOrEmpty(fileName);
			if (exportToClipboard)
			{
				Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(stringBuilder.ToString()));
			}
		}

		public static bool IsNull(object value)
		{
			var nullable = value as IValue;
			return value == DBNull.Value ||
			       (nullable != null && nullable.IsNull);
		}

		public static IReadOnlyList<ColumnHeader> GetOrderedExportableColumns(DataGrid dataGrid)
		{
			return dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.Select(c => c.Header as ColumnHeader)
					.Where(h => h != null)
					.ToArray();
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
