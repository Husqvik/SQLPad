using System;
using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	public interface ICommandFactory
	{
		ICollection<CommandExecutionHandler> CommandHandlers { get; }
			
		DisplayCommandBase CreateFindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel);

		EditCommandBase CreateSafeDeleteCommand(StatementCollection statements, int currentPosition, IDatabaseModel databaseModel);
	}

	public class CommandExecutionHandler
	{
		public string Name { get; set; }
		public InputGestureCollection DefaultGestures { get; set; }
		public Action<CommandExecutionContext> ExecuteHandler { get; set; }
	}

	public class CommandExecutionContext
	{
		public readonly ICollection<TextSegment> SegmentsToReplace = new List<TextSegment>();

		public string StatementText { get; private set; }
		public StatementCollection Statements { get; private set; }
		public int SelectionStart { get; private set; }
		public int SelectionLength { get; private set; }

		public static CommandExecutionContext Create(TextEditor editor, StatementCollection statements)
		{
			return new CommandExecutionContext
			       {
				       SelectionStart = editor.SelectionStart,
					   SelectionLength = editor.SelectionLength,
					   StatementText = editor.Text,
					   Statements = statements
			       };
		}
	}
}
