using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	public class FindUsagesCommand : DisplayCommandBase
	{
		private readonly StatementDescriptionNode _currentNode;
		private readonly OracleStatementSemanticModel _semanticModel;
		private readonly OracleQueryBlock _queryBlock;

		public FindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel)
		{
			_currentNode = new OracleSqlParser().Parse(statementText).GetTerminalAtPosition(currentPosition, t => t.Id.IsIdentifierOrAlias());
			if (_currentNode == null)
				return;
			
			_semanticModel = new OracleStatementSemanticModel(statementText, (OracleStatement)_currentNode.Statement, (DatabaseModelFake)databaseModel);
			_queryBlock = _semanticModel.GetQueryBlock(_currentNode);
		}

		public override bool CanExecute(object parameter)
		{
			return _currentNode != null;
		}

		protected override void ExecuteInternal(ICollection<TextSegment> segments)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();

			switch (_currentNode.Id)
			{
				case Terminals.ObjectAlias:
				case Terminals.ObjectIdentifier:
					nodes = GetTableReferenceUsage();
					break;
				case Terminals.SchemaIdentifier:
					nodes = GetSchemaReferenceUsage();
					break;
			}

			foreach (var node in nodes)
			{
				segments.Add(new TextSegment
				                      {
					                      IndextStart = node.SourcePosition.IndexStart,
										  Length = node.SourcePosition.Length
				                      });
			}
		}

		private IEnumerable<StatementDescriptionNode> GetTableReferenceUsage()
		{
			var columnReferencedObject = _queryBlock.Columns.SelectMany(c => c.ColumnReferences).Concat(_queryBlock.ColumnReferences)
				.SingleOrDefault(c => c.ObjectNode == _currentNode && c.ObjectNodeReferences.Count == 1);

			var referencedObject = _queryBlock.ObjectReferences.SingleOrDefault(t => t.ObjectNode == _currentNode || t.AliasNode == _currentNode);
			var objectReference = columnReferencedObject != null
				? columnReferencedObject.ObjectNodeReferences.Single()
				: referencedObject;

			var objectReferenceNodes = Enumerable.Repeat(objectReference.ObjectNode, 1);
			if (objectReference.AliasNode != null)
			{
				objectReferenceNodes = objectReferenceNodes.Concat(Enumerable.Repeat(objectReference.AliasNode, 1));
			}

			return _queryBlock.Columns.SelectMany(c => c.ColumnReferences).Concat(_queryBlock.ColumnReferences).Where(c => c.ObjectNode != null && c.ObjectNodeReferences.Count == 1 && c.ObjectNodeReferences.Single() == objectReference).Select(c => c.ObjectNode)
				.Concat(objectReferenceNodes);
		}

		private IEnumerable<StatementDescriptionNode> GetColumnReferenceUsage()
		{
			return null;
		}

		private IEnumerable<StatementDescriptionNode> GetColumnAliasUsage()
		{
			return null;
		}

		private IEnumerable<StatementDescriptionNode> GetSchemaReferenceUsage()
		{
			return _currentNode.Statement.AllTerminals.Where(t => t.Id == Terminals.SchemaIdentifier && t.Token.Value.ToQuotedIdentifier() == _currentNode.Token.Value.ToQuotedIdentifier());
		}
	}
}
