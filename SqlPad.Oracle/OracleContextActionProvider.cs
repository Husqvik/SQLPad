﻿using System.Collections.Generic;
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

			var currentTerminal = statements.GetTerminalAtPosition(cursorPosition - 1);
			if (currentTerminal == null)
				return EmptyCollection;

			var semanticModel = new OracleStatementSemanticModel(statementText, (OracleStatement)currentTerminal.Statement, DatabaseModelFake.Instance);
			var actionList = new List<IContextAction>();

			if (currentTerminal.Id == Terminals.ObjectIdentifier)
			{
				var tables = semanticModel.QueryBlocks.SelectMany(b => b.TableReferences).Where(t => t.TableNode == currentTerminal).ToArray();
				if (tables.Length == 1 && tables[0].AliasNode == null)
				{
					actionList.Add(new OracleContextAction("Add Alias", new AddAliasCommand(semanticModel)));
				}
			}

			return actionList.AsReadOnly();
		}
	}

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