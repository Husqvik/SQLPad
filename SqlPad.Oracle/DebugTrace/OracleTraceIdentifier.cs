using System;

namespace SqlPad.Oracle.DebugTrace
{
	public static class OracleTraceIdentifier
	{
		public static string Normalize(string traceIdentifier)
		{
			return String.IsNullOrEmpty(traceIdentifier) ? "''''" : $"\"{traceIdentifier.Replace('"', '_').Replace("'", "''")}\"";
		}
	}
}