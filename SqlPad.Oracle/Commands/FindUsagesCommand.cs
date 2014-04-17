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
			_currentNode = new OracleSqlParser().Parse(statementText).GetTerminalAtPosition(currentPosition);
			if (_currentNode == null)
				return;
			
			_semanticModel = new OracleStatementSemanticModel(statementText, (OracleStatement)_currentNode.Statement, (DatabaseModelFake)databaseModel);
			_queryBlock = _semanticModel.GetQueryBlock(_currentNode);
		}

		public override bool CanExecute(object parameter)
		{
			return _currentNode != null && (Terminals.IsIdentifier(_currentNode.Id) || _currentNode.Id == Terminals.Alias || _currentNode.Id == Terminals.ObjectAlias);
		}

		protected override void ExecuteInternal(ICollection<TextSegment> segments)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (_currentNode.Id == Terminals.ObjectIdentifier || _currentNode.Id == Terminals.ObjectAlias)
			{
				nodes = GetTableReferenceUsage();
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
			return null;
		}
	}
}
