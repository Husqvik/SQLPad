using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	public class CommandExecutionHandler
	{
		public string Name { get; set; }
		
		public InputGestureCollection DefaultGestures { get; set; }
		
		public Action<ActionExecutionContext> ExecutionHandler { get; set; }
		
		public Func<ActionExecutionContext, CancellationToken, Task> ExecutionHandlerAsync { get; set; }

		public Func<ActionExecutionContext, CommandCanExecuteResult> CanExecuteHandler { get; set; }
	}

	public class ActionExecutionContext
	{
		private readonly List<TextSegment> _segmentsToReplace = new List<TextSegment>();

		public IList<TextSegment> SegmentsToReplace => _segmentsToReplace;

	    public string StatementText { get; private set; }
		
		public SqlDocumentRepository DocumentRepository { get; private set; }
		
		public int SelectionStart { get; set; }

		public int SelectionEnd => SelectionStart + SelectionLength;

		public IReadOnlyList<SourcePosition> SelectedSegments { get; set; } 

		public int CaretOffset { get; set; }

		public int SelectionLength { get; set; }
		
		public ICommandSettingsProvider SettingsProvider { get; set; }

		public void EnsureSettingsProviderAvailable()
		{
			if (SettingsProvider == null)
				throw new InvalidOperationException("Settings provider is mandatory. ");
		}

		public ActionExecutionContext(string statementText, int caretOffset, int selectionStart, int selectionLength, SqlDocumentRepository documentRepository)
		{
			StatementText = statementText;
			SelectedSegments = new[] { SourcePosition.Create(selectionStart, selectionLength) };
			SelectionStart = selectionStart;
			SelectionLength = selectionLength;
			CaretOffset = caretOffset;
			DocumentRepository = documentRepository;
		}

		public static ActionExecutionContext Create(TextEditor editor, SqlDocumentRepository documentRepository)
		{
			return
				new ActionExecutionContext(editor.Text, editor.CaretOffset, editor.SelectionStart, editor.SelectionLength, documentRepository)
				{
					SelectedSegments = editor.TextArea.Selection.Segments
						.Select(s => SourcePosition.Create(s.StartOffset, s.EndOffset - 1))
						.ToArray()
				};
		}

		public ActionExecutionContext Clone()
		{
			return (ActionExecutionContext)MemberwiseClone();
		}
	}

	public struct CommandCanExecuteResult
	{
		public bool CanExecute { get; set; }

		public bool IsLongOperation { get; set; }

		public static implicit operator CommandCanExecuteResult(bool canExecute)
		{
			return new CommandCanExecuteResult { CanExecute = canExecute };
		}

		public static implicit operator Boolean(CommandCanExecuteResult canExecuteResult)
		{
			return canExecuteResult.CanExecute;
		}
	}
}
