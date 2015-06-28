using System;
using System.Diagnostics;
using System.IO;

namespace SqlPad.Test
{
	public static class TestFixture
	{
		public static string SetupTestDirectory()
		{
			var tempDirectoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempDirectoryName);
			Trace.WriteLine(String.Format("Test temp directory '{0}' has been created. ", tempDirectoryName));
			return tempDirectoryName;
		}
	}
}