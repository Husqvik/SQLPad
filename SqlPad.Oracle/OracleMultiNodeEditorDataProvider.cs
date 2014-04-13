using System;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleMultiNodeEditorDataProvider : IMultiNodeEditorDataProvider
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();

		public MultiNodeEditorData GetMultiNodeEditorData(string sqlText, int position, int selectionStart, int selectionLength)
		{
			var statements = _parser.Parse(sqlText);
			var currentNode = statements.GetTerminalAtPosition(position, n => Terminals.AllTerminals.Contains(n.Id));
			var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)currentNode.Statement, DatabaseModelFake.Instance);

			// TODO: check if selection span the terminal
			var synchronizedNodes = currentNode.Statement.AllTerminals
				.Where(t => t != currentNode && String.Equals(t.Token.Value, currentNode.Token.Value, StringComparison.InvariantCultureIgnoreCase))
				.OrderByDescending(t => t.SourcePosition.IndexStart);

			var offsetFromNodeStartIndex = Math.Min(selectionStart, position) - currentNode.SourcePosition.IndexStart;

			return new MultiNodeEditorData
			       {
				       OffsetFromNodeStartIndex = offsetFromNodeStartIndex,
				       CurrentNode = currentNode,
				       SynchronizedNodes = synchronizedNodes
			       };
		}
	}
}