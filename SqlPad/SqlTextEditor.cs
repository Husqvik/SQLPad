using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public class SqlTextEditor : TextEditor
	{
		public static readonly DependencyPropertyKey CurrentLineKey = DependencyProperty.RegisterReadOnly("CurrentLine", typeof(int), typeof(SqlTextEditor), new UIPropertyMetadata(0));
		public static readonly DependencyPropertyKey CurrentColumnKey = DependencyProperty.RegisterReadOnly("CurrentColumn", typeof(int), typeof(SqlTextEditor), new UIPropertyMetadata(0));
		public static readonly DependencyPropertyKey CurrentSelectionLengthKey = DependencyProperty.RegisterReadOnly("CurrentSelectionLength", typeof(int?), typeof(SqlTextEditor), new UIPropertyMetadata(null));

		public int CurrentLine
		{
			get { return (int)GetValue(CurrentLineKey.DependencyProperty); }
		}

		public int CurrentColumn
		{
			get { return (int)GetValue(CurrentColumnKey.DependencyProperty); }
		}

		public int? CurrentSelectionLength
		{
			get { return (int?)GetValue(CurrentSelectionLengthKey.DependencyProperty); }
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			TextArea.Caret.PositionChanged += CaretPositionChangedHandler;
			TextArea.SelectionChanged += TextAreaSelectionChangedHandler;
			//TextArea.PreviewMouseWheel += PreviewMouseWheelHandler;
		}

		private void TextAreaSelectionChangedHandler(object sender, EventArgs eventArgs)
		{
			SetValue(CurrentSelectionLengthKey, SelectionLength == 0 ? null : (int?)SelectionLength);
		}

		private void CaretPositionChangedHandler(object sender, EventArgs eventArgs)
		{
			var location = Document.GetLocation(CaretOffset);

			SetValue(CurrentLineKey, location.Line);
			SetValue(CurrentColumnKey, location.Column);
		}

		public bool TryRemoveBlockComment()
		{
			const int commentHeadingLength = 2;
			var commentRemoved = false;

			if (SelectionLength == 0)
			{
				if (Text.Length - CaretOffset >= commentHeadingLength && CaretOffset >= commentHeadingLength &&
					Text.Substring(CaretOffset - commentHeadingLength, 4) == "/**/")
				{
					commentRemoved = true;
					Document.Remove(CaretOffset - commentHeadingLength, 4);
				}

				if (Text.Length - CaretOffset >= 2 * commentHeadingLength &&
					Text.Substring(CaretOffset, 4) == "/**/")
				{
					commentRemoved = true;
					Document.Remove(CaretOffset, 4);
				}
			}
			else if (Text.Length - SelectionStart >= commentHeadingLength && SelectionStart + SelectionLength > commentHeadingLength &&
					 Text.Substring(SelectionStart, commentHeadingLength) == "/*" &&
					 Text.Substring(SelectionStart + SelectionLength - commentHeadingLength, commentHeadingLength) == "*/")
			{
				commentRemoved = true;
				Document.Remove(SelectionStart + SelectionLength - commentHeadingLength, commentHeadingLength);
				Document.Remove(SelectionStart, commentHeadingLength);
			}

			return commentRemoved;
		}

		public void ReplaceTextSegments(ICollection<TextSegment> textSegments)
		{
			Document.BeginUpdate();

			foreach (var textSegment in textSegments.OrderByDescending(s => s.IndextStart).ThenByDescending(s => s.Length))
			{
				Document.Replace(textSegment.IndextStart, textSegment.Length, textSegment.Text);
			}

			Document.EndUpdate();
		}

		public void ScrollToCaret()
		{
			var location = Document.GetLocation(CaretOffset);
			ScrollTo(location.Line, location.Column);
		}

		public int GetLineNumberByOffset(int offset)
		{
			return Document.GetLineByOffset(offset).LineNumber;
		}

		private void PreviewMouseWheelHandler(object sender, MouseWheelEventArgs e)
		{
			if (Keyboard.Modifiers != ModifierKeys.Control)
			{
				return;
			}

			if (e.Delta <= 0 || FontSize >= 50.0)
			{
				FontSize -= 1.0;
			}
			else if (e.Delta > 0 && FontSize > 10.0)
			{
				FontSize += 1.0;
			}

			e.Handled = true;
		}
	}
}
