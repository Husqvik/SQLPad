using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
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

			const string statementText = "SELECT * FROM DUAL";
			const string watchVariable1 = "variable1";
			const string watchVariable2 = "variable2";

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
					Text = statementText,
					TabIndex = 3,
					EnableDatabaseOutput = true,
					KeepDatabaseOutputHistory = true,
					FontSize = 16,
					DebuggerViewDefaultTabIndex = 1,
					RefreshInterval = TimeSpan.FromSeconds(900),
					HeaderTextColorCode = Colors.Crimson.ToString(),
					HeaderBackgroundColorCode = Colors.CornflowerBlue.ToString(),
					WatchItems = new[] { watchVariable1, watchVariable2 }
				};

			const int expectedActiveDocumentIndex = 666;
			WorkDocumentCollection.ActiveDocumentIndex = expectedActiveDocumentIndex;
			WorkDocumentCollection.AddDocument(newWorkingDocument);

			const string providerName = "Oracle.DataAccess.Client";
			const string bindVariableDataType = "CHAR";
			const string bindVariableName = "Variable";
			const string bindVariableValue = "TestValue";
			var providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);
			providerConfiguration.SetBindVariable(
				new BindVariableConfiguration
				{
					DataType = bindVariableDataType,
					Name = bindVariableName,
					Value = bindVariableValue,
					IsFilePath = true
				});

			var statementExecutedAt = new DateTime(2015, 7, 22, 7, 53, 55);
			const string historyEntryTag = "Test";
			providerConfiguration.AddStatementExecution(
				new StatementExecutionHistoryEntry(statementText, statementExecutedAt)
				{
					Tags = historyEntryTag
				});

			WorkDocumentCollection.Save();

			var fileInfo = new FileInfo(Path.Combine(TempDirectoryName, "WorkArea", WorkDocumentCollection.ConfigurationFileName));
			fileInfo.Exists.ShouldBe(true);
			fileInfo.Length.ShouldBe(408);

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
			deserializedWorkingDocument.FontSize.ShouldBe(newWorkingDocument.FontSize);
			deserializedWorkingDocument.RefreshInterval.ShouldBe(newWorkingDocument.RefreshInterval);
			deserializedWorkingDocument.DebuggerViewDefaultTabIndex.ShouldBe(newWorkingDocument.DebuggerViewDefaultTabIndex);
			deserializedWorkingDocument.HeaderTextColorCode.ShouldBe(newWorkingDocument.HeaderTextColorCode);
			deserializedWorkingDocument.HeaderBackgroundColorCode.ShouldBe(newWorkingDocument.HeaderBackgroundColorCode);
			deserializedWorkingDocument.WatchItems.Length.ShouldBe(newWorkingDocument.WatchItems.Length);
			deserializedWorkingDocument.WatchItems[0].ShouldBe(watchVariable1);
			deserializedWorkingDocument.WatchItems[1].ShouldBe(watchVariable2);

			var deserializedProviderConfiguration = WorkDocumentCollection.GetProviderConfiguration(providerName);
			providerConfiguration.ShouldNotBeSameAs(deserializedProviderConfiguration);
			deserializedProviderConfiguration.BindVariables.Count.ShouldBe(1);
			var deserializedBindVariable = deserializedProviderConfiguration.BindVariables.First();
			deserializedBindVariable.DataType.ShouldBe(bindVariableDataType);
			deserializedBindVariable.Name.ShouldBe(bindVariableName);
			deserializedBindVariable.Value.ShouldBe(bindVariableValue);
			deserializedBindVariable.IsFilePath.ShouldBe(true);

			deserializedProviderConfiguration.StatementExecutionHistory.Count.ShouldBe(1);
			var historyEntry = deserializedProviderConfiguration.StatementExecutionHistory.Single();
			historyEntry.ExecutedAt.ShouldBe(statementExecutedAt);
			historyEntry.StatementText.ShouldBe(statementText);
			historyEntry.Tags.ShouldBe(historyEntryTag);

			WorkDocumentCollection.ActiveDocumentIndex.ShouldBe(expectedActiveDocumentIndex);
		}
	}
}
