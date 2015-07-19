using System;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleMultiNodeEditorDataProvider : IMultiNodeEditorDataProvider
	{
		public MultiNodeEditorData GetMultiNodeEditorData(IDatabaseModel databaseModel, string sqlText, int currentPosition, int selectionStart, int selectionLength)
		{
			var statements = OracleSqlParser.Instance.Parse(sqlText);
			var currentNode = statements.GetTerminalAtPosition(currentPosition, n => n.Id.IsIdentifierOrAlias());
			if (currentNode == null)
				return new MultiNodeEditorData();

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