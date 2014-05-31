using System;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class ToggleQuotedNotationCommand : OracleCommandBase
	{
		public const string Title = "Toggle quoted notation";

		public static CommandExecutionHandler ExecutionHandler = new CommandExecutionHandler
		{
			Name = "ToggleQuotedNotation",
			ExecutionHandler = ExecutionHandlerImplementation,
			CanExecuteHandler = CanExecuteHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			new ToggleQuotedNotationCommand((OracleCommandExecutionContext)executionContext).Execute();
		}

		private static bool CanExecuteHandlerImplementation(CommandExecutionContext executionContext)
		{
			return new ToggleQuotedNotationCommand((OracleCommandExecutionContext)executionContext).CanExecute();
		}

		private ToggleQuotedNotationCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		private bool CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null && CurrentNode.Id == Terminals.Select;
		}

		private void Execute()
		{
			bool? enableQuotes = null;
			foreach (var identifier in CurrentQueryBlock.RootNode.Terminals.Where(t => (OracleGrammarDescription.Identifiers.Contains(t.Id) || t.Id == Terminals.ColumnAlias || t.Id == Terminals.ObjectAlias) && !t.Token.Value.CollidesWithKeyword()))
			{
				if (!enableQuotes.HasValue)
				{
					enableQuotes = !identifier.Token.Value.IsQuoted();
				}

				if ((enableQuotes.Value && identifier.Token.Value.IsQuoted()) ||
				    !enableQuotes.Value && !identifier.Token.Value.IsQuoted())
					continue;

				var replacedLength = enableQuotes.Value ? 0 : 1;
				var newText = enableQuotes.Value ? "\"" : String.Empty;

				ExecutionContext.SegmentsToReplace.Add(new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexStart,
					Length = replacedLength,
					Text = newText
				});

				ExecutionContext.SegmentsToReplace.Add(new TextSegment
				{
					IndextStart = identifier.SourcePosition.IndexEnd + 1 - replacedLength,
					Length = replacedLength,
					Text = newText
				});
			}
		}
	}
}
