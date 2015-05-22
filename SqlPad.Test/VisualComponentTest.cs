using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Test
{
	[TestFixture]
	public class VisualComponentTest
	{
		private App _app;
		private MainWindow _mainWindow;
		private DocumentPage _page;
		private TextEditor _editor;
		private string _tempDirectoryName;

		private void SetupEnvironment()
		{
			_app = new App();
			_app.InitializeComponent();

			_tempDirectoryName = TestFixture.SetupTestDirectory();
			ConfigurationProvider.SetUserDataFolder(_tempDirectoryName);

			var sqlPadDirectory = new Uri(Path.GetDirectoryName(typeof(Snippets).Assembly.CodeBase)).LocalPath;
			ConfigurationProvider.SetSnippetsFolder(Path.Combine(sqlPadDirectory, Snippets.SnippetDirectoryName));
			ConfigurationProvider.SetCodeGenerationItemFolder(Path.Combine(sqlPadDirectory, Snippets.CodeGenerationItemDirectoryName));
			DocumentPage.IsParsingSynchronous = true;
		}

		private void InitializeApplicationWindow()
		{
			SetupEnvironment();

			_mainWindow = (MainWindow)(_app.MainWindow = new MainWindow());
			_mainWindow.Show();

			var tempFile = Path.Combine(_tempDirectoryName, "tempDocument.sql");
			File.WriteAllText(tempFile, String.Empty);
			_mainWindow.GetType().GetMethod("OpenExistingFile", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_mainWindow, new object[] { tempFile });

			_page = (DocumentPage)((TabItem)_mainWindow.DocumentTabControl.SelectedItem).Content;
			_editor = _page.Editor;
		}

		private static void Wait(double seconds)
		{
			var frame = new DispatcherFrame();
			Task.Factory.StartNew(
				() =>
				{
					Thread.Sleep(TimeSpan.FromSeconds(seconds));
					frame.Continue = false;
				});
			
			Dispatcher.PushFrame(frame);
		}

		[Test(Description = @""), STAThread, RequiresThread]
		public void TestBasicApplicationBehavior()
		{
			InitializeApplicationWindow();

			const string statementText =
@"SELECT
	:BIND_VARIABLE, UNDEFINED, DUMMY AMBIGUOUS,
	SQLPAD_FUNCTION('Invalid parameter 1', 'Invalid parameter 2')
FROM DUAL, DUAL D;
SELECT *;

SELECT 1 FROM DUAL UNION ALL SELECT 2 FROM DUAL UNION ALL SELECT 3, 4 FROM DUAL;

SELECT T.* FROM T@HQ_PDB";
			
			_editor.Document.Insert(0, statementText);
			_editor.CaretOffset = 50;

			Wait(0.1);

			_editor.CaretOffset = 111;

			GenericCommands.FindUsages.Execute(null, _editor.TextArea);
			GenericCommands.ExecuteDatabaseCommandWithActualExecutionPlan.Execute(null, _editor.TextArea);
			GenericCommands.ListContextActions.Execute(null, _editor.TextArea);
			GenericCommands.ListCodeGenerationItems.Execute(null, _editor.TextArea);

			Wait(0.2);

			TestPairCharacterInsertion();

			TestPairCharacterInsertionWhenNextCharacterIsThePairCharacter();
			
			TestPairCharacterInsertionAfterAlreadyEnteredPair();
			
			TestPairCharacterInsertionBeforeExistingText();

			TestParenthesisCharacterInsertionWithinExistingParenthesis();

			TestPairCharacterDeletionWithCursorBetweenEmptyPair();

			Wait(0.2);

			_editor.IsModified = false;

			_mainWindow.Close();
			Dispatcher.CurrentDispatcher.InvokeShutdown();

			Directory.Delete(_tempDirectoryName, true);
		}

		private void TestPairCharacterInsertionBeforeExistingText()
		{
			_editor.Clear();
			EnterText("Existing text");
			_editor.CaretOffset = 0;

			EnterText("'");
			_editor.Text.ShouldBe("'Existing text");
			_editor.CaretOffset.ShouldBe(1);

			_editor.CaretOffset = 10;
			EnterText("\"");
			_editor.Text.ShouldBe("'Existing \"text");
			_editor.CaretOffset.ShouldBe(11);
		}

		private void TestPairCharacterInsertion()
		{
			_editor.Clear();
			
			EnterText("(");
			_editor.Text.ShouldBe("()");
			_editor.CaretOffset.ShouldBe(1);

			_editor.CaretOffset = 2;
			EnterText("'");
			_editor.Text.ShouldBe("()''");
			_editor.CaretOffset.ShouldBe(3);

			_editor.CaretOffset = 4;
			EnterText("\"");
			_editor.Text.ShouldBe("()''\"\"");
			_editor.CaretOffset.ShouldBe(5);
		}

		private void TestPairCharacterInsertionWhenNextCharacterIsThePairCharacter()
		{
			_editor.Clear();

			EnterText("()");
			_editor.CaretOffset = 1;
			EnterText(")");
			_editor.Text.ShouldBe("()");
			_editor.CaretOffset.ShouldBe(2);

			EnterText("''");
			_editor.CaretOffset = 3;
			EnterText("'");
			_editor.Text.ShouldBe("()''");
			_editor.CaretOffset.ShouldBe(4);

			EnterText("\"\"");
			_editor.CaretOffset = 5;
			EnterText("\"");
			_editor.Text.ShouldBe("()''\"\"");
			_editor.CaretOffset.ShouldBe(6);
		}

		private void TestPairCharacterInsertionAfterAlreadyEnteredPair()
		{
			_editor.Clear();

			EnterText("''");
			_editor.CaretOffset = 2;
			EnterText("'");
			_editor.Text.ShouldBe("'''");
			_editor.CaretOffset.ShouldBe(3);

			_editor.Clear();

			EnterText("\"\"");
			_editor.CaretOffset = 2;
			EnterText("\"");
			_editor.Text.ShouldBe("\"\"\"");
			_editor.CaretOffset.ShouldBe(3);

			_editor.Clear();

			EnterText("()");
			_editor.CaretOffset = 2;
			EnterText("(");
			_editor.Text.ShouldBe("()()");
			_editor.CaretOffset.ShouldBe(3);
		}

		private void TestParenthesisCharacterInsertionWithinExistingParenthesis()
		{
			_editor.Clear();

			EnterText("()");
			_editor.CaretOffset = 1;
			EnterText("(");
			_editor.Text.ShouldBe("(()");
			_editor.CaretOffset.ShouldBe(2);

			_editor.Clear();

			EnterText("(SELECT)");
			_editor.CaretOffset = 1;
			EnterText("(");
			_editor.Text.ShouldBe("((SELECT)");
			_editor.CaretOffset.ShouldBe(2);
		}

		private void TestPairCharacterDeletionWithCursorBetweenEmptyPair()
		{
			_editor.Clear();

			EnterText("()");
			_editor.CaretOffset = 1;

			PressBackspace();

			// TODO: Fix test
			//_editor.Text.ShouldBe(String.Empty);
			//_editor.CaretOffset.ShouldBe(0);
		}

		private void EnterText(string text)
		{
			_editor.TextArea.RaiseEvent(
				new TextCompositionEventArgs(
					InputManager.Current.PrimaryKeyboardDevice,
					new TextComposition(InputManager.Current, _editor.TextArea, text)) { RoutedEvent = TextCompositionManager.TextInputEvent }
				);
		}

		private void PressBackspace()
		{
			var keyEventArgs =
				new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_editor), 0, Key.Back)
				{
					RoutedEvent = Keyboard.PreviewKeyDownEvent
				};
			
			_editor.TextArea.RaiseEvent(keyEventArgs);
		}

		[Test, STAThread]
		public void TestCsvDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new CsvDataExporter());

			const string expectedResult = "\"DUMMY1\";\"DUMMY_WITH_UNDERSCORES\"\r\n\"Value \"\"1\"\" '2' <3>\";\"16.8.2014 22:25:34\"\r\n\"\"\"2.\"\"Value\";\"16.8.2014 00:00:00\"\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestJsonDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new JsonDataExporter());

			const string expectedResult = "[{\r\n\t\"DUMMY1\": 'Value \"1\" \\'2\\' <3>',\r\n\t\"DUMMY_WITH_UNDERSCORES\": 16.8.2014 22:25:34\r\n},\r\n{\r\n\t\"DUMMY1\": '\"2.\"Value',\r\n\t\"DUMMY_WITH_UNDERSCORES\": 16.8.2014 00:00:00\r\n}]\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestXmlDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new XmlDataExporter());

			const string expectedResult = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<data>\r\n  <row>\r\n    <DUMMY1>Value \"1\" '2' &lt;3&gt;</DUMMY1>\r\n    <DUMMY_WITH_UNDERSCORES>16.8.2014 22:25:34</DUMMY_WITH_UNDERSCORES>\r\n  </row>\r\n  <row>\r\n    <DUMMY1>\"2.\"Value</DUMMY1>\r\n    <DUMMY_WITH_UNDERSCORES>16.8.2014 00:00:00</DUMMY_WITH_UNDERSCORES>\r\n  </row>\r\n</data>";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestSqlInsertDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new SqlInsertDataExporter());

			const string expectedResult = "INSERT INTO MY_TABLE (DUMMY1, DUMMY_WITH_UNDERSCORES) VALUES ('Value \"1\" ''2'' <3>', TO_DATE('2014-08-16 22:25:34', YYYY-MM-DD HH24:MI:SS));\r\nINSERT INTO MY_TABLE (DUMMY1, DUMMY_WITH_UNDERSCORES) VALUES ('\"2.\"Value', TO_DATE('2014-08-16 00:00:00', YYYY-MM-DD HH24:MI:SS));\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestSqlUpdateDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new SqlUpdateDataExporter());

			const string expectedResult = "UPDATE MY_TABLE SET DUMMY1 = 'Value \"1\" ''2'' <3>', DUMMY_WITH_UNDERSCORES = TO_DATE('2014-08-16 22:25:34', YYYY-MM-DD HH24:MI:SS);\r\nUPDATE MY_TABLE SET DUMMY1 = '\"2.\"Value', DUMMY_WITH_UNDERSCORES = TO_DATE('2014-08-16 00:00:00', YYYY-MM-DD HH24:MI:SS);\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestHtmlDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new HtmlDataExporter());

			const string expectedResult = "<!DOCTYPE html><html><head><title></title></head><body><table border=\"1\" style=\"border-collapse:collapse\"><tr><th>DUMMY1</th><th>DUMMY_WITH_UNDERSCORES</th><tr><tr><td>Value \"1\" '2' &lt;3&gt;</td><td>16.8.2014 22:25:34</td><tr><tr><td>\"2.\"Value</td><td>16.8.2014 00:00:00</td><tr><table>";
			result.ShouldBe(expectedResult);
		}

		private string GetExportContent(DataGrid resultGrid, IDataExporter dataExporter)
		{
			var tempFileName = Path.GetTempFileName();
			dataExporter.ExportToFile(tempFileName, resultGrid, _page.InfrastructureFactory.DataExportConverter);

			var result = File.ReadAllText(tempFileName);
			File.Delete(tempFileName);
			return result;
		}

		private DataGrid InitializeDataGrid()
		{
			var columnHeaders =
				new[]
				{
					new ColumnHeader { ColumnIndex = 0, DatabaseDataType = "Varchar2", DataType = typeof (string), Name = "DUMMY1" },
					new ColumnHeader { ColumnIndex = 1, DatabaseDataType = "Date", DataType = typeof (DateTime), Name = "DUMMY_WITH_UNDERSCORES" }
				};

			var outputViewer = new OutputViewer { DataModel = new PageModel(_page) };
			outputViewer.Initialize(columnHeaders);
			outputViewer.ResultGrid.ItemsSource =
				new[]
				{
					new object[] { "Value \"1\" '2' <3>", new DateTime(2014, 8, 16, 22, 25, 34) },
					new object[] { "\"2.\"Value", new DateTime(2014, 8, 16) }
				};
			
			return outputViewer.ResultGrid;
		}

		[Test, STAThread]
		public void TestLargeTextValueEditorInitialization()
		{
			var editor = new LargeValueEditor("Dummy", new TestLargeTextValue());
			var task = (Task) typeof (LargeValueEditor).GetMethod("SetEditorValue", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(editor, null);
			task.Wait();
			
			editor.TextEditor.Text.ShouldBe(TestLargeTextValue.TextValue);
			editor.TabText.Visibility.ShouldBe(Visibility.Visible);
			editor.TabRaw.Visibility.ShouldBe(Visibility.Collapsed);
		}

		private class TestLargeTextValue : ILargeTextValue
		{
			public const string TextValue = "</root>";

			public string DataTypeName { get { return "CLOB"; } }

			public bool IsEditable { get { return false; } }
			
			public bool IsNull { get { return false; } }

			public long Length { get { return TextValue.Length; } }

			public string Preview { get { throw new NotImplementedException(); } }

			public string Value { get { return TextValue; } }

			public void GetChunk(StringBuilder stringBuilder, int offset, int length)
			{
				throw new NotImplementedException();
			}

			public void Prefetch()
			{
				throw new NotImplementedException();
			}
		}
	}
}
