using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class FindUsagesCommand
	{
		private readonly StatementDescriptionNode _currentNode;
		private readonly OracleStatementSemanticModel _semanticModel;
		private readonly OracleQueryBlock _queryBlock;
		private readonly CommandExecutionContext _executionContext;

		public static readonly CommandExecutionHandler FindUsages = new CommandExecutionHandler
		{
			Name = "FindUsages",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.F11, ModifierKeys.Alt | ModifierKeys.Shift) },
			ExecutionHandler = ExecutionHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			var commandInstance = new FindUsagesCommand(executionContext);
			if (commandInstance.CanExecute())
			{
				commandInstance.ExecuteFindUsages();
			}
		}

		private FindUsagesCommand(CommandExecutionContext executionContext)
		{
			_currentNode = GetFindUsagesCompatibleTerminal(executionContext.Statements, executionContext.CaretOffset);
			if (_currentNode == null)
				return;

			_semanticModel = new OracleStatementSemanticModel(executionContext.StatementText, (OracleStatement)_currentNode.Statement, (OracleDatabaseModelBase)executionContext.DatabaseModel);
			_queryBlock = _semanticModel.GetQueryBlock(_currentNode);
			_executionContext = executionContext;
		}

		private static StatementDescriptionNode GetFindUsagesCompatibleTerminal(StatementCollection statements, int currentPosition)
		{
			return statements.GetTerminalAtPosition(currentPosition,
				t => t.Id.In(Terminals.Identifier, Terminals.ObjectIdentifier, Terminals.SchemaIdentifier, Terminals.BindVariableIdentifier, Terminals.ColumnAlias, Terminals.ObjectAlias, Terminals.Count) ||
				     t.Id.IsLiteral() || t.ParentNode.Id.In(NonTerminals.AnalyticFunction, NonTerminals.AggregateFunction));
		}

		private bool CanExecute()
		{
			return _currentNode != null;
		}

		private void ExecuteFindUsages()
		{
			IEnumerable<StatementDescriptionNode> nodes;

			switch (_currentNode.Id)
			{
				case Terminals.IntegerLiteral:
				case Terminals.StringLiteral:
				case Terminals.NumberLiteral:
					nodes = GetLiteralUsage();
					break;
				case Terminals.BindVariableIdentifier:
					nodes = GetBindVariableUsage();
					break;
				case Terminals.ObjectAlias:
				case Terminals.ObjectIdentifier:
					nodes = GetObjectReferences().SelectMany(GetObjectReferenceUsage);
					break;
				case Terminals.SchemaIdentifier:
					nodes = GetSchemaReferenceUsage();
					break;
				case Terminals.Min:
				case Terminals.Max:
				case Terminals.Sum:
				case Terminals.Avg:
				case Terminals.FirstValue:
				case Terminals.Count:
				case Terminals.Variance:
				case Terminals.StandardDeviation:
				case Terminals.LastValue:
				case Terminals.Lead:
				case Terminals.Lag:
				case Terminals.Identifier:
					var functionUsages = GetFunctionReferenceUsage();
					if (functionUsages.Count > 0)
					{
						nodes = functionUsages;
						break;
					}

					goto case Terminals.ColumnAlias;
				case Terminals.ColumnAlias:
					nodes = GetColumnReferenceUsage();
					break;
				default:
					throw new NotSupportedException(String.Format("Terminal '{0}' is not supported. ", _currentNode.Id));
			}

			_executionContext.SegmentsToReplace.AddRange(
				nodes.Select(n =>
					new TextSegment
					{
						IndextStart = n.SourcePosition.IndexStart,
						Length = n.SourcePosition.Length,
						DisplayOptions = n.Id.IsAlias() ? DisplayOptions.Definition : DisplayOptions.Usage
					}));
		}

		private IEnumerable<StatementDescriptionNode> GetBindVariableUsage()
		{
			return _semanticModel.Statement.RootNode.Terminals.Where(t => t.Id == _currentNode.Id && t.Token.Value.ToQuotedIdentifier() == _currentNode.Token.Value.ToQuotedIdentifier());
		}

		private IEnumerable<StatementDescriptionNode> GetLiteralUsage()
		{
			return _semanticModel.Statement.RootNode.Terminals.Where(t => t.Id == _currentNode.Id && t.Token.Value == _currentNode.Token.Value);
		}

		private ICollection<StatementDescriptionNode> GetFunctionReferenceUsage()
		{
			var functionReference = _queryBlock.AllFunctionReferences
				.FirstOrDefault(f => f.FunctionIdentifierNode == _currentNode && f.Metadata != null);

			if (functionReference == null)
				return new StatementDescriptionNode[0];

			return _semanticModel.QueryBlocks
				.SelectMany(qb => qb.AllFunctionReferences)
				.Where(f => f.Metadata == functionReference.Metadata)
				.Select(f => f.FunctionIdentifierNode)
				.ToArray();
		}

		private IEnumerable<OracleDataObjectReference> GetObjectReferences()
		{
			if (_queryBlock == null && _currentNode.Id == Terminals.ObjectAlias)
			{
				var cteQueryBlock = _semanticModel.QueryBlocks.SingleOrDefault(qb => qb.AliasNode == _currentNode);
				return _semanticModel.QueryBlocks.SelectMany(qb => qb.ObjectReferences.Where(o => o.QueryBlocks.Count == 1 && o.QueryBlocks.Contains(cteQueryBlock)));
			}

			var columnReferencedObject = _queryBlock.AllColumnReferences
				.SingleOrDefault(c => c.ObjectNode == _currentNode && c.ObjectNodeObjectReferences.Count == 1);

			var referencedObject = _queryBlock.ObjectReferences.SingleOrDefault(t => t.ObjectNode == _currentNode || t.AliasNode == _currentNode);
			var objectReference = columnReferencedObject != null
				? columnReferencedObject.ObjectNodeObjectReferences.Single()
				: referencedObject;

			return Enumerable.Repeat(objectReference, 1);
		}

		private IEnumerable<StatementDescriptionNode> GetObjectReferenceUsage(OracleDataObjectReference objectReference)
		{
			var nodes = new List<StatementDescriptionNode>();
			if (objectReference.Type != TableReferenceType.InlineView)
			{
				nodes.Add(objectReference.ObjectNode);
			}
			
			if (objectReference.AliasNode != null)
			{
				nodes.Add(objectReference.AliasNode);
			}

			if (objectReference.QueryBlocks.Count == 1 && objectReference.Type == TableReferenceType.CommonTableExpression)
			{
				nodes.Add(objectReference.QueryBlocks.Single().AliasNode);
			}

			return objectReference.Owner.AllColumnReferences.Where(c => c.ObjectNode != null && c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.Single() == objectReference)
				.Select(c => c.ObjectNode)
				.Concat(nodes);
		}

		private IEnumerable<StatementDescriptionNode> GetColumnReferenceUsage()
		{
			IEnumerable<StatementDescriptionNode> nodes;
			var columnReference = _queryBlock.AllColumnReferences
				.FirstOrDefault(c => (c.ColumnNode == _currentNode || (c.SelectListColumn != null && c.SelectListColumn.AliasNode == _currentNode)) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeColumnReferences.Count == 1);

			OracleSelectListColumn selectListColumn;
			if (columnReference != null)
			{
				var objectReference = columnReference.ColumnNodeObjectReferences.Single();
				var columnReferences = _queryBlock.AllColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == objectReference && c.NormalizedName == columnReference.NormalizedName).ToArray();
				nodes = columnReferences.Select(c => c.ColumnNode);

				bool searchChildren;
				if (_currentNode.Id == Terminals.Identifier)
				{
					searchChildren = true;

					selectListColumn = columnReference.SelectListColumn;
					if (selectListColumn == null)
					{
						var selectListColumnReference = columnReferences.FirstOrDefault(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectReference);
						if (selectListColumnReference != null && selectListColumnReference.SelectListColumn.AliasNode != selectListColumnReference.ColumnNode)
						{
							selectListColumn = selectListColumnReference.SelectListColumn;
						}
					}
					else if (!selectListColumn.IsDirectReference)
					{
						selectListColumn = null;
					}

					if (selectListColumn != null && selectListColumn.AliasNode != _currentNode)
					{
						nodes = nodes.Concat(new[] { selectListColumn.AliasNode });
					}
				}
				else
				{
					selectListColumn = _queryBlock.Columns.Single(c => c.AliasNode == _currentNode);
					var nodeList = new List<StatementDescriptionNode> { selectListColumn.AliasNode };
					searchChildren = selectListColumn.IsDirectReference;

					nodes = searchChildren ? nodes.Concat(nodeList) : nodeList;
				}

				if (searchChildren)
					nodes = nodes.Concat(GetChildQueryBlockColumnReferences(objectReference, columnReference));
			}
			else
			{
				nodes = new[] { _currentNode };
				selectListColumn = _queryBlock.Columns.SingleOrDefault(c => c.AliasNode == _currentNode);
			}

			nodes = nodes.Concat(GetParentQueryBlockReferences(selectListColumn));

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetChildQueryBlockColumnReferences(OracleDataObjectReference objectReference, OracleColumnReference columnReference)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (objectReference.QueryBlocks.Count != 1)
				return nodes;

			var childQueryBlock = objectReference.QueryBlocks.Single();
			var childColumn = childQueryBlock.Columns
				.SingleOrDefault(c => c.NormalizedName == columnReference.NormalizedName);
			
			if (childColumn == null)
				return nodes;

			nodes = new List<StatementDescriptionNode>{ childColumn.AliasNode };

			if (childColumn.IsDirectReference && childColumn.ColumnReferences.All(cr => cr.ColumnNodeColumnReferences.Count == 1))
			{
				var childSelectColumnReferences = childQueryBlock.Columns.SelectMany(c => c.ColumnReferences)
					.Where(c => !c.ReferencesAllColumns && c.ColumnNodeObjectReferences.Count == 1 && c.SelectListColumn.NormalizedName == columnReference.NormalizedName && c.ColumnNode != childColumn.AliasNode)
					.Select(c => c.ColumnNode);

				nodes = nodes.Concat(childSelectColumnReferences);

				var childColumnReference = childColumn.ColumnReferences.Single();
				nodes = nodes.Concat(childQueryBlock.ColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == childColumnReference.ColumnNodeObjectReferences.Single() && c.NormalizedName == childColumnReference.NormalizedName).Select(c => c.ColumnNode));

				if (childColumnReference.ColumnNodeObjectReferences.Count == 1)
				{
					nodes = nodes.Concat(GetChildQueryBlockColumnReferences(childColumnReference.ColumnNodeObjectReferences.Single(), childColumnReference));
				}
			}

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetParentQueryBlockReferences(OracleSelectListColumn selectListColumn)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (selectListColumn == null || selectListColumn.AliasNode == null)
				return nodes;

			var parentQueryBlocks = _semanticModel.QueryBlocks.Where(qb => qb.ObjectReferences.SelectMany(o => o.QueryBlocks).Contains(selectListColumn.Owner));
			foreach (var parentQueryBlock in parentQueryBlocks)
			{
				var parentReferences = parentQueryBlock.AllColumnReferences
					.Where(c => c.ColumnNodeColumnReferences.Count == 1 && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single().QueryBlocks.Count == 1
								&& c.ColumnNodeObjectReferences.Single().QueryBlocks.Single() == selectListColumn.Owner && c.NormalizedName == selectListColumn.NormalizedName)
					.ToArray();

				if (parentReferences.Length == 0)
					continue;

				nodes = parentReferences.Select(c => c.ColumnNode);

				var parentColumnReferences = parentReferences.Where(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectReference).ToArray();

				if (parentColumnReferences.Length == 1)
				{
					nodes = nodes
						.Concat(parentColumnReferences.Where(c => c.ColumnNode != c.SelectListColumn.AliasNode).Select(c => c.SelectListColumn.AliasNode))
						.Concat(GetParentQueryBlockReferences(parentColumnReferences[0].SelectListColumn));
				}
			}

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetSchemaReferenceUsage()
		{
			return _currentNode.Statement.AllTerminals.Where(t => t.Id == Terminals.SchemaIdentifier && t.Token.Value.ToQuotedIdentifier() == _currentNode.Token.Value.ToQuotedIdentifier());
		}
	}
}
