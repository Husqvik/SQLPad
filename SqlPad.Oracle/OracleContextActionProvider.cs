using System.Collections.Generic;
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
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.SourcePosition.IndexStart <= cursorPosition - 1 && s.SourcePosition.IndexEnd >= cursorPosition - 1);
			if (statement == null)
				return EmptyCollection;

			var currentNode = statement.GetNodeAtPosition(cursorPosition);

			if (currentNode == null || currentNode.Type == NodeType.NonTerminal)
				return EmptyCollection;

			var semanticModel = new OracleStatementSemanticModel(statementText, statement, DatabaseModelFake.Instance);
			var actionList = new List<IContextAction>();

			if (currentNode.Id == Terminals.ObjectIdentifier)
			{
				var tables = semanticModel.QueryBlocks.SelectMany(b => b.TableReferences).Where(t => t.TableNode == currentNode).ToArray();
				if (tables.Length == 1 && tables[0].AliasNode == null)
				{
					actionList.Add(new OracleContextAction("Add Alias", new AddAliasCommand()));
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