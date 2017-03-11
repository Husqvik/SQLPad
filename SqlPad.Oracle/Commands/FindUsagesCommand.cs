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
			IEnumerable<TerminalUsage> usages;

			switch (_currentNode.Id)
			{
				case Terminals.IntegerLiteral:
				case Terminals.StringLiteral:
				case Terminals.NumberLiteral:
					usages = GetLiteralUsage();
					break;
				case Terminals.BindVariableIdentifier:
					usages = GetBindVariableUsage();
					break;
				case Terminals.ObjectAlias:
				case Terminals.ObjectIdentifier:
					usages = GetObjectReferences().SelectMany(GetObjectReferenceUsage);
					break;
				case Terminals.SchemaIdentifier:
					usages = GetSchemaReferenceUsage();
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
						usages = functionUsages;
						break;
					}

					goto case Terminals.ColumnAlias;
				case Terminals.ColumnAlias:
					usages = GetColumnUsagesInternal(_semanticModel, _currentNode, false);
					break;
				default:
					throw new NotSupportedException($"Terminal '{_currentNode.Id}' is not supported. ");
			}

			_executionContext.SegmentsToReplace.AddRange(
				usages.Select(n =>
					new TextSegment
					{
						IndextStart = n.Terminal.SourcePosition.IndexStart,
						Length = n.Terminal.SourcePosition.Length,
						DisplayOptions = n.Option
					}));
		}

		private IEnumerable<TerminalUsage> GetBindVariableUsage()
		{
			return GetBindVariable(_semanticModel, _currentNode.Token.Value).Nodes.Select(CreateStandardTerminalUsage);
		}

		public static BindVariableConfiguration GetBindVariable(OracleStatementSemanticModel semanticModel, string bindVariableIdentifier)
		{
			var normalizedIdentifier = bindVariableIdentifier.ToQuotedIdentifier();
			return semanticModel.Statement.BindVariables
				.SingleOrDefault(v => v.Nodes.Any(n => String.Equals(n.Token.Value.ToQuotedIdentifier(), normalizedIdentifier)));
		}

		private IEnumerable<TerminalUsage> GetLiteralUsage()
		{
			return GetEqualValueLiteralTerminals(_semanticModel.Statement, _currentNode).Select(CreateStandardTerminalUsage);
		}

		public static IEnumerable<StatementGrammarNode> GetEqualValueLiteralTerminals(OracleStatement statement, StatementGrammarNode literal)
		{
			return statement.RootNode.Terminals.Where(t => String.Equals(t.Id, literal.Id) && String.Equals(t.Token.Value, literal.Token.Value));
		}

		private ICollection<TerminalUsage> GetFunctionReferenceUsage()
		{
			var functionReference = _queryBlock.AllProgramReferences
				.FirstOrDefault(f => f.ProgramIdentifierNode == _currentNode && f.Metadata != null);

			if (functionReference == null)
				return new TerminalUsage[0];

			return _semanticModel.QueryBlocks
				.SelectMany(qb => qb.AllProgramReferences)
				.Where(f => f.Metadata == functionReference.Metadata)
				.Select(f => CreateStandardTerminalUsage(f.ProgramIdentifierNode))
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

		private IEnumerable<TerminalUsage> GetObjectReferenceUsage(OracleObjectWithColumnsReference objectReference)
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
				.Concat(nodes)
				.Select(CreateStandardTerminalUsage);
		}

		private static TerminalUsage CreateStandardTerminalUsage(StatementGrammarNode terminal)
		{
			return new TerminalUsage { Terminal = terminal, Option = terminal.Id.IsAlias() ? DisplayOptions.Definition : DisplayOptions.Usage };
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

		private static IEnumerable<TerminalUsage> GetModelClauseAndPivotTableReferences(OracleReference columnReference, bool onlyAliasOrigin)
		{
			var usages = new List<TerminalUsage>();
			if (columnReference.Owner == null)
			{
				return usages;
			}

			var normalizedName = columnReference.NormalizedName;
			var modelClauseAndPivotTableColumnReferences = columnReference.Owner.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 &&
				            c != columnReference &&
				            c.Placement.In(StatementPlacement.Model, StatementPlacement.PivotClause) &&
				            String.Equals(c.SelectListColumn?.NormalizedName, normalizedName));

			foreach (var innerReference in modelClauseAndPivotTableColumnReferences)
			{
				var definition = new TerminalUsage { Terminal = innerReference.SelectListColumn.AliasNode, Option = DisplayOptions.Definition };
				usages.Add(definition);

				if (!innerReference.SelectListColumn.IsDirectReference)
				{
					continue;
				}

				if (innerReference.SelectListColumn.HasExplicitAlias)
				{
					var usage = new TerminalUsage { Terminal = innerReference.ColumnNode, Option = DisplayOptions.Usage };
					usages.Add(usage);
				}

				var objectReference = (OracleDataObjectReference)innerReference.ColumnNodeObjectReferences.First();
				foreach (var innerObjectReference in objectReference.IncludeInnerReferences)
				{
					usages.AddRange(GetChildQueryBlockColumnReferences(innerObjectReference, innerReference, onlyAliasOrigin));
				}
			}

			if (columnReference.Owner.ModelReference != null)
			{
				foreach (var modelColumn in columnReference.Owner.ModelReference.ColumnDefinitions.Where(c => !c.IsDirectReference && c.AliasNode != null && String.Equals(c.NormalizedName, normalizedName)))
				{
					var definition = new TerminalUsage { Terminal = modelColumn.AliasNode, Option = DisplayOptions.Definition };
					usages.Add(definition);
				}
			}

			return usages;
		}

		internal static IEnumerable<StatementGrammarNode> GetColumnUsages(OracleStatementSemanticModel semanticModel, StatementGrammarNode columnNode, bool onlyAliasOrigin)
		{
			return GetColumnUsagesInternal(semanticModel, columnNode, onlyAliasOrigin)
				.Select(u => u.Terminal);
		}

		private static IEnumerable<TerminalUsage> GetColumnUsagesInternal(OracleStatementSemanticModel semanticModel, StatementGrammarNode columnNode, bool onlyAliasOrigin)
		{
			var usages = Enumerable.Empty<TerminalUsage>();
			var columnReference = semanticModel.AllReferenceContainers
				.SelectMany(c => c.ColumnReferences)
				.FirstOrDefault(c => (c.ColumnNode == columnNode || c.SelectListColumn?.AliasNode == columnNode) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeColumnReferences.Count == 1);

			OracleSelectListColumn selectListColumn;
			if (columnReference != null)
			{
				var objectReference = columnReference.ColumnNodeObjectReferences.First();
				var sourceColumnReferences = objectReference.Owner == null ? objectReference.Container.ColumnReferences : objectReference.Owner.AllColumnReferences;
				var columnReferences = sourceColumnReferences.Where(c => IsValidReference(c, columnReference, objectReference)).ToArray();
				usages = columnReferences.Select(c => new TerminalUsage { Terminal = c.ColumnNode, Option = DisplayOptions.Usage });

				if (objectReference is OraclePivotTableReference pivotTableReference)
				{
					var pivotColumnAliases = pivotTableReference.PivotColumns
						.Where(c => c.AliasNode != null && String.Equals(c.NormalizedName, columnReference.NormalizedName))
						.Select(c => new TerminalUsage { Terminal = c.AliasNode, Option = DisplayOptions.Definition });

					usages = usages.Concat(pivotColumnAliases);
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
						var definition = new TerminalUsage { Terminal = selectListColumn.AliasNode, Option = DisplayOptions.Definition };
						usages = usages.Concat(new[] { definition });
					}
				}
				else
				{
					selectListColumn = columnReference.Owner.Columns.Single(c => c.AliasNode == columnNode);
					var nodeList = Enumerable.Repeat(new TerminalUsage { Terminal = selectListColumn.AliasNode, Option = DisplayOptions.Definition }, 1);
					searchChildren = selectListColumn.IsDirectReference;

					usages = searchChildren ? usages.Concat(nodeList) : nodeList;
				}

				usages = usages.Concat(GetModelClauseAndPivotTableReferences(columnReference, onlyAliasOrigin));

				if (searchChildren)
				{
					usages = usages.Concat(GetChildQueryBlockColumnReferences(objectReference, columnReference, onlyAliasOrigin));
				}
			}
			else
			{
				var queryBlock = semanticModel.GetQueryBlock(columnNode);
				selectListColumn = queryBlock.Columns.SingleOrDefault(c => c.AliasNode == columnNode || c.ExplicitAliasNode == columnNode);

				var usageOption = DisplayOptions.Usage;
				if (selectListColumn?.ExplicitAliasNode == columnNode)
				{
					usageOption = DisplayOptions.Definition;

					if (queryBlock.IsRecursive && queryBlock.FollowingConcatenatedQueryBlock != null)
					{
						var oracleDataObjectReference = queryBlock.FollowingConcatenatedQueryBlock.ObjectReferences.FirstOrDefault(o => o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == queryBlock.FollowingConcatenatedQueryBlock);
						usages =
							queryBlock.FollowingConcatenatedQueryBlock.AllColumnReferences
								.Where(c => String.Equals(c.NormalizedName, selectListColumn.NormalizedName) && c.ColumnNodeColumnReferences.Count == 1 && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.First() == oracleDataObjectReference)
								.Select(c => CreateStandardTerminalUsage(c.ColumnNode));
					}
				}

				var usage = new TerminalUsage { Terminal = columnNode, Option = usageOption };
				usages = usages.Concat(Enumerable.Repeat(usage, 1));

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
								return GetColumnUsagesInternal(semanticModel, columnReference.ColumnNode, onlyAliasOrigin);
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
							var pivotUsages = queryBlock.AllColumnReferences
								.Where(c => c.ValidObjectReference == pivotTableColumn.PivotTable && c.HasExplicitDefinition && String.Equals(c.NormalizedName, selectListColumn.NormalizedName))
								.Select(c => new TerminalUsage { Terminal = c.ColumnNode, Option = DisplayOptions.Usage });

							usages = usages.Concat(pivotUsages);
						}
					}
				}
				else
				{
					var orderByReferenceUsages = queryBlock.ColumnReferences
						.Where(c => c.Placement == StatementPlacement.OrderBy && c.ValidObjectReference == queryBlock.SelfObjectReference && String.Equals(c.NormalizedName, selectListColumn.NormalizedName))
						.Select(c => new TerminalUsage { Terminal = c.ColumnNode, Option = DisplayOptions.Usage });
					usages = usages.Concat(orderByReferenceUsages);
				}
			}

			var parentUsages = GetParentQueryBlockUsages(selectListColumn);
			if (onlyAliasOrigin)
			{
				var columnName = columnNode.Token.Value.ToQuotedIdentifier();
				parentUsages = parentUsages.TakeWhile(u => String.Equals(u.Terminal.Token.Value.ToQuotedIdentifier(), columnName));
			}

			usages = usages.Concat(parentUsages);

			return usages;
		}

		private static IEnumerable<TerminalUsage> GetChildQueryBlockColumnReferences(OracleObjectWithColumnsReference objectReference, OracleColumnReference columnReference, bool onlyAliasOrigin)
		{
			var usages = Enumerable.Empty<TerminalUsage>();
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
					usages = usages.Concat(Enumerable.Repeat(CreateStandardTerminalUsage(childColumn.AliasNode), 1));
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

					var childSelectColumnReferences = childQueryBlock.Columns
						.SelectMany(c => c.ColumnReferences)
						.Where(c => !c.ReferencesAllColumns && c.ColumnNodeObjectReferences.Count == 1 && String.Equals(c.SelectListColumn.NormalizedName, columnReference.NormalizedName) && c.ColumnNode != childColumn.AliasNode)
						.Select(c => new TerminalUsage { Terminal = c.ColumnNode, Option = DisplayOptions.Usage });

					usages = usages.Concat(childSelectColumnReferences);

					var childColumnReferences = childQueryBlock.ColumnReferences
						.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == childColumnReference.ColumnNodeObjectReferences.Single() && String.Equals(c.NormalizedName, childColumnReference.NormalizedName))
						.Select(c => new TerminalUsage { Terminal = c.ColumnNode, Option = DisplayOptions.Usage });

					usages = usages.Concat(childColumnReferences);

					if (childColumnReference.ColumnNodeObjectReferences.Count == 1)
					{
						usages = usages.Concat(GetChildQueryBlockColumnReferences(childColumnReference.ColumnNodeObjectReferences.Single(), childColumnReference, onlyAliasOrigin));
					}
				}
			}

			return usages;
		}

		private static bool MatchesOwnerQueryBlock(OracleObjectWithColumnsReference objectReference, OracleQueryBlock ownerQueryBlock)
		{
			Func<OracleObjectWithColumnsReference, bool> ownerQueryBlockPredicate = r => r.QueryBlocks.Count == 1 && r.QueryBlocks.First() == ownerQueryBlock;
			var dataObjectReference = objectReference as OracleDataObjectReference;
			var matchesOwnerQueryBlock = dataObjectReference?.IncludeInnerReferences.Any(ownerQueryBlockPredicate) ?? ownerQueryBlockPredicate(objectReference);
			var matchesOwnSelectColumn = objectReference.Owner != null && objectReference.QueryBlocks.FirstOrDefault() == objectReference.Owner;
			return matchesOwnerQueryBlock || matchesOwnSelectColumn;
		}

		internal static IEnumerable<StatementGrammarNode> GetParentQueryBlockColumnUsages(OracleSelectListColumn selectListColumn)
		{
			return GetParentQueryBlockUsages(selectListColumn).Select(u => u.Terminal);
		}

		private static IEnumerable<TerminalUsage> GetParentQueryBlockUsages(OracleSelectListColumn selectListColumn)
		{
			var usages = Enumerable.Empty<TerminalUsage>();
			if (selectListColumn == null)
			{
				return usages;
			}

			var aliasNode = selectListColumn.ExplicitAliasNode ?? selectListColumn.AliasNode;
			if (aliasNode == null)
			{
				return usages;
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

				usages = parentReferences.Select(c => new TerminalUsage { Terminal = c.ColumnNode, Option = DisplayOptions.Usage });
				if (parentQueryBlock.ExplicitColumnNameList != null)
				{
					continue;
				}

				var parentColumnReferences = parentReferences.Where(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectReference).ToArray();
				if (parentColumnReferences.Length == 1)
				{
					var parentColumnReference = parentColumnReferences[0];
					usages = usages
						.Concat(parentColumnReferences.Where(c => c.SelectListColumn.HasExplicitAlias).Select(c => new TerminalUsage { Terminal = c.SelectListColumn.AliasNode, Option = DisplayOptions.Definition }))
						.Concat(GetParentQueryBlockUsages(parentColumnReference.SelectListColumn));
				}
			}

			return usages;
		}

		private IEnumerable<TerminalUsage> GetSchemaReferenceUsage()
		{
			var schemaName = _currentNode.Token.Value.ToQuotedIdentifier();
			return _currentNode.Statement.AllTerminals
				.Where(t => String.Equals(t.Id, Terminals.SchemaIdentifier) && String.Equals(t.Token.Value.ToQuotedIdentifier(), schemaName))
				.Select(CreateStandardTerminalUsage);
		}

		private struct TerminalUsage
		{
			public StatementGrammarNode Terminal { get; set; }

			public DisplayOptions Option { get; set; }
		}
	}
}
