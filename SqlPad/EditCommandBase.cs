using System;
using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public abstract class DisplayCommandBase : ICommand
	{
		public abstract bool CanExecute(object parameter);

		public void Execute(object parameter)
		{
			var segments = (ICollection<TextSegment>)parameter;
			ExecuteInternal(segments);
		}

		protected abstract void ExecuteInternal(ICollection<TextSegment> segments);

		event EventHandler ICommand.CanExecuteChanged { add { } remove { } }
	}

	public abstract class EditCommandBase : ICommand
	{
		public abstract bool CanExecute(object parameter);

		public void Execute(object parameter)
		{
			var editor = (TextEditor)parameter;
			var textSegments = new List<TextSegment>();
			ExecuteInternal(editor.Text, textSegments);

			editor.ReplaceTextSegments(textSegments);
		}

		public abstract string Title { get; }

		protected abstract void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace);

		event EventHandler ICommand.CanExecuteChanged { add { } remove { } }
	}

	public struct TextSegment
	{
		public static readonly TextSegment Empty = new TextSegment();

		public int IndextStart { get; set; }
		public int Length { get; set; }
		public string Text { get; set; }
	}
}