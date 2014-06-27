using System;
using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	public class CommandExecutionHandler
	{
		public string Name { get; set; }
		public InputGestureCollection DefaultGestures { get; set; }
		public Action<CommandExecutionContext> ExecutionHandler { get; set; }
		public Func<CommandExecutionContext, bool> CanExecuteHandler { get; set; }
	}

	public class CommandExecutionContext
	{
		public readonly ICollection<TextSegment> SegmentsToReplace = new List<TextSegment>();

		public string StatementText { get; private set; }
		
		public StatementCollection Statements { get; private set; }
		
		public int SelectionStart { get; private set; }
		
		public int CaretOffset { get; set; }

		public int Line { get; set; }
		
		public int Column { get; set; }
		
		public int SelectionLength { get; private set; }
		
		public IDatabaseModel DatabaseModel { get; private set; }
		
		public ICommandSettingsProvider SettingsProvider { get; set; }

		public void EnsureSettingsProviderAvailable()
		{
			if (SettingsProvider == null)
				throw new InvalidOperationException(String.Format("Settings provider is mandatory. "));
		}

		public CommandExecutionContext(string statementText, int caretOffset, StatementCollection statements, IDatabaseModel databaseModel)
		{
			StatementText = statementText;
			CaretOffset = caretOffset;
			SelectionStart = caretOffset;
			Statements = statements;
			DatabaseModel = databaseModel;
		}

		public static CommandExecutionContext Create(TextEditor editor, StatementCollection statements, IDatabaseModel databaseModel)
		{
			return new CommandExecutionContext(editor.Text, editor.CaretOffset, statements, databaseModel)
			{
				SelectionStart = editor.SelectionStart,
				SelectionLength = editor.SelectionLength,
				Line = editor.TextArea.Caret.Line,
				Column = editor.TextArea.Caret.Column
			};
		}
	}
}
