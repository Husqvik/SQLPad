using System;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal static class MakeUpperCaseCommand
	{
		public static CommandExecutionHandler ExecutionHandler = new CommandExecutionHandler
		{
			Name = "MakeUpperCase",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.U, ModifierKeys.Control | ModifierKeys.Shift) },
			ExecuteHandler = ExecutionHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			var selectionStart = executionContext.SelectionStart;
			var selectionLength = executionContext.SelectionLength;
			if (selectionLength == 0)
				return;

			foreach (var terminal in executionContext.Statements.SelectMany(s => s.AllTerminals)
				.Where(t => t.SourcePosition.IndexEnd >= selectionStart && t.SourcePosition.IndexStart < selectionStart + selectionLength &&
				            t.Id != Terminals.StringLiteral && (!t.Id.IsIdentifier() || !t.Token.Value.StartsWith("\""))))
			{
				var startOffset = selectionStart > terminal.SourcePosition.IndexStart ? selectionStart - terminal.SourcePosition.IndexStart : 0;
				var indextStart = Math.Max(terminal.SourcePosition.IndexStart, selectionStart);
				var indexEnd = Math.Min(terminal.SourcePosition.IndexEnd + 1, selectionStart + selectionLength);

				var substring = terminal.Token.Value.Substring(startOffset, indexEnd - indextStart);
				if (substring == substring.ToUpper())
					continue;

				executionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = indextStart,
						Length = indexEnd - indextStart,
						Text = substring.ToUpper()
					});
			}
		}
	}
}
