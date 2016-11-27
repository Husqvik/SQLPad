using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace SqlPad
{
	public static class TraceLog
	{
		public static void Log(string message)
		{
			var messageWithTimestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
			Trace.WriteLine(messageWithTimestamp);

			Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => WindowTraceLog.Instance.Log(messageWithTimestamp)));
		}
	}
}