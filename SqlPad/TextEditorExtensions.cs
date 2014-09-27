using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public static class TextEditorExtensions
	{
		public static bool TryRemoveBlockComment(this TextEditor editor)
		{
			const int commentHeadingLength = 2;
			var commentRemoved = false;

			if (editor.SelectionLength == 0)
			{
				if (editor.Text.Length - editor.CaretOffset >= commentHeadingLength && editor.CaretOffset >= commentHeadingLength &&
					editor.Text.Substring(editor.CaretOffset - commentHeadingLength, 4) == "/**/")
				{
					commentRemoved = true;
					editor.Document.Remove(editor.CaretOffset - commentHeadingLength, 4);
				}

				if (editor.Text.Length - editor.CaretOffset >= 2 * commentHeadingLength &&
					editor.Text.Substring(editor.CaretOffset, 4) == "/**/")
				{
					commentRemoved = true;
					editor.Document.Remove(editor.CaretOffset, 4);
				}
			}
			else if (editor.Text.Length - editor.SelectionStart >= commentHeadingLength && editor.SelectionStart + editor.SelectionLength > commentHeadingLength &&
					 editor.Text.Substring(editor.SelectionStart, commentHeadingLength) == "/*" &&
					 editor.Text.Substring(editor.SelectionStart + editor.SelectionLength - commentHeadingLength, commentHeadingLength) == "*/")
			{
				commentRemoved = true;
				editor.Document.Remove(editor.SelectionStart + editor.SelectionLength - commentHeadingLength, commentHeadingLength);
				editor.Document.Remove(editor.SelectionStart, commentHeadingLength);
			}

			return commentRemoved;
		}

		public static void ReplaceTextSegments(this TextEditor editor, ICollection<TextSegment> textSegments)
		{
			editor.Document.BeginUpdate();

			foreach (var textSegment in textSegments.OrderByDescending(s => s.IndextStart).ThenByDescending(s => s.Length))
			{
				editor.Document.Replace(textSegment.IndextStart, textSegment.Length, textSegment.Text);
			}

			editor.Document.EndUpdate();
		}

		public static void ScrollToCaret(this TextEditor editor)
		{
			var location = editor.Document.GetLocation(editor.CaretOffset);
			editor.ScrollTo(location.Line, location.Column);
		}

		public static int GetLineNumberByOffset(this TextEditor editor, int offset)
		{
			return editor.Document.GetLineByOffset(offset).LineNumber;
		}
	}
}
