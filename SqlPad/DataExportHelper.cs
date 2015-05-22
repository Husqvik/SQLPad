using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
			var largeValue = value as ILargeValue;
			return value == DBNull.Value ||
			       (largeValue != null && largeValue.IsNull);
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
