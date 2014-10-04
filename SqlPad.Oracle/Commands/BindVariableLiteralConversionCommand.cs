using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class BindVariableLiteralConversionCommand : OracleCommandBase
	{
		private readonly BindVariableConfiguration _bindVariable;
		private readonly bool _allOccurences;

		public static ICollection<CommandExecutionHandler> ResolveCommandHandlers(OracleStatementSemanticModel semanticModel, StatementGrammarNode currentTerminal)
		{
			CheckParametersNotNull(semanticModel, currentTerminal);

			if (currentTerminal.Id != Terminals.BindVariableIdentifier)
				return EmptyHandlerCollection;

			var bindVariable = FindUsagesCommand.GetBindVariable(semanticModel, currentTerminal.Token.Value);

			var singleOccurenceConvertAction =
				new CommandExecutionHandler
				{
					Name = "Convert to literal",
					ExecutionHandler = c => new BindVariableLiteralConversionCommand(c, bindVariable, false)
						.Execute(),
					CanExecuteHandler = c => true
				};

			var commands = new List<CommandExecutionHandler> { singleOccurenceConvertAction };
			
			if (bindVariable.Nodes.Count > 1)
			{
				var allOccurencesConvertAction =
					new CommandExecutionHandler
					{
						Name = "Convert all accurences to literal",
						ExecutionHandler = c => new BindVariableLiteralConversionCommand(c, bindVariable, true)
							.Execute(),
						CanExecuteHandler = c => true
					};

				commands.Add(allOccurencesConvertAction);
			}

			return commands.AsReadOnly();
		}

		private BindVariableLiteralConversionCommand(CommandExecutionContext executionContext, BindVariableConfiguration bindVariable, bool allOccurences)
			: base(executionContext)
		{
			_bindVariable = bindVariable;
			_allOccurences = allOccurences;
		}

		protected override void Execute()
		{
			foreach (var node in _bindVariable.Nodes.Where(n => _allOccurences || n == CurrentNode))
			{
				var textSegment =
					new TextSegment
					{
						IndextStart = node.ParentNode.SourcePosition.IndexStart,
						Length = node.ParentNode.SourcePosition.Length
					};
				
				switch (_bindVariable.DataType)
				{
					case OracleBindVariable.DataTypeNumber:
						textSegment.Text = Convert.ToString(_bindVariable.Value);
						break;
					case OracleBindVariable.DataTypeDate:
						textSegment.Text = String.Format("DATE'{0}'", _bindVariable.Value);
						break;
					default:
						textSegment.Text = String.Format("'{0}'", _bindVariable.Value);
						break;
				}

				ExecutionContext.SegmentsToReplace.Add(textSegment);
			}
		}
	}
}
