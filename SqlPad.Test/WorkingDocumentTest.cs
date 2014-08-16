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
			WorkingDocumentCollection.SetWorkingDocumentDirectory(_tempDirectoryName);
		}

		[TearDown]
		public void TearDown()
		{
			Directory.Delete(_tempDirectoryName, true);
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
					SelectionStart = 10,
					EditorGridRowHeight = 142.17,
					EditorGridColumnWidth = 98.32,
					Text = "SELECT * FROM DUAL"
				};

			const int expectedActiveDocumentIndex = 666;
			WorkingDocumentCollection.ActiveDocumentIndex = expectedActiveDocumentIndex;
			WorkingDocumentCollection.AddDocument(newWorkingDocument);
			WorkingDocumentCollection.Save();

			var fileInfo = new FileInfo(Path.Combine(_tempDirectoryName, WorkingDocumentCollection.ConfigurationFileName));
			fileInfo.Exists.ShouldBe(true);
			fileInfo.Length.ShouldBe(163);

			WorkingDocumentCollection.SetWorkingDocumentDirectory(_tempDirectoryName);
			WorkingDocumentCollection.WorkingDocuments.Count.ShouldBe(1);
			var deserializedWorkingDocument = WorkingDocumentCollection.WorkingDocuments.Single();

			deserializedWorkingDocument.ShouldNotBeSameAs(newWorkingDocument);
			deserializedWorkingDocument.ConnectionName.ShouldBe(newWorkingDocument.ConnectionName);
			deserializedWorkingDocument.CursorPosition.ShouldBe(newWorkingDocument.CursorPosition);
			deserializedWorkingDocument.DocumentFileName.ShouldBe(newWorkingDocument.DocumentFileName);
			deserializedWorkingDocument.IsModified.ShouldBe(newWorkingDocument.IsModified);
			deserializedWorkingDocument.SchemaName.ShouldBe(newWorkingDocument.SchemaName);
			deserializedWorkingDocument.SelectionLength.ShouldBe(newWorkingDocument.SelectionLength);
			deserializedWorkingDocument.SelectionStart.ShouldBe(newWorkingDocument.SelectionStart);
			deserializedWorkingDocument.Text.ShouldBe(newWorkingDocument.Text);
			deserializedWorkingDocument.DocumentId.ShouldBe(newWorkingDocument.DocumentId);
			deserializedWorkingDocument.EditorGridRowHeight.ShouldBe(newWorkingDocument.EditorGridRowHeight);
			deserializedWorkingDocument.EditorGridColumnWidth.ShouldBe(newWorkingDocument.EditorGridColumnWidth);

			WorkingDocumentCollection.ActiveDocumentIndex.ShouldBe(expectedActiveDocumentIndex);
		}
	}
}
