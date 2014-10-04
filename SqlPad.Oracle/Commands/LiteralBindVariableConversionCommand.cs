using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class LiteralBindVariableConversionCommand : OracleCommandBase
	{
		private readonly ICollection<StatementGrammarNode> _literalTerminals;

		public static ICollection<CommandExecutionHandler> ResolveCommandHandlers(OracleStatementSemanticModel semanticModel, StatementGrammarNode currentTerminal)
		{
			CheckParametersNotNull(semanticModel, currentTerminal);

			var commands = new List<CommandExecutionHandler>();

			if (!CanConvertCurrentTerminal(currentTerminal))
			{
				return EmptyHandlerCollection;
			}

			var literalTerminals = FindUsagesCommand.GetEqualValueLiteralTerminals(semanticModel.Statement, currentTerminal).ToArray();

			var singleOccurenceConvertAction =
				new CommandExecutionHandler
				{
					Name = "Convert to bind variable",
					ExecutionHandler = c => new LiteralBindVariableConversionCommand(c, new[] { currentTerminal })
						.Execute(),
					CanExecuteHandler = c => true
				};

			commands.Add(singleOccurenceConvertAction);

			if (literalTerminals.Length > 1)
			{
				var allOccurencesConvertAction =
				new CommandExecutionHandler
				{
					Name = "Convert all occurences to bind variable",
					ExecutionHandler = c => new LiteralBindVariableConversionCommand(c, literalTerminals)
						.Execute(),
					CanExecuteHandler = c => true
				};

				commands.Add(allOccurencesConvertAction);
			}

			return commands.AsReadOnly();
		}

		private static bool CanConvertCurrentTerminal(StatementGrammarNode currentTerminal)
		{
			return currentTerminal.Id.In(Terminals.NumberLiteral, Terminals.StringLiteral) &&
			       currentTerminal.RootNode.Id.In(NonTerminals.SelectStatement, NonTerminals.UpdateStatement, NonTerminals.DeleteStatement, NonTerminals.InsertStatement);
		}

		private LiteralBindVariableConversionCommand(CommandExecutionContext executionContext, ICollection<StatementGrammarNode> literalTerminals)
			: base(executionContext)
		{
			_literalTerminals = literalTerminals;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var precedingTerminal = CurrentNode.PrecedingTerminal;
			var requiredPrecedingTerminalId = precedingTerminal != null && precedingTerminal.Id.In(Terminals.Date, Terminals.Timestamp)
				? precedingTerminal.Id
				: null;

			foreach (var node in _literalTerminals.Where(t => requiredPrecedingTerminalId == null || requiredPrecedingTerminalId == t.PrecedingTerminal.Id))
			{
				var indexStart = requiredPrecedingTerminalId == null
					? node.SourcePosition.IndexStart
					: node.PrecedingTerminal.SourcePosition.IndexStart;

				var replaceLength = requiredPrecedingTerminalId == null
					? node.SourcePosition.Length
					: node.SourcePosition.IndexEnd - node.PrecedingTerminal.SourcePosition.IndexStart + 1;

				var textSegment =
					new TextSegment
					{
						Text = String.Format(":{0}", settingsModel.Value),
						IndextStart = indexStart,
						Length = replaceLength
					};

				ExecutionContext.SegmentsToReplace.Add(textSegment);
			}
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;
			settingsModel.ValidationRule = new OracleBindVariableIdentifierValidationRule();

			settingsModel.Title = "Bind variable";
			settingsModel.Heading = settingsModel.Title;
			settingsModel.Description = "Enter bind variable name";

			return settingsModel;
		}
	}
}
