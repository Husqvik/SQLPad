using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal static class MakeUpperCaseCommand
	{
		public static readonly CommandExecutionHandler ExecutionHandler = new CommandExecutionHandler
		{
			Name = "MakeUpperCase",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.U, ModifierKeys.Control | ModifierKeys.Shift) },
			ExecutionHandler = ExecutionHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			var selectionStart = executionContext.SelectionStart;
			var selectionLength = executionContext.SelectionLength;
			if (selectionLength == 0)
				return;

			var grammarRecognizedStart = selectionStart + selectionLength;
			var grammarRecognizedEnd = selectionStart;

			var selectedTerminals = executionContext.Statements
				.SelectMany(s => s.AllTerminals)
				.Where(t => t.SourcePosition.IndexEnd >= selectionStart && t.SourcePosition.IndexStart < selectionStart + selectionLength)
				.ToArray();

			var isSelectionWithinSingleTerminal = selectedTerminals.Length == 1 &&
			                                      selectedTerminals[0].SourcePosition.IndexStart <= selectionStart &&
			                                      selectedTerminals[0].SourcePosition.IndexEnd + 1 >= selectionStart + selectionLength;

			foreach (var terminal in selectedTerminals.Where(t => IsUppercaseSafe(t) || isSelectionWithinSingleTerminal))
			{
				var startOffset = selectionStart > terminal.SourcePosition.IndexStart ? selectionStart - terminal.SourcePosition.IndexStart : 0;
				var indextStart = Math.Max(terminal.SourcePosition.IndexStart, selectionStart);
				grammarRecognizedStart = Math.Min(grammarRecognizedStart, indextStart);
				var indexEnd = Math.Min(terminal.SourcePosition.IndexEnd + 1, selectionStart + selectionLength);
				grammarRecognizedEnd = Math.Max(grammarRecognizedEnd, indexEnd);

				AddUppercaseSegment(executionContext.SegmentsToReplace, terminal.Token.Value, startOffset, indextStart, indexEnd - indextStart);
			}

			if (grammarRecognizedStart > selectionStart)
			{
				AddUppercaseSegment(executionContext.SegmentsToReplace, executionContext.StatementText, selectionStart, selectionStart, grammarRecognizedStart - selectionStart);
			}

			if (grammarRecognizedEnd < selectionStart + selectionLength)
			{
				AddUppercaseSegment(executionContext.SegmentsToReplace, executionContext.StatementText, grammarRecognizedEnd, grammarRecognizedEnd, selectionStart + selectionLength - grammarRecognizedEnd);
			}
		}

		private static void AddUppercaseSegment(ICollection<TextSegment> segmentsToReplace, string text, int textIndextStart, int globalIndextStart, int length)
		{
			var substring = text.Substring(textIndextStart, length);
			if (substring == substring.ToUpper())
				return;

			segmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = globalIndextStart,
					Length = length,
					Text = substring.ToUpper()
				});
		}

		private static bool IsUppercaseSafe(StatementDescriptionNode terminal)
		{
			return terminal.Id != Terminals.StringLiteral &&
			       (!terminal.Id.IsIdentifierOrAlias() || !terminal.Token.Value.StartsWith("\""));
		}
	}
}
