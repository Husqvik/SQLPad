using System;
using System.Collections.Generic;
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
		
		public Action<CommandExecutionContext> ExecutionHandler { get; set; }
		
		public Func<CommandExecutionContext, CancellationToken, Task> ExecutionHandlerAsync { get; set; }
		
		public Func<CommandExecutionContext, bool> CanExecuteHandler { get; set; }
	}

	public class CommandExecutionContext
	{
		public readonly ICollection<TextSegment> SegmentsToReplace = new List<TextSegment>();

		public string StatementText { get; private set; }
		
		public SqlDocumentRepository DocumentRepository { get; private set; }
		
		public int SelectionStart { get; private set; }

		public int SelectionEnd { get { return SelectionStart + SelectionLength; } }
		
		public int CaretOffset { get; set; }

		public int SelectionLength { get; set; }
		
		public ICommandSettingsProvider SettingsProvider { get; set; }

		public void EnsureSettingsProviderAvailable()
		{
			if (SettingsProvider == null)
				throw new InvalidOperationException(String.Format("Settings provider is mandatory. "));
		}

		public CommandExecutionContext(string statementText, int caretOffset, int selectionStart, int selectionLength, SqlDocumentRepository documentRepository)
		{
			StatementText = statementText;
			SelectionStart = selectionStart;
			SelectionLength = selectionLength;
			CaretOffset = caretOffset;
			SelectionStart = caretOffset;
			DocumentRepository = documentRepository;
		}

		public static CommandExecutionContext Create(TextEditor editor, SqlDocumentRepository documentRepository)
		{
			return new CommandExecutionContext(editor.Text, editor.CaretOffset, editor.SelectionStart, editor.SelectionLength, documentRepository)
			{
				SelectionStart = editor.SelectionStart,
				SelectionLength = editor.SelectionLength,
			};
		}

		public CommandExecutionContext Clone()
		{
			return (CommandExecutionContext)MemberwiseClone();
		}
	}
}
