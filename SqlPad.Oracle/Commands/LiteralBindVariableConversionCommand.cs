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
		private readonly IReadOnlyList<StatementGrammarNode> _literalTerminals;
		private readonly string _requiredPrecedingTerminalId;

		public static ICollection<CommandExecutionHandler> ResolveCommandHandlers(OracleStatementSemanticModel semanticModel, StatementGrammarNode currentTerminal)
		{
			CheckParametersNotNull(semanticModel, currentTerminal);

			var commands = new List<CommandExecutionHandler>();

			if (!CanConvertCurrentTerminal(currentTerminal))
			{
				return EmptyHandlerCollection;
			}

			var requiredPrecedingTerminalId = GetRequiredPrecedingTerminalId(currentTerminal);
			var literalTerminals = FindUsagesCommand.GetEqualValueLiteralTerminals(semanticModel.Statement, currentTerminal)
				.Where(t => requiredPrecedingTerminalId == t.PrecedingTerminal.Id || (requiredPrecedingTerminalId == null && !t.PrecedingTerminal.Id.In(Terminals.Date, Terminals.Timestamp)))
				.ToArray();

			var singleOccurenceConvertAction =
				new CommandExecutionHandler
				{
					Name = "Convert to bind variable",
					ExecutionHandler = c => new LiteralBindVariableConversionCommand(c, new[] { currentTerminal }, requiredPrecedingTerminalId)
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
						ExecutionHandler = c => new LiteralBindVariableConversionCommand(c, literalTerminals, requiredPrecedingTerminalId)
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
			       currentTerminal.RootNode[0, 0].Id.In(NonTerminals.SelectStatement, NonTerminals.UpdateStatement, NonTerminals.DeleteStatement, NonTerminals.InsertStatement, NonTerminals.MergeStatement) ||
				   currentTerminal.RootNode.Id == NonTerminals.PlSqlBlockStatement;
		}

		private LiteralBindVariableConversionCommand(CommandExecutionContext executionContext, IReadOnlyList<StatementGrammarNode> literalTerminals, string requiredPrecedingTerminalId)
			: base(executionContext)
		{
			_literalTerminals = literalTerminals;
			_requiredPrecedingTerminalId = requiredPrecedingTerminalId;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			foreach (var node in _literalTerminals)
			{
				var indexStart = _requiredPrecedingTerminalId == null
					? node.SourcePosition.IndexStart
					: node.PrecedingTerminal.SourcePosition.IndexStart;

				var replaceLength = _requiredPrecedingTerminalId == null
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

			var bindVariableName = settingsModel.Value.ToNormalizedBindVariableIdentifier();
			CreateBindVariable(bindVariableName);
		}

		private void CreateBindVariable(string bindVariableName)
		{
			var bindVariable = new BindVariableConfiguration { Name = bindVariableName };
			var literalTerminal = _literalTerminals[0];
			
			switch (literalTerminal.Id)
			{
				case Terminals.NumberLiteral:
					bindVariable.Value = literalTerminal.Token.Value;
					break;
				case Terminals.StringLiteral:
					bindVariable.Value = literalTerminal.Token.Value.ToPlainString();
					break;
			}

			switch (_requiredPrecedingTerminalId)
			{
				case Terminals.Date:
					bindVariable.DataType = OracleBindVariable.DataTypeDate;
					break;
				case Terminals.Timestamp:
					bindVariable.DataType = OracleBindVariable.DataTypeTimestamp;
					break;
				case null:
					if (literalTerminal.Id == Terminals.NumberLiteral)
					{
						bindVariable.DataType = OracleBindVariable.DataTypeNumber;
					}
					else
					{
						bindVariable.DataType = literalTerminal.Token.Value[0].In('n', 'N')
							? OracleBindVariable.DataTypeUnicodeVarchar2
							: OracleBindVariable.DataTypeVarchar2;
					}

					break;
			}

			var configuration = WorkDocumentCollection.GetProviderConfiguration(SemanticModel.DatabaseModel.ConnectionString.ProviderName);
			configuration.SetBindVariable(bindVariable);
		}

		private static string GetRequiredPrecedingTerminalId(StatementGrammarNode terminal)
		{
			var precedingTerminal = terminal.PrecedingTerminal;
			return precedingTerminal != null && precedingTerminal.Id.In(Terminals.Date, Terminals.Timestamp)
				? precedingTerminal.Id
				: null;
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
