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
		private MainWindow _mainWindow;
		private DocumentPage _page;
		private TextEditor _editor;
		private string _tempDirectoryName;

		private void SetupEnvironment()
		{
			_tempDirectoryName = TestFixture.SetupTestDirectory();
			WorkingDocumentCollection.SetWorkingDocumentDirectory(_tempDirectoryName);

			var sqlPadDirectory = new Uri(Path.GetDirectoryName(typeof(Snippets).Assembly.CodeBase)).LocalPath;
			Snippets.SetSnippetDirectory(Path.Combine(sqlPadDirectory, Snippets.SnippetDirectoryName));
			DocumentPage.IsParsingSynchronous = true;
		}

		private void InitializeApplicationWindow()
		{
			SetupEnvironment();

			_mainWindow = new MainWindow();
			_mainWindow.Show();
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

			_editor.Document.Insert(0, "SELECT UNDEFINED, DUMMY AMBIGUOUS, SQLPAD_FUNCTION('Invalid parameter 1', 'Invalid parameter 2') FROM DUAL, DUAL D;\nSELECT *");
			_editor.CaretOffset = 50;

			Wait(0.1);

			_editor.CaretOffset = 108;

			GenericCommands.FindUsagesCommand.Execute(null, _editor.TextArea);
			GenericCommands.ExecuteDatabaseCommandCommand.Execute(null, _editor.TextArea);
			GenericCommands.ListContextActionCommand.Execute(null, _editor.TextArea);

			Wait(0.2);

			TestPairCharacterInsertion();

			TestPairCharacterInsertionWhenNextCharacterIsThePairCharacter();
			
			TestPairCharacterInsertionAfterAlreadyEnteredPair();
			
			TestPairCharacterInsertionBeforeExistingText();

			TestParenthesisCharacterInsertionWithinExistingParenthesis();

			TestPairCharacterDeletionWithCursorBetweenEmptyPair();

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
		public void TestExportToCsv()
		{
			var columnHeaders =
				new[]
					{
						new ColumnHeader { ColumnIndex = 0, DatabaseDataType = "Varchar2", DataType = typeof(string), Name = "DUMMY1", ValueConverterFunction = (h, v) => v },
						new ColumnHeader { ColumnIndex = 1, DatabaseDataType = "Date", DataType = typeof(DateTime), Name = "DUMMY2", ValueConverterFunction = (h, v) => ((DateTime)v).ToString("O") }
					};

			var documentPage = new DocumentPage();
			documentPage.GetType().InvokeMember("InitializeResultGrid", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod, null, documentPage, new object[] { columnHeaders });
			documentPage.ResultGrid.ItemsSource =
				new[]
					{
						new object[] {"Value \"1\"", new DateTime(2014, 8, 16, 22, 25, 34)},
						new object[] {"\"2.\"Value", new DateTime(2014, 8, 16)}
					};

			var stringBuilder = new StringBuilder();
			using (var writer = new StringWriter(stringBuilder))
			{
				documentPage.GetType().InvokeMember("ExportToCsv", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod, null, documentPage, new object[] { writer });
			}

			var result = stringBuilder.ToString();

			const string expectedResult = "\"DUMMY1\";\"DUMMY2\"\r\n\"Value \"\"1\"\"\";\"2014-08-16T22:25:34.0000000\"\r\n\"\"\"2.\"\"Value\";\"2014-08-16T00:00:00.0000000\"\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestLargeTextValueEditorInitialization()
		{
			var editor = new LargeValueEditor("Dummy", new TestLargeTextValue());
			editor.TextEditor.Text.ShouldBe(TestLargeTextValue.TextValue);
			editor.TabText.Visibility.ShouldBe(Visibility.Visible);
			editor.TabRaw.Visibility.ShouldBe(Visibility.Collapsed);
		}

		[Test, STAThread]
		public void TestLargeBinaryValueEditorInitialization()
		{
			var editor = new LargeValueEditor("Dummy", new TestLargeBinaryValue());

			editor.TabText.Visibility.ShouldBe(Visibility.Collapsed);
			editor.TabRaw.Visibility.ShouldBe(Visibility.Visible);
		}

		private class TestLargeTextValue : ILargeTextValue
		{
			public const string TextValue = "</root>";
			public string DataTypeName { get { return "CLOB"; } }
			public bool IsEditable { get { return false; } }
			public long Length { get { return TextValue.Length; } }
			public string Preview { get { throw new NotImplementedException(); } }
			public string Value { get { return TextValue; } }
			public string GetChunk(int offset, int length)
			{
				throw new NotImplementedException();
			}
		}

		private class TestLargeBinaryValue : ILargeBinaryValue
		{
			private static readonly byte[] BinaryValue = Encoding.ASCII.GetBytes("</root>");
			public string DataTypeName { get { return "CLOB"; } }
			public bool IsEditable { get { return false; } }
			public long Length { get { return BinaryValue.Length; } }
			public string Preview { get { throw new NotImplementedException(); } }
			public byte[] Value { get { return BinaryValue; } }
			public byte[] GetChunk(int offset, int length)
			{
				throw new NotImplementedException();
			}
		}
	}
}
