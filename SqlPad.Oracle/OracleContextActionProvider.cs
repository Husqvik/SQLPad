using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SqlPad.Oracle.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleContextActionProvider : IContextActionProvider
	{
		private static readonly IContextAction[] EmptyCollection = new IContextAction[0];
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();

		public ICollection<IContextAction> GetContextActions(string statementText, int cursorPosition)
		{
			var statements = _oracleParser.Parse(statementText);

			var currentTerminal = statements.GetTerminalAtPosition(cursorPosition, n => Terminals.AllTerminals.Contains(n.Id));
			if (currentTerminal == null)
				return EmptyCollection;

			var semanticModel = new OracleStatementSemanticModel(statementText, (OracleStatement)currentTerminal.Statement, DatabaseModelFake.Instance);
			var actionList = new List<IContextAction>();

			var addAliasCommand = new AddAliasCommand(semanticModel, currentTerminal);
			if (addAliasCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction("Add Alias", addAliasCommand));
			}

			var wrapAsSubqueryCommand = new WrapAsSubqueryCommand(semanticModel, currentTerminal);
			if (wrapAsSubqueryCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction("Wrap as sub-query", wrapAsSubqueryCommand));
			}

			/*var wrapAsCommonTableExpressionCommand = new WrapAsCommonTableExpressionCommand(semanticModel, currentTerminal);
			if (wrapAsCommonTableExpressionCommand.CanExecute(null))
			{
				actionList.Add(new OracleContextAction("Wrap as common table expression", wrapAsCommonTableExpressionCommand));
			}*/

			var actions = ResolveAmbiguousColumnCommand.ResolveCommands(semanticModel, currentTerminal)
				.Select(c => new OracleContextAction("Resolve as " + c.ResolvedName, c));

			actionList.AddRange(actions);

			// TODO: Resolve command order
			return actionList.AsReadOnly();
		}
	}

	[DebuggerDisplay("OracleContextAction (Name={Name})")]
	public class OracleContextAction : IContextAction
	{
		public OracleContextAction(string name, ICommand command)
		{
			Name = name;
			Command = command;
		}

		public string Name { get; private set; }

		public ICommand Command { get; private set; }
	}
}