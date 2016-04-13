using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace SqlPad.Test
{
	public abstract class TemporaryDirectoryTestFixture
	{
		protected string TempDirectoryName;

		[SetUp]
		public void SetUp()
		{
			TempDirectoryName = TestFixture.SetupTestDirectory();
			ConfigurationProvider.SetUserDataFolder(TempDirectoryName);
		}

		[TearDown]
		public void TearDown()
		{
			WorkDocumentCollection.ReleaseConfigurationLock();
			Directory.Delete(TempDirectoryName, true);
			Trace.WriteLine($"Test temp directory '{TempDirectoryName}' has been deleted. ");
		}
	}
}