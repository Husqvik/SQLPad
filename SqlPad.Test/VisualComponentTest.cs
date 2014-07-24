using System;
using System.IO;
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

		static VisualComponentTest()
		{
			var sqlPadDirectory = new Uri(Path.GetDirectoryName(typeof(Snippets).Assembly.CodeBase)).LocalPath;
			Snippets.SelectSnippetDirectory(Path.Combine(sqlPadDirectory, Snippets.SnippetDirectoryName));
		}

		private void InitializeApplicationWindow()
		{
			_mainWindow = new MainWindow();
			_mainWindow.Show();
			_page = (DocumentPage)((TabItem)_mainWindow.DocumentTabControl.SelectedItem).Content;
			_page.IsParsingSynchronous = true;
			_editor = _page.Editor;
			_mainWindow.Show();
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

			Wait(0.2);

			TestPairCharacterInsertion();

			TestPairCharacterInsertionWhenNextCharacterIsThePairCharacter();
			
			TestPairCharacterInsertionAfterAlreadyEnteredPair();

			//TestPairCharacterDeletionWithCursorBetweenEmptyPair();

			_editor.IsModified = false;

			_mainWindow.Close();
			Dispatcher.CurrentDispatcher.InvokeShutdown();
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

		private void TestPairCharacterDeletionWithCursorBetweenEmptyPair()
		{
			_editor.Clear();

			EnterText("()");
			_editor.CaretOffset = 1;

			PressBackspace();

			_editor.Text.ShouldBe(String.Empty);
			_editor.CaretOffset.ShouldBe(0);
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
			_editor.TextArea.RaiseEvent(
				new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_editor.TextArea), 0, Key.Back) { RoutedEvent = Keyboard.KeyDownEvent }
				);
		}
	}
}
