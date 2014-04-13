using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public abstract class CommandBase : ICommand
	{
		public abstract bool CanExecute(object parameter);

		public void Execute(object parameter)
		{
			var editor = (TextEditor)parameter;
			var textSegments = new List<TextSegment>();
			ExecuteInternal(textSegments);

			editor.Document.BeginUpdate();

			foreach (var textSegment in textSegments.OrderByDescending(s => s.IndextStart))
			{
				editor.Document.Replace(textSegment.IndextStart, textSegment.Length, textSegment.Text);
			}

			editor.Document.EndUpdate();
		}

		protected abstract void ExecuteInternal(ICollection<TextSegment> segmentsToReplace);

		public abstract event EventHandler CanExecuteChanged;
	}

	public struct TextSegment
	{
		public int IndextStart { get; set; }
		public int Length { get; set; }
		public string Text { get; set; }
	}
}