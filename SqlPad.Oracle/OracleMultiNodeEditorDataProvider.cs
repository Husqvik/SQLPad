using System;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleMultiNodeEditorDataProvider : IMultiNodeEditorDataProvider
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();

		public MultiNodeEditorData GetMultiNodeEditorData(IDatabaseModel databaseModel, string sqlText, int currentPosition, int selectionStart, int selectionLength)
		{
			var statements = _parser.Parse(sqlText);
			var currentNode = statements.GetTerminalAtPosition(currentPosition, n => n.Id.IsIdentifierOrAlias());
			if (currentNode == null)
				return new MultiNodeEditorData();

			//var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)currentNode.Statement, (OracleDatabaseModel)databaseModel);

			// TODO: check if selection span the terminal
			var synchronizedNodes = currentNode.Statement.AllTerminals
				.Where(t => t != currentNode && String.Equals(t.Token.Value, currentNode.Token.Value, StringComparison.InvariantCultureIgnoreCase))
				.OrderByDescending(t => t.SourcePosition.IndexStart);

			var offsetFromNodeStartIndex = Math.Min(selectionStart, currentPosition) - currentNode.SourcePosition.IndexStart;

			return new MultiNodeEditorData
			       {
				       OffsetFromNodeStartIndex = offsetFromNodeStartIndex,
				       CurrentNode = currentNode,
				       SynchronizedNodes = synchronizedNodes
			       };
		}
	}
}