using ICSharpCode.AvalonEdit;
using System.Globalization;
using System;
using System.Windows.Controls;

namespace SqlPad.FindReplace
{
	public class TextEditorAdapter : IEditor
	{
		public TextEditorAdapter(TextEditor editor)
		{
			_editor = editor;
		}

		public Control Control => _editor;

		private readonly TextEditor _editor;

		public string Text => _editor.Text;

		public int SelectionStart => _editor.SelectionStart;

		public int SelectionLength => _editor.SelectionLength;

		public string SelectedText => _editor.SelectedText;

		public void BeginChange() 
		{
			_editor.BeginChange();
		}

		public void EndChange()
		{
			_editor.EndChange();
		}

		public void Select(int start, int length)
		{
			_editor.Select(start, length);
			var location = _editor.Document.GetLocation(start);
			_editor.ScrollTo(location.Line, location.Column);
		}

		public void Replace(int start, int length, string replaceWith)
		{
			_editor.Document.Replace(start, length, replaceWith);
		}

	}

	public class EditorConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new TextEditorAdapter((TextEditor)value);
		}
	}
}
