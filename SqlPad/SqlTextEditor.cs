using System;
using System.Windows;
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
	}
}
