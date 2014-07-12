using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;

namespace SqlPad.Commands
{
	internal class GenericCommandHandler
	{
		private const string LineCommentPrefix = "--";

		private static TextEditor GetEditorFromSender(object sender)
		{
			return (TextEditor)((TextArea)sender).TextView.Services.GetService(typeof(TextEditor));
		}

		public static ExecutedRoutedEventHandler CreateRoutedEditCommandHandler(CommandExecutionHandler handler, Func<SqlDocumentRepository> getDocumentRepositoryFunction)
		{
			return (sender, args) =>
					{
						var editor = GetEditorFromSender(sender);
						ExecuteEditCommand(getDocumentRepositoryFunction(), editor, handler.ExecutionHandler);
					};
		}

		public static void ExecuteEditCommand(SqlDocumentRepository documentRepository, TextEditor editor, Action<CommandExecutionContext> executionHandler)
		{
			if (documentRepository.StatementText != editor.Text)
				return;

			var executionContext = CommandExecutionContext.Create(editor, documentRepository);
			executionHandler(executionContext);
			UpdateDocument(editor, executionContext);
		}

		public static void UpdateDocument(TextEditor editor, CommandExecutionContext executionContext)
		{
			var line = editor.TextArea.Caret.Line;
			var column = editor.TextArea.Caret.Column;
			var caretOffset = editor.CaretOffset;
			editor.ReplaceTextSegments(executionContext.SegmentsToReplace);

			if (executionContext.Line != line)
				editor.TextArea.Caret.Line = executionContext.Line;

			if (executionContext.Column != column)
				editor.TextArea.Caret.Column = executionContext.Column;

			if (executionContext.CaretOffset != caretOffset)
				editor.CaretOffset = executionContext.CaretOffset;
		}

		public static void DuplicateText(object sender, ExecutedRoutedEventArgs args)
		{
			var editor = GetEditorFromSender(sender);

			int caretOffset;
			if (editor.SelectionLength > 0)
			{
				caretOffset = editor.SelectionStart + editor.SelectionLength + editor.SelectedText.Length;
				editor.Document.Insert(editor.SelectionStart + editor.SelectionLength, editor.SelectedText);
			}
			else
			{
				var currentLine = editor.Document.GetLineByOffset(editor.CaretOffset);
				var currentLineText = "\n" + editor.Document.GetText(currentLine);
				caretOffset = editor.SelectionStart + currentLineText.Length;
				editor.Document.Insert(currentLine.EndOffset, currentLineText);
			}

			editor.SelectionLength = 0;
			editor.CaretOffset = caretOffset;
		}

		public static void HandleBlockComments(object sender, ExecutedRoutedEventArgs args)
		{
			var editor = GetEditorFromSender(sender);

			editor.BeginChange();

			int caretOffset;
			if (editor.TryRemoveBlockComment())
			{
				caretOffset = editor.CaretOffset;
			}
			else
			{
				editor.Document.Insert(editor.SelectionStart, "/*");
				caretOffset = editor.CaretOffset;
				editor.Document.Insert(editor.SelectionStart + editor.SelectionLength, "*/");
			}

			editor.SelectionLength = 0;
			editor.CaretOffset = caretOffset;
			editor.EndChange();
		}

		public static void HandleLineComments(object sender, ExecutedRoutedEventArgs args)
		{
			var editor = GetEditorFromSender(sender);

			var startLine = editor.Document.GetLineByOffset(editor.SelectionStart);
			var endLine = editor.Document.GetLineByOffset(editor.SelectionStart + editor.SelectionLength);

			var lines = Enumerable.Range(startLine.LineNumber, endLine.LineNumber - startLine.LineNumber + 1)
				.Select(l => editor.Document.GetLineByNumber(l)).ToArray();

			var allLinesCommented = lines
				.All(l => editor.Text.Substring(startLine.Offset, startLine.Length).TrimStart().StartsWith(LineCommentPrefix));

			editor.BeginChange();

			foreach (var line in lines)
			{
				if (allLinesCommented)
				{
					editor.Document.Remove(line.Offset + editor.Text.Substring(line.Offset, line.Length).IndexOf(LineCommentPrefix, StringComparison.InvariantCulture), 2);
				}
				else
				{
					editor.Document.Insert(line.Offset, LineCommentPrefix);
				}
			}

			var caretOffset = editor.CaretOffset;
			editor.SelectionLength = 0;
			editor.CaretOffset = caretOffset;

			editor.EndChange();
		}
	}

	public static class GenericCommands
	{
		public static RoutedCommand ShowFunctionOverloadCommand = new RoutedCommand("ShowFunctionOverloads", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Space, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand DuplicateTextCommand = new RoutedCommand("DuplicateText", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.D, ModifierKeys.Control) });
		public static RoutedCommand BlockCommentCommand = new RoutedCommand("BlockComment", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Oem2, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand LineCommentCommand = new RoutedCommand("LineComment", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Oem2, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand ListContextActionCommand = new RoutedCommand("ListContextActions", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Enter, ModifierKeys.Alt) });
		public static RoutedCommand MultiNodeEditCommand = new RoutedCommand("EditMultipleNodes", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F6, ModifierKeys.Shift) });
		public static RoutedCommand NavigateToPreviousUsageCommand = new RoutedCommand("NavigateToPreviousHighlightedUsage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.PageUp, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand NavigateToNextUsageCommand = new RoutedCommand("NavigateToNextHighlightedUsage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.PageDown, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand NavigateToQueryBlockRootCommand = new RoutedCommand("NavigateToQueryBlockRoot", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Home, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand NavigateToDefinitionRootCommand = new RoutedCommand("NavigateToDefinition", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F12) });
		public static RoutedCommand ExecuteDatabaseCommandCommand = new RoutedCommand("ExecuteDatabaseCommand", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F9), new KeyGesture(Key.Enter, ModifierKeys.Control) });
		public static RoutedCommand SaveCommand = new RoutedCommand("Save", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });
		public static RoutedCommand FormatStatementCommand = new RoutedCommand("FormatStatement", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand FindUsagesCommand = new RoutedCommand("FindUsages", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F11, ModifierKeys.Alt | ModifierKeys.Shift) });
		public static RoutedCommand FetchNextRowsCommand = new RoutedCommand("FetchNextRows", typeof(DataGrid), new InputGestureCollection { new KeyGesture(Key.PageDown), new KeyGesture(Key.Down) });
	}
}
