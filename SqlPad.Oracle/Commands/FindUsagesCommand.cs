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
				case Terminals.Identifier:
					nodes = GetColumnReferenceUsage();
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
			var columnReferencedObject = _queryBlock.AllColumnReferences
				.SingleOrDefault(c => c.ObjectNode == _currentNode && c.ObjectNodeObjectReferences.Count == 1);

			var referencedObject = _queryBlock.ObjectReferences.SingleOrDefault(t => t.ObjectNode == _currentNode || t.AliasNode == _currentNode);
			var objectReference = columnReferencedObject != null
				? columnReferencedObject.ObjectNodeObjectReferences.Single()
				: referencedObject;

			var objectReferenceNodes = Enumerable.Repeat(objectReference.ObjectNode, 1);
			if (objectReference.AliasNode != null)
			{
				objectReferenceNodes = objectReferenceNodes.Concat(Enumerable.Repeat(objectReference.AliasNode, 1));
			}

			return _queryBlock.AllColumnReferences.Where(c => c.ObjectNode != null && c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.Single() == objectReference)
				.Select(c => c.ObjectNode)
				.Concat(objectReferenceNodes);
		}

		private IEnumerable<StatementDescriptionNode> GetColumnReferenceUsage()
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			var columnReference = _queryBlock.AllColumnReferences
				.SingleOrDefault(c => c.ColumnNode == _currentNode && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeColumnReferences == 1);
			
			if (columnReference == null)
				return nodes;

			var objectReference = columnReference.ColumnNodeObjectReferences.Single();
			nodes = _queryBlock.AllColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == objectReference && c.NormalizedName == columnReference.NormalizedName)
				.Select(c => c.ColumnNode);

			if (objectReference.QueryBlocks.Count != 1 || !columnReference.SelectListColumn.IsDirectColumnReference)
				return nodes;

			nodes = nodes.Concat(GetChildQueryBlockColumnReferences(objectReference.QueryBlocks.Single(), columnReference));

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetChildQueryBlockColumnReferences(OracleQueryBlock queryBlock, OracleColumnReference columnReference)
		{
			return queryBlock.Columns.SelectMany(c => c.ColumnReferences)
				.Where(c => c.SelectListColumn.IsDirectColumnReference && c.ColumnNodeObjectReferences.Count == 1 && c.SelectListColumn.NormalizedName == columnReference.NormalizedName)
				.Select(c => c.SelectListColumn.AliasNode ?? c.ColumnNode);
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
