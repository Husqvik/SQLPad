using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
	public class WorkingDocumentTest
	{
		private string _tempDirectoryName;

		[SetUp]
		public void SetUp()
		{
			_tempDirectoryName = TestFixture.SetupTestDirectory();

			var sourceName = Path.Combine("TestFiles", WorkingDocumentCollection.ConfigurationFileName);
			var destinationName = Path.Combine(_tempDirectoryName, WorkingDocumentCollection.ConfigurationFileName);
			File.Copy(sourceName, destinationName, true);
			WorkingDocumentCollection.SetWorkingDocumentDirectory(_tempDirectoryName);
		}

		[TearDown]
		public void TearDown()
		{
			Directory.Delete(_tempDirectoryName, true);
		}

		[Test]
		public void InitializationTest()
		{
			var workingDocuments = WorkingDocumentCollection.WorkingDocuments.ToArray();
			workingDocuments.Length.ShouldBe(3);
			workingDocuments[0].DocumentFileName.ShouldBe(null);
			workingDocuments[0].SchemaName.ShouldBe("MMA_DEV");
			workingDocuments[0].IsModified.ShouldBe(true);
			workingDocuments[0].CursorPosition.ShouldBe(22);
			workingDocuments[0].ConnectionName.ShouldBe("Oracle 12c PDB EZ-Connect");
			workingDocuments[0].WorkingFile.Name.ShouldBe("WorkingDocument_e03fc4ff2480476f8bdc5cf245e7104d.working");
			workingDocuments[2].DocumentFileName.ShouldBe(null);
			workingDocuments[2].SchemaName.ShouldBe("CA_DEV");
			workingDocuments[2].IsModified.ShouldBe(true);
			workingDocuments[2].CursorPosition.ShouldBe(60);
			workingDocuments[2].ConnectionName.ShouldBe("Oracle 12c PDB EZ-Connect");
			workingDocuments[2].WorkingFile.Name.ShouldBe("WorkingDocument_19557492bb62410abe880adf1975999b.working");
		}

		[Test]
		public void SerializationTest()
		{
			WorkingDocumentCollection.CloseAllDocuments();
			WorkingDocumentCollection.WorkingDocuments.Count.ShouldBe(0);

			var newWorkingDocument =
				new WorkingDocument
				{
					ConnectionName = "DummyConnection",
					CursorPosition = 999,
					DocumentFileName = "DummyDocument.sql",
					IsModified = true,
					SchemaName = "DummySchema",
					SelectionLength = 20,
					SelectionStart = 10
				};

			const int expectedActiveDocumentIndex = 666;
			WorkingDocumentCollection.ActiveDocumentIndex = expectedActiveDocumentIndex;
			WorkingDocumentCollection.AddDocument(newWorkingDocument);
			WorkingDocumentCollection.Save();

			WorkingDocumentCollection.SetWorkingDocumentDirectory(_tempDirectoryName);
			var deserializedWorkingDocument = WorkingDocumentCollection.WorkingDocuments.Single();

			deserializedWorkingDocument.ShouldNotBeSameAs(newWorkingDocument);
			deserializedWorkingDocument.ConnectionName.ShouldBe(newWorkingDocument.ConnectionName);
			deserializedWorkingDocument.CursorPosition.ShouldBe(newWorkingDocument.CursorPosition);
			deserializedWorkingDocument.DocumentFileName.ShouldBe(newWorkingDocument.DocumentFileName);
			deserializedWorkingDocument.IsModified.ShouldBe(newWorkingDocument.IsModified);
			deserializedWorkingDocument.SchemaName.ShouldBe(newWorkingDocument.SchemaName);
			deserializedWorkingDocument.SelectionLength.ShouldBe(newWorkingDocument.SelectionLength);
			deserializedWorkingDocument.SelectionStart.ShouldBe(newWorkingDocument.SelectionStart);

			WorkingDocumentCollection.ActiveDocumentIndex.ShouldBe(expectedActiveDocumentIndex);
		}
	}
}
