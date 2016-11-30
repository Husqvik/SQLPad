using System;
using System.Diagnostics;

namespace SqlPad
{
	public static class TraceLog
	{
		public static void WriteLine(string message)
		{
			var messageWithTimestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
			Trace.WriteLine(messageWithTimestamp);

			WindowTraceLog.WriteLine(messageWithTimestamp);
		}
	}
}