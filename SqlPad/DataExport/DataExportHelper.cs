using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace SqlPad.DataExport
{
	internal static class DataExportHelper
	{
		public static IReadOnlyList<ColumnHeader> GetOrderedExportableColumns(DataGrid dataGrid)
		{
			return
				dataGrid.Columns
					.OrderBy(c => c.DisplayIndex)
					.Select(c => c.Header as ColumnHeader)
					.Where(h => h != null)
					.ToArray();
		}

		public static TextWriter InitializeWriter(string fileName, Stream stream)
		{
			return String.IsNullOrEmpty(fileName) ? new StreamWriter(stream) : File.CreateText(fileName);
		}
	}
}
