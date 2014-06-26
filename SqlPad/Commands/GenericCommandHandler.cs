using System;
using System.Linq;
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

		public static ExecutedRoutedEventHandler CreateRoutedEditCommandHandler(CommandExecutionHandler handler, Func<SqlDocument> getSqlDocumentFunction, IDatabaseModel databaseModel)
		{
			return (sender, args) =>
					{
						var editor = GetEditorFromSender(sender);
						var sqlDocument = getSqlDocumentFunction();
						if (sqlDocument.StatementText != editor.Text)
							return;

						var executionContext = CommandExecutionContext.Create(editor, sqlDocument.StatementCollection, databaseModel);
						handler.ExecutionHandler(executionContext);
						editor.ReplaceTextSegments(executionContext.SegmentsToReplace);

						if (executionContext.Column != editor.TextArea.Caret.Column)
							editor.TextArea.Caret.Column = executionContext.Column;

						if (executionContext.Line != editor.TextArea.Caret.Line)
							editor.TextArea.Caret.Line = executionContext.Line;
					};
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
				var currentLineText = editor.Document.GetText(currentLine) + "\n";
				editor.Document.Insert(currentLine.EndOffset + 1, currentLineText);
				caretOffset = editor.SelectionStart + editor.SelectionLength + currentLineText.Length;
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
}
