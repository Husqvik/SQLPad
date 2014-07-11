using System.Windows.Data;
using ICSharpCode.AvalonEdit;
using System.Globalization;
using System;

namespace SqlPad.FindReplace
{
	public class TextEditorAdapter : IEditor
	{
		public TextEditorAdapter(TextEditor editor)
		{
			_textEditor = editor;
		}

		private readonly TextEditor _textEditor;

		public string Text { get { return _textEditor.Text; } }

		public int SelectionStart { get { return _textEditor.SelectionStart; } }

		public int SelectionLength { get { return _textEditor.SelectionLength; } }

		public string SelectedText { get { return _textEditor.SelectedText; } }

		public void BeginChange() 
		{
			_textEditor.BeginChange();
		}

		public void EndChange()
		{
			_textEditor.EndChange();
		}

		public void Select(int start, int length)
		{
			_textEditor.Select(start, length);
			var location = _textEditor.Document.GetLocation(start);
			_textEditor.ScrollTo(location.Line, location.Column);
		}

		public void Replace(int start, int length, string replaceWith)
		{
			_textEditor.Document.Replace(start, length, replaceWith);
		}

	}

	public class EditorConverter : IValueConverter
	{
		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new TextEditorAdapter(value as TextEditor);
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
