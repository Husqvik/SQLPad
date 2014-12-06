using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
	public class WorkDocumentTest : TemporaryDirectoryTestFixture
	{
		[Test]
		public void SerializationTest()
		{
			WorkDocumentCollection.WorkingDocuments.Count.ShouldBe(0);

			var newWorkingDocument =
				new WorkDocument
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
					Text = "SELECT * FROM DUAL",
					TabIndex = 3,
					EnableDatabaseOutput = true,
					KeepDatabaseOutputHistory = true
				};

			const int expectedActiveDocumentIndex = 666;
			WorkDocumentCollection.ActiveDocumentIndex = expectedActiveDocumentIndex;
			WorkDocumentCollection.AddDocument(newWorkingDocument);
			
			const string providerName = "Oracle.DataAccess.Client";
			const string bindVariableDataType = "CHAR";
			const string bindVariableName = "Variable";
			const string bindVariableValue = "TestValue";
			var providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);
			providerConfiguration.SetBindVariable(new BindVariableConfiguration { DataType = bindVariableDataType, Name = bindVariableName, Value = bindVariableValue });

			WorkDocumentCollection.Save();

			var fileInfo = new FileInfo(Path.Combine(TempDirectoryName, "WorkArea", WorkDocumentCollection.ConfigurationFileName));
			fileInfo.Exists.ShouldBe(true);
			fileInfo.Length.ShouldBe(304);

			WorkDocumentCollection.Configure();
			WorkDocumentCollection.WorkingDocuments.Count.ShouldBe(1);
			var deserializedWorkingDocument = WorkDocumentCollection.WorkingDocuments.Single();

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
			deserializedWorkingDocument.TabIndex.ShouldBe(newWorkingDocument.TabIndex);
			deserializedWorkingDocument.EnableDatabaseOutput.ShouldBe(newWorkingDocument.EnableDatabaseOutput);
			deserializedWorkingDocument.KeepDatabaseOutputHistory.ShouldBe(newWorkingDocument.KeepDatabaseOutputHistory);

			var deserializedProviderConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);
			providerConfiguration.ShouldNotBeSameAs(deserializedProviderConfiguration);
			deserializedProviderConfiguration.BindVariables.Count.ShouldBe(1);
			var deserializedBindVariable = deserializedProviderConfiguration.BindVariables.First();
			deserializedBindVariable.DataType.ShouldBe(bindVariableDataType);
			deserializedBindVariable.Name.ShouldBe(bindVariableName);
			deserializedBindVariable.Value.ShouldBe(bindVariableValue);

			WorkDocumentCollection.ActiveDocumentIndex.ShouldBe(expectedActiveDocumentIndex);
		}
	}
}
