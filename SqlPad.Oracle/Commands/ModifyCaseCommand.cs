using System;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class ModifyCaseCommand
	{
		private readonly ActionExecutionContext _executionContext;
		private readonly Func<string, string> _changeCaseFunction;

		public static readonly CommandExecutionHandler MakeUpperCase = new CommandExecutionHandler
		{
			Name = "MakeUpperCase",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.U, ModifierKeys.Control | ModifierKeys.Shift) },
			ExecutionHandler = MakeUpperCaseHandler
		};

		public static readonly CommandExecutionHandler MakeLowerCase = new CommandExecutionHandler
		{
			Name = "MakeLowerCase",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Shift) },
			ExecutionHandler = MakeLowerCaseHandler
		};

		private static void MakeLowerCaseHandler(ActionExecutionContext executionContext)
		{
			new ModifyCaseCommand(executionContext, s => s.ToLower()).ModifyCase();
		}

		private static void MakeUpperCaseHandler(ActionExecutionContext executionContext)
		{
			new ModifyCaseCommand(executionContext, s => s.ToUpper()).ModifyCase();
		}

		private ModifyCaseCommand(ActionExecutionContext executionContext, Func<string, string> changeCaseFunction)
		{
			_changeCaseFunction = changeCaseFunction;
			_executionContext = executionContext;
		}

		private void ModifyCase()
		{
			var selectionStart = _executionContext.SelectionStart;
			var selectionLength = _executionContext.SelectionLength;
			if (selectionLength == 0)
				return;

			var grammarRecognizedStart = selectionStart + selectionLength;
			var grammarRecognizedEnd = selectionStart;

			var selectedTerminals = _executionContext.DocumentRepository.Statements
				.SelectMany(s => s.AllTerminals)
				.Where(t => t.SourcePosition.IndexEnd >= selectionStart && t.SourcePosition.IndexStart < selectionStart + selectionLength)
				.ToArray();

			var isSelectionWithinSingleTerminal = selectedTerminals.Length == 1 &&
			                                      selectedTerminals[0].SourcePosition.IndexStart <= selectionStart &&
			                                      selectedTerminals[0].SourcePosition.IndexEnd + 1 >= selectionStart + selectionLength;

			foreach (var terminal in selectedTerminals)
			{
				var startOffset = selectionStart > terminal.SourcePosition.IndexStart ? selectionStart - terminal.SourcePosition.IndexStart : 0;
				var indextStart = Math.Max(terminal.SourcePosition.IndexStart, selectionStart);
				grammarRecognizedStart = Math.Min(grammarRecognizedStart, indextStart);
				var indexEnd = Math.Min(terminal.SourcePosition.IndexEnd + 1, selectionStart + selectionLength);
				grammarRecognizedEnd = Math.Max(grammarRecognizedEnd, indexEnd);

				if (IsCaseModificationSafe(terminal) || isSelectionWithinSingleTerminal)
				{
					AddModifiedCaseSegment(terminal.Token.Value, startOffset, indextStart, indexEnd - indextStart);
				}
			}

			if (grammarRecognizedStart > selectionStart)
			{
				AddModifiedCaseSegment(_executionContext.StatementText, selectionStart, selectionStart, grammarRecognizedStart - selectionStart);
			}

			if (grammarRecognizedEnd < selectionStart + selectionLength)
			{
				AddModifiedCaseSegment(_executionContext.StatementText, grammarRecognizedEnd, grammarRecognizedEnd, selectionStart + selectionLength - grammarRecognizedEnd);
			}
		}

		private void AddModifiedCaseSegment(string text, int textIndextStart, int globalIndextStart, int length)
		{
			var substring = text.Substring(textIndextStart, length);
			var modifiedCaseString = _changeCaseFunction(substring);
			if (substring == modifiedCaseString)
				return;

			_executionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = globalIndextStart,
					Length = length,
					Text = modifiedCaseString
				});
		}

		private static bool IsCaseModificationSafe(StatementGrammarNode terminal)
		{
			return terminal.Id != Terminals.StringLiteral &&
			       (!terminal.Id.IsIdentifierOrAlias() || !terminal.Token.Value.StartsWith("\""));
		}
	}
}
