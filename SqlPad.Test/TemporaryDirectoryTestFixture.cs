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
			WorkingDocumentCollection.SetWorkingDocumentDirectory(TempDirectoryName);
		}

		[TearDown]
		public void TearDown()
		{
			Directory.Delete(TempDirectoryName, true);
		}
	}
}