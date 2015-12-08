using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class FindUsagesCommand
	{
		private readonly StatementGrammarNode _currentNode;
		private readonly OracleStatementSemanticModel _semanticModel;
		private readonly OracleQueryBlock _queryBlock;
		private readonly ActionExecutionContext _executionContext;

		public static readonly CommandExecutionHandler FindUsages =
			new CommandExecutionHandler
			{
				Name = "FindUsages",
				DefaultGestures = GenericCommands.FindUsages.InputGestures,
				ExecutionHandler = ExecutionHandlerImplementation
			};

		private static void ExecutionHandlerImplementation(ActionExecutionContext executionContext)
		{
			var commandInstance = new FindUsagesCommand(executionContext);
			if (commandInstance.CanExecute())
			{
				commandInstance.ExecuteFindUsages();
			}
		}

		private FindUsagesCommand(ActionExecutionContext executionContext)
		{
			if (executionContext.DocumentRepository == null || executionContext.DocumentRepository.StatementText != executionContext.StatementText)
			{
				return;
			}

			_currentNode = GetFindUsagesCompatibleTerminal(executionContext.DocumentRepository.Statements, executionContext.CaretOffset);
			if (_currentNode == null)
			{
				return;
			}

			_semanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[_currentNode.Statement].SemanticModel;
			_queryBlock = _semanticModel.GetQueryBlock(_currentNode);
			_executionContext = executionContext;
		}

		private static StatementGrammarNode GetFindUsagesCompatibleTerminal(StatementCollection statements, int currentPosition)
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
			IEnumerable<StatementGrammarNode> nodes;

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
					nodes = GetColumnUsages();
					break;
				default:
					throw new NotSupportedException($"Terminal '{_currentNode.Id}' is not supported. ");
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

		private IEnumerable<StatementGrammarNode> GetBindVariableUsage()
		{
			return GetBindVariable(_semanticModel, _currentNode.Token.Value).Nodes;
		}

		public static BindVariableConfiguration GetBindVariable(OracleStatementSemanticModel semanticModel, string bindVariableIdentifier)
		{
			var normalizedIdentifier = bindVariableIdentifier.ToQuotedIdentifier();
			return semanticModel.Statement.BindVariables
				.SingleOrDefault(v => v.Nodes.Any(n => String.Equals(n.Token.Value.ToQuotedIdentifier(), normalizedIdentifier)));
		}

		private IEnumerable<StatementGrammarNode> GetLiteralUsage()
		{
			return GetEqualValueLiteralTerminals(_semanticModel.Statement, _currentNode);
		}

		public static IEnumerable<StatementGrammarNode> GetEqualValueLiteralTerminals(OracleStatement statement, StatementGrammarNode literal)
		{
			return statement.RootNode.Terminals.Where(t => t.Id == literal.Id && t.Token.Value == literal.Token.Value);
		}

		private ICollection<StatementGrammarNode> GetFunctionReferenceUsage()
		{
			var functionReference = _queryBlock.AllProgramReferences
				.FirstOrDefault(f => f.FunctionIdentifierNode == _currentNode && f.Metadata != null);

			if (functionReference == null)
				return new StatementGrammarNode[0];

			return _semanticModel.QueryBlocks
				.SelectMany(qb => qb.AllProgramReferences)
				.Where(f => f.Metadata == functionReference.Metadata)
				.Select(f => f.FunctionIdentifierNode)
				.ToArray();
		}

		private IEnumerable<OracleObjectWithColumnsReference> GetObjectReferences()
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

		private IEnumerable<StatementGrammarNode> GetObjectReferenceUsage(OracleObjectWithColumnsReference objectReference)
		{
			var nodes = new List<StatementGrammarNode>();
			if (objectReference.Type != ReferenceType.InlineView)
			{
				nodes.Add(objectReference.ObjectNode);
			}

			var dataObjectReference = objectReference as OracleDataObjectReference;
			if (dataObjectReference?.AliasNode != null)
			{
				nodes.Add(dataObjectReference.AliasNode);
			}

			if (objectReference.QueryBlocks.Count == 1 && objectReference.Type == ReferenceType.CommonTableExpression)
			{
				var referencedQueryBlock = objectReference.QueryBlocks.First();
				if (referencedQueryBlock.PrecedingConcatenatedQueryBlock != null && referencedQueryBlock.PrecedingConcatenatedQueryBlock.IsRecursive)
				{
					nodes.Add(referencedQueryBlock.PrecedingConcatenatedQueryBlock.AliasNode);
				}
				else
				{
					nodes.Add(referencedQueryBlock.AliasNode);
				}

				if (referencedQueryBlock.FollowingConcatenatedQueryBlock != null)
				{
					var recursiveQueryBlockReferences = referencedQueryBlock.FollowingConcatenatedQueryBlock.ObjectReferences.Where(r => r.Type == ReferenceType.CommonTableExpression && r.QueryBlocks.Count == 1 && r.QueryBlocks.First() == referencedQueryBlock.FollowingConcatenatedQueryBlock);
					nodes.AddRange(recursiveQueryBlockReferences.Select(r => r.ObjectNode));
				}
			}

			var queryBlock = objectReference.Owner;
			var columnReferences = queryBlock.AllColumnReferences.Where(c => c.ObjectNode != null && c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.First() == objectReference);

			foreach (var correlatedQueryBlock in _semanticModel.QueryBlocks.Where(qb => qb.OuterCorrelatedQueryBlock == queryBlock))
			{
				var correlatedColumnReferences = correlatedQueryBlock.AllColumnReferences
					.Where(c => c.ObjectNode != null && c.ValidObjectReference == objectReference);

				columnReferences = columnReferences.Concat(correlatedColumnReferences);
			}

			return columnReferences
				.Select(c => c.ObjectNode)
				.Concat(nodes);
		}

		private static bool IsValidReference(OracleColumnReference columnReference, OracleColumnReference selectedColumnReference, OracleObjectWithColumnsReference selectedObjectReference)
		{
			return
				columnReference.ColumnNodeObjectReferences.Count == 1 &&
				IsObjectReferenceMatched(columnReference.ColumnNodeObjectReferences.First(), selectedObjectReference) &&
				String.Equals(columnReference.NormalizedName, selectedColumnReference.NormalizedName);
		}

		private static bool IsObjectReferenceMatched(OracleObjectWithColumnsReference columnObjectReference, OracleObjectWithColumnsReference selectedObjectReference)
		{
			var isSelectedObjectReferencePivotOrModel = selectedObjectReference is OraclePivotTableReference || selectedObjectReference is OracleSqlModelReference;
			var isColumnObjectReferencePivorOrModel = columnObjectReference is OraclePivotTableReference || columnObjectReference is OracleSqlModelReference;
			return columnObjectReference == selectedObjectReference ||
			       (isSelectedObjectReferencePivotOrModel && ((OracleDataObjectReference)selectedObjectReference).IncludeInnerReferences.Any(o => o == selectedObjectReference)) ||
			       (isColumnObjectReferencePivorOrModel && ((OracleDataObjectReference)columnObjectReference).IncludeInnerReferences.Any(o => o == selectedObjectReference));
		}

		private static IEnumerable<StatementGrammarNode> GetModelClauseAndPivotTableReferences(OracleReference columnReference, bool onlyAliasOrigin)
		{
			var nodes = new List<StatementGrammarNode>();
			if (columnReference.Owner == null)
			{
				return nodes;
			}

			var normalizedName = columnReference.NormalizedName;
			var modelClauseAndPivotTableColumnReferences = columnReference.Owner.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 &&
				            c != columnReference &&
				            c.Placement.In(StatementPlacement.Model, StatementPlacement.PivotClause) &&
				            String.Equals(c.SelectListColumn?.NormalizedName, normalizedName));

			foreach (var innerReference in modelClauseAndPivotTableColumnReferences)
			{
				nodes.Add(innerReference.SelectListColumn.AliasNode);

				if (!innerReference.SelectListColumn.IsDirectReference)
				{
					continue;
				}

				if (innerReference.SelectListColumn.HasExplicitAlias)
				{
					nodes.Add(innerReference.ColumnNode);
				}

				var objectReference = (OracleDataObjectReference)innerReference.ColumnNodeObjectReferences.First();
				foreach (var innerObjectReference in objectReference.IncludeInnerReferences)
				{
					nodes.AddRange(GetChildQueryBlockColumnReferences(innerObjectReference, innerReference, onlyAliasOrigin));
				}
			}

			if (columnReference.Owner.ModelReference != null)
			{
				foreach (var modelColumn in columnReference.Owner.ModelReference.ColumnDefinitions.Where(c => !c.IsDirectReference && c.AliasNode != null && String.Equals(c.NormalizedName, normalizedName)))
				{
					nodes.Add(modelColumn.AliasNode);
				}
			}

			return nodes;
		}

		private IEnumerable<StatementGrammarNode> GetColumnUsages()
		{
			return GetColumnUsages(_semanticModel, _currentNode, false);
		}

		internal static IEnumerable<StatementGrammarNode> GetColumnUsages(OracleStatementSemanticModel semanticModel, StatementGrammarNode columnNode, bool onlyAliasOrigin)
		{
			IEnumerable<StatementGrammarNode> nodes;
			var columnReference = semanticModel.AllReferenceContainers
				.SelectMany(c => c.ColumnReferences)
				.FirstOrDefault(c => (c.ColumnNode == columnNode || c.SelectListColumn?.AliasNode == columnNode) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeColumnReferences.Count == 1);

			OracleSelectListColumn selectListColumn;
			if (columnReference != null)
			{
				var objectReference = columnReference.ColumnNodeObjectReferences.First();
				var sourceColumnReferences = objectReference.Owner == null ? objectReference.Container.ColumnReferences : objectReference.Owner.AllColumnReferences;
				var columnReferences = sourceColumnReferences.Where(c => IsValidReference(c, columnReference, objectReference)).ToArray();
				nodes = columnReferences.Select(c => c.ColumnNode);

				var pivotTableReference = objectReference as OraclePivotTableReference;
				if (pivotTableReference != null)
				{
					var pivotColumnAliases = pivotTableReference.PivotColumns
						.Where(c => c.AliasNode != null && String.Equals(c.NormalizedName, columnReference.NormalizedName))
						.Select(c => c.AliasNode);

					nodes = nodes.Concat(pivotColumnAliases);
				}

				bool searchChildren;
				if (String.Equals(columnNode.Id, Terminals.Identifier))
				{
					searchChildren = true;

					selectListColumn = columnReference.SelectListColumn;
					if (selectListColumn == null)
					{
						selectListColumn = columnReferences.FirstOrDefault(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectReference && c.SelectListColumn.HasExplicitDefinition)?.SelectListColumn;
					}
					else if (!selectListColumn.IsDirectReference)
					{
						selectListColumn = null;
					}

					if (selectListColumn != null && !onlyAliasOrigin && selectListColumn.HasExplicitAlias)
					{
						nodes = nodes.Concat(new[] { selectListColumn.AliasNode });
					}
				}
				else
				{
					selectListColumn = columnReference.Owner.Columns.Single(c => c.AliasNode == columnNode);
					var nodeList = new List<StatementGrammarNode> { selectListColumn.AliasNode };
					searchChildren = selectListColumn.IsDirectReference;

					nodes = searchChildren ? nodes.Concat(nodeList) : nodeList;
				}

				nodes = nodes.Concat(GetModelClauseAndPivotTableReferences(columnReference, onlyAliasOrigin));

				if (searchChildren)
				{
					nodes = nodes.Concat(GetChildQueryBlockColumnReferences(objectReference, columnReference, onlyAliasOrigin));
				}
			}
			else
			{
				nodes = new[] { columnNode };
				var queryBlock = semanticModel.GetQueryBlock(columnNode);
				selectListColumn = queryBlock.Columns.SingleOrDefault(c => c.AliasNode == columnNode);

				if (selectListColumn == null)
				{
					if (queryBlock.ModelReference != null)
					{
						selectListColumn = queryBlock.ModelReference.ColumnDefinitions.SingleOrDefault(c => c.AliasNode == columnNode);
						if (selectListColumn != null)
						{
							columnReference = queryBlock.ModelReference.DimensionReferenceContainer.ColumnReferences
								.Concat(queryBlock.ModelReference.MeasuresReferenceContainer.ColumnReferences)
								.Concat(queryBlock.AllColumnReferences)
								.FirstOrDefault(c => String.Equals(c.NormalizedName, selectListColumn.NormalizedName));

							if (columnReference != null)
							{
								return GetColumnUsages(semanticModel, columnReference.ColumnNode, onlyAliasOrigin);
							}
						}
					}
					else
					{
						var pivotTableColumn =
							queryBlock.ObjectReferences.OfType<OraclePivotTableReference>()
								.SelectMany(pt => pt.PivotColumns.Select(c => new { PivotTable = pt, Column = c }))
								.SingleOrDefault(ptc => ptc.Column.AliasNode == columnNode);

						if (pivotTableColumn != null)
						{
							selectListColumn = pivotTableColumn.Column;
							var columnNodes = queryBlock.AllColumnReferences
								.Where(c => c.ValidObjectReference == pivotTableColumn.PivotTable && c.HasExplicitDefinition && String.Equals(c.NormalizedName, selectListColumn.NormalizedName))
								.Select(c => c.ColumnNode);

							nodes = nodes.Concat(columnNodes);
						}
					}
				}
				else
				{
					var orderByReferenceNodes = queryBlock.ColumnReferences
						.Where(c => c.Placement == StatementPlacement.OrderBy && c.ValidObjectReference == queryBlock.SelfObjectReference && String.Equals(c.NormalizedName, selectListColumn.NormalizedName))
						.Select(c => c.ColumnNode);
					nodes = nodes.Concat(orderByReferenceNodes);
				}
			}

			var parentNodes = GetParentQueryBlockReferences(selectListColumn);
			if (onlyAliasOrigin)
			{
				parentNodes = parentNodes.TakeWhile(t => String.Equals(t.Token.Value.ToQuotedIdentifier(), columnNode.Token.Value.ToQuotedIdentifier()));
			}

			nodes = nodes.Concat(parentNodes);

			return nodes;
		}

		private static IEnumerable<StatementGrammarNode> GetChildQueryBlockColumnReferences(OracleObjectWithColumnsReference objectReference, OracleColumnReference columnReference, bool onlyAliasOrigin)
		{
			var nodes = Enumerable.Empty<StatementGrammarNode>();
			var dataObjectReference = objectReference as OracleDataObjectReference;
			var sourceObjectReferences = dataObjectReference == null ? Enumerable.Repeat(objectReference, 1) : dataObjectReference.IncludeInnerReferences;

			foreach (var reference in sourceObjectReferences)
			{
				if (reference.QueryBlocks.Count != 1)
				{
					continue;
				}

				var childQueryBlock = reference.QueryBlocks.First();
				var childColumn = childQueryBlock.Columns.SingleOrDefault(c => String.Equals(c.NormalizedName, columnReference.NormalizedName));

				if (childColumn == null)
				{
					continue;
				}

				if (childColumn.AliasNode != null && (!onlyAliasOrigin || childColumn.HasExplicitAlias))
				{
					nodes = nodes.Concat(Enumerable.Repeat(childColumn.AliasNode, 1));
				}

				if (childColumn.IsDirectReference && childColumn.ColumnReferences.Count > 0 && childColumn.ColumnReferences.All(cr => cr.ColumnNodeColumnReferences.Count == 1))
				{
					var childColumnReference = childColumn.ColumnReferences.Single();

					if (onlyAliasOrigin)
					{
						var childColumnObjectReference = childColumnReference.ValidObjectReference;
						if (childColumnObjectReference == null || childColumnObjectReference.QueryBlocks.Count != 1 || !String.Equals(childColumnReference.NormalizedName, childColumn.NormalizedName))
						{
							continue;
						}
					}

					var childSelectColumnReferences = childQueryBlock.Columns.SelectMany(c => c.ColumnReferences)
						.Where(c => !c.ReferencesAllColumns && c.ColumnNodeObjectReferences.Count == 1 && String.Equals(c.SelectListColumn.NormalizedName, columnReference.NormalizedName) && c.ColumnNode != childColumn.AliasNode)
						.Select(c => c.ColumnNode);

					nodes = nodes.Concat(childSelectColumnReferences);

					nodes = nodes.Concat(childQueryBlock.ColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == childColumnReference.ColumnNodeObjectReferences.Single() && String.Equals(c.NormalizedName, childColumnReference.NormalizedName)).Select(c => c.ColumnNode));

					if (childColumnReference.ColumnNodeObjectReferences.Count == 1)
					{
						nodes = nodes.Concat(GetChildQueryBlockColumnReferences(childColumnReference.ColumnNodeObjectReferences.Single(), childColumnReference, onlyAliasOrigin));
					}
				}
			}

			return nodes;
		}

		private static bool MatchesOwnerQueryBlock(OracleObjectWithColumnsReference objectReference, OracleQueryBlock ownerQueryBlock)
		{
			Func<OracleObjectWithColumnsReference, bool> ownerQueryBlockPredicate = r => r.QueryBlocks.Count == 1 && r.QueryBlocks.First() == ownerQueryBlock;
			var dataObjectReference = objectReference as OracleDataObjectReference;
			var matchesOwnerQueryBlock = dataObjectReference?.IncludeInnerReferences.Any(ownerQueryBlockPredicate) ?? ownerQueryBlockPredicate(objectReference);
			var matchesOwnSelectColumn = objectReference.Owner != null && objectReference.QueryBlocks.FirstOrDefault() == objectReference.Owner;
			return matchesOwnerQueryBlock || matchesOwnSelectColumn;
		}

		internal static IEnumerable<StatementGrammarNode> GetParentQueryBlockReferences(OracleSelectListColumn selectListColumn)
		{
			var nodes = Enumerable.Empty<StatementGrammarNode>();
			if (selectListColumn?.AliasNode == null)
			{
				return nodes;
			}

			var parentQueryBlocks = selectListColumn.Owner.SemanticModel.QueryBlocks
				.Where(qb => qb.ObjectReferences.SelectMany(o => o.IncludeInnerReferences).SelectMany(o => o.QueryBlocks).Contains(selectListColumn.Owner));

			foreach (var parentQueryBlock in parentQueryBlocks)
			{
				var parentReferences = parentQueryBlock.AllColumnReferences
					.Where(c => c.ColumnNodeColumnReferences.Count == 1 && c.ColumnNodeObjectReferences.Count == 1 &&
					            MatchesOwnerQueryBlock(c.ColumnNodeObjectReferences.First(), selectListColumn.Owner) &&
					            String.Equals(c.NormalizedName, selectListColumn.NormalizedName))
					.ToArray();

				if (parentReferences.Length == 0)
				{
					continue;
				}

				nodes = parentReferences.Select(c => c.ColumnNode);

				var parentColumnReferences = parentReferences.Where(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectReference).ToArray();

				if (parentColumnReferences.Length == 1)
				{
					var parentColumnReference = parentColumnReferences[0];
					nodes = nodes
						.Concat(parentColumnReferences.Where(c => c.SelectListColumn.HasExplicitAlias).Select(c => c.SelectListColumn.AliasNode))
						.Concat(GetParentQueryBlockReferences(parentColumnReference.SelectListColumn));
				}
			}

			return nodes;
		}

		private IEnumerable<StatementGrammarNode> GetSchemaReferenceUsage()
		{
			return _currentNode.Statement.AllTerminals.Where(t => String.Equals(t.Id, Terminals.SchemaIdentifier) && String.Equals(t.Token.Value.ToQuotedIdentifier(), _currentNode.Token.Value.ToQuotedIdentifier()));
		}
	}
}
