using System;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleMultiNodeEditorDataProvider : IMultiNodeEditorDataProvider
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();

		public MultiNodeEditorData GetMultiNodeEditorData(string sqlText, int position, int selectionStart, int selectionLength)
		{
			var statements = _parser.Parse(sqlText);
			var currentNode = statements.GetTerminalAtPosition(position, n => n.Id.In("Alias", "ObjectIdentifier", "SchemaIdentifier", "Identifier"));

			// TODO: Handle by provider, check if selection span the terminal
			var synchronizedNodes = currentNode.Statement.AllTerminals
				.Where(t => t != currentNode && String.Equals(t.Token.Value, currentNode.Token.Value, StringComparison.InvariantCultureIgnoreCase))
				.OrderByDescending(t => t.SourcePosition.IndexStart);

			var offsetFromNodeStartIndex = Math.Min(selectionStart, position) - currentNode.SourcePosition.IndexStart;
			var characters = selectionLength == 0 ? 1 : selectionLength;

			return new MultiNodeEditorData
			       {
				       Characters = characters,
				       OffsetFromNodeStartIndex = offsetFromNodeStartIndex,
				       CurrentNode = currentNode,
				       SynchronizedNodes = synchronizedNodes
			       };
		}
	}
}