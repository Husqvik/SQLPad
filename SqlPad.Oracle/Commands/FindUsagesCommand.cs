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
			
			_semanticModel = new OracleStatementSemanticModel(statementText, (OracleStatement)_currentNode.Statement, (OracleDatabaseModel)databaseModel);
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
				case Terminals.ColumnAlias:
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
				.SingleOrDefault(c => (c.ColumnNode == _currentNode || (c.SelectListColumn != null && c.SelectListColumn.AliasNode == _currentNode)) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeColumnReferences == 1);
			
			if (columnReference == null)
				return nodes;

			var objectReference = columnReference.ColumnNodeObjectReferences.Single();
			if (_currentNode.Id == Terminals.Identifier)
			{
				var columnReferences = _queryBlock.AllColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == objectReference && c.NormalizedName == columnReference.NormalizedName).ToArray();
				if (columnReference.SelectListColumn == null)
				{
					var selectionListColumnReference = columnReferences.FirstOrDefault(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectColumnReference);
					if (selectionListColumnReference != null)
					{
						columnReference = selectionListColumnReference;
					}
				}

				nodes = columnReferences.Select(c => c.ColumnNode);
			}
			else
			{
				nodes = _queryBlock.Columns.Where(c => c.AliasNode == _currentNode).Select(c => c.AliasNode);
			}

			nodes = nodes.Concat(GetParentQueryBlockReferences(columnReference));

			nodes = nodes.Concat(GetChildQueryBlockColumnReferences(objectReference, columnReference));

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetChildQueryBlockColumnReferences(OracleObjectReference objectReference, OracleColumnReference columnReference)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (objectReference.QueryBlocks.Count != 1/* || !columnReference.SelectListColumn.IsDirectColumnReference*/)
				return nodes;

			var childQueryBlock = objectReference.QueryBlocks.Single();
			var childColumnReferences = childQueryBlock.Columns.SelectMany(c => c.ColumnReferences)
				.Where(c => c.SelectListColumn.NormalizedName == columnReference.NormalizedName)
				.ToArray();

			var childSubqueryColumnReference = childColumnReferences.FirstOrDefault(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectColumnReference && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single().QueryBlocks.Count == 1);
			nodes = childColumnReferences.Select(c => c.SelectListColumn.AliasNode ?? c.ColumnNode); 

			var childColumnReference = childColumnReferences.FirstOrDefault(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectColumnReference && c.ColumnNodeObjectReferences.Count == 1);
			if (childColumnReference != null)
			{
				nodes = nodes.Concat(childQueryBlock.ColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == childColumnReference.ColumnNodeObjectReferences.Single() && c.NormalizedName == childColumnReference.NormalizedName).Select(c => c.ColumnNode));
			}

			if (childSubqueryColumnReference != null)
			{
				nodes = nodes.Concat(GetChildQueryBlockColumnReferences(childSubqueryColumnReference.ColumnNodeObjectReferences.Single(), childSubqueryColumnReference));
			}

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetParentQueryBlockReferences(OracleColumnReference columnReference)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (columnReference.SelectListColumn == null || columnReference.SelectListColumn.AliasNode == null || !columnReference.SelectListColumn.IsDirectColumnReference)
				return nodes;

			var parentQueryBlocks = _semanticModel.QueryBlocks.Where(qb => qb.ObjectReferences.SelectMany(o => o.QueryBlocks).Contains(columnReference.Owner));
			foreach (var parentQueryBlock in parentQueryBlocks)
			{
				var parentReferences = parentQueryBlock.AllColumnReferences
					.Where(c => c.ColumnNodeColumnReferences == 1 && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single().QueryBlocks.Count == 1
								&& c.ColumnNodeObjectReferences.Single().QueryBlocks.Single() == columnReference.Owner && c.NormalizedName == columnReference.SelectListColumn.NormalizedName)
					.ToArray();

				if (parentReferences.Length == 0)
					continue;

				nodes = nodes.Concat(parentReferences.Select(c => c.ColumnNode));

				var parentColumnReferences = parentReferences.Where(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectColumnReference).ToArray();

				if (parentColumnReferences.Length == 1)
				{
					nodes = nodes.Concat(GetParentQueryBlockReferences(parentColumnReferences[0]));
				}
			}

			return nodes;
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
