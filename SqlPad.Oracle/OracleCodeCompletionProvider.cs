using System.Collections.Generic;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();
		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];

		public ICollection<ICodeCompletionItem> ResolveItems(string statementText, int cursorPosition)
		{
			var statements = _oracleParser.Parse(statementText);
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			if (statement == null)
				return EmptyCollection;

			var currentNode = statement.GetNodeAtPosition(cursorPosition);
			var semanticModel = new OracleStatementSemanticModel(statementText, statement, DatabaseModelFake.Instance);

			if (currentNode.Id == Terminals.Identifier)
			{
				var selectList = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList);
				if (selectList != null)
				{
					//currentNode
				}
			}

			return EmptyCollection;
		}
	}
}