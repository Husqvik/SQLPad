using System;
using System.Collections.Generic;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleStatementSemanticModel
	{
		private readonly Dictionary<StatementDescriptionNode, OracleQueryBlock> _queryBlockResults = new Dictionary<StatementDescriptionNode, OracleQueryBlock>();
		private readonly Dictionary<OracleSelectListColumn, ICollection<OracleTableReference>> _asteriskTableReferences = new Dictionary<OracleSelectListColumn, ICollection<OracleTableReference>>();
		private readonly List<ICollection<OracleColumnReference>> _joinClauseColumnReferences = new List<ICollection<OracleColumnReference>>();

		public OracleStatement Statement { get; private set; }

		public ICollection<OracleQueryBlock> QueryBlocks
		{
			get { return _queryBlockResults.Values; }
		}

		public OracleStatementSemanticModel(string sqlText, OracleStatement statement, DatabaseModelFake databaseModel)
		{
			if (statement == null)
				throw new ArgumentNullException("statement");

			Statement = statement;

			_queryBlockResults = statement.NodeCollection.SelectMany(n => n.GetDescendants(NonTerminals.QueryBlock))
				.OrderByDescending(q => q.Level).ToDictionary(n => n, n => new OracleQueryBlock { RootNode = n });

			foreach (var queryBlockNode in _queryBlockResults)
			{
				var queryBlock = queryBlockNode.Key;
				var item = queryBlockNode.Value;

				var scalarSubqueryExpression = queryBlock.GetAncestor(NonTerminals.Expression, false);
				if (scalarSubqueryExpression != null)
				{
					item.Type = QueryBlockType.ScalarSubquery;
				}

				var factoredSubqueryReference = queryBlock.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, NonTerminals.SubqueryComponent);
				if (factoredSubqueryReference != null)
				{
					item.Alias = factoredSubqueryReference.ChildNodes.First().Token.Value.ToQuotedIdentifier();
					item.Type = QueryBlockType.CommonTableExpression;
				}
				else
				{
					var selfTableReference = queryBlock.GetAncestor(NonTerminals.TableReference, false);
					if (selfTableReference != null)
					{
						item.Type = QueryBlockType.Normal;

						var nestedSubqueryAlias = selfTableReference.ChildNodes.SingleOrDefault(n => n.Id == Terminals.Alias);
						if (nestedSubqueryAlias != null)
						{
							item.Alias = nestedSubqueryAlias.Token.Value.ToQuotedIdentifier();
						}
					}
				}

				var fromClause = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
				var tableReferenceNonterminals = fromClause == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: fromClause.GetDescendantsWithinSameQuery(NonTerminals.TableReference).ToArray();

				foreach (var tableReferenceNonterminal in tableReferenceNonterminals)
				{
					var queryTableExpression = tableReferenceNonterminal.GetDescendantsWithinSameQuery(NonTerminals.QueryTableExpression).SingleOrDefault();
					if (queryTableExpression == null)
						continue;

					var tableReferenceAlias = tableReferenceNonterminal.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();
					
					var nestedQueryTableReference = queryTableExpression.GetPathFilterDescendants(f => f.Id != NonTerminals.Subquery, NonTerminals.NestedQuery).SingleOrDefault();
					if (nestedQueryTableReference != null)
					{
						var nestedQueryTableReferenceQueryBlock = nestedQueryTableReference.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery && n.Id != NonTerminals.SubqueryFactoringClause, NonTerminals.QueryBlock).Single();

						item.TableReferences.Add(new OracleTableReference
						{
							TableReferenceNode = tableReferenceNonterminal,
							TableNode = nestedQueryTableReferenceQueryBlock,
							Type = TableReferenceType.NestedQuery,
							AliasNode = tableReferenceAlias
						});

						continue;
					}

					var tableIdentifierNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == Terminals.ObjectIdentifier);

					if (tableIdentifierNode == null)
						continue;

					var schemaPrefixNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
					if (schemaPrefixNode != null)
					{
						schemaPrefixNode = schemaPrefixNode.ChildNodes.First();
					}

					var tableName = tableIdentifierNode.Token.Value.ToQuotedIdentifier();
					var commonTableExpressions = schemaPrefixNode != null
						? new StatementDescriptionNode[0]
						: GetCommonTableExpressionReferences(queryBlock, tableName, sqlText).ToArray();

					var referenceType = TableReferenceType.CommonTableExpression;

					var result = SchemaObjectResult.EmptyResult;
					if (commonTableExpressions.Length == 0)
					{
						referenceType = TableReferenceType.PhysicalObject;

						var objectName = tableIdentifierNode.Token.Value;
						var owner = schemaPrefixNode == null ? null : schemaPrefixNode.Token.Value;

						result = databaseModel.GetObject(OracleObjectIdentifier.Create(owner, objectName));
					}

					item.TableReferences.Add(new OracleTableReference
					                         {
												 TableReferenceNode = tableReferenceNonterminal,
						                         OwnerNode = schemaPrefixNode,
						                         TableNode = tableIdentifierNode,
						                         Type = referenceType,
												 Nodes = commonTableExpressions,
												 AliasNode = tableReferenceAlias,
												 SearchResult = result
					                         });
				}

				ResolveSelectList(item);

				ResolveWhereGroupByHavingReferences(item);

				ResolveJoinColumnReferences(item);
			}

			foreach (var queryBlock in _queryBlockResults.Values)
			{
				foreach (var nestedQueryReference in queryBlock.TableReferences.Where(t => t.Type != TableReferenceType.PhysicalObject))
				{
					if (nestedQueryReference.Type == TableReferenceType.NestedQuery)
					{
						nestedQueryReference.QueryBlocks.Add(_queryBlockResults[nestedQueryReference.TableNode]);
					}
					else
					{
						foreach (var referencedQueryBlock in nestedQueryReference.Nodes
							.SelectMany(cteNode => cteNode.GetDescendantsWithinSameQuery(NonTerminals.QueryBlock))
							.Where(qb => OracleObjectIdentifier.Create(null, qb.GetAncestor(NonTerminals.SubqueryComponent, false).ChildNodes.Single(n => n.Id == Terminals.ObjectIdentifier).Token.Value) == nestedQueryReference.FullyQualifiedName))
						{
							nestedQueryReference.QueryBlocks.Add(_queryBlockResults[referencedQueryBlock]);
						}
					}
				}
			}

			foreach (var asteriskTableReference in _asteriskTableReferences)
			{
				foreach (var tableReference in asteriskTableReference.Value)
				{
					if (tableReference.Type == TableReferenceType.PhysicalObject)
					{
						if (tableReference.SearchResult.SchemaObject == null)
							continue;

						foreach (OracleColumn physicalColumn in tableReference.SearchResult.SchemaObject.Columns)
						{
							var column = new OracleSelectListColumn
							             {
											 Owner = asteriskTableReference.Key.Owner,
											 ExplicitDefinition = false,
											 IsDirectColumnReference = true,
											 ColumnDescription = physicalColumn
							             };

							asteriskTableReference.Key.Owner.Columns.Add(column);
						}
					}
					else
					{
						foreach (var exposedColumn in tableReference.QueryBlocks.SelectMany(qb => qb.Columns).Where(c => !c.IsAsterisk))
						{
							var implicitColumn = exposedColumn.AsImplicit();
							implicitColumn.Owner = asteriskTableReference.Key.Owner;
							asteriskTableReference.Key.Owner.Columns.Add(implicitColumn);
						}
					}
				}
			}

			foreach (var queryBlock in _queryBlockResults.Values)
			{
				var columnReferencesExceptJoinClauses = queryBlock.Columns.SelectMany(c => c.ColumnReferences).Concat(queryBlock.ColumnReferences);
				ResolveColumnTableReferences(columnReferencesExceptJoinClauses, queryBlock.TableReferences);
			}

			foreach (var joinClauseColumnReferences in _joinClauseColumnReferences)
			{
				var queryBlock = joinClauseColumnReferences.First().Owner;
				foreach (var columnReference in joinClauseColumnReferences)
				{
					var fromClauseNode = columnReference.ColumnNode.GetAncestor(NonTerminals.FromClause);
					var relatedTableReferences = queryBlock.TableReferences
						.Where(t => t.TableReferenceNode.SourcePosition.IndexStart >= fromClauseNode.SourcePosition.IndexStart &&
						            t.TableReferenceNode.SourcePosition.IndexEnd <= columnReference.ColumnNode.SourcePosition.IndexStart).ToArray();
					ResolveColumnTableReferences(joinClauseColumnReferences, relatedTableReferences);
					queryBlock.ColumnReferences.Add(columnReference);
				}
			}
		}

		public OracleQueryBlock GetQueryBlock(StatementDescriptionNode node)
		{
			var queryBlockNode = node.GetAncestor(NonTerminals.QueryBlock, false);
			return queryBlockNode == null ? null : _queryBlockResults[queryBlockNode];
		}

		private void ResolveColumnTableReferences(IEnumerable<OracleColumnReference> columnReferences, ICollection<OracleTableReference> accessibleTableReferences)
		{
			foreach (var columnReference in columnReferences)
			{
				foreach (var tableReference in accessibleTableReferences)
				{
					if (!String.IsNullOrEmpty(columnReference.FullyQualifiedObjectName.NormalizedName) &&
						(tableReference.FullyQualifiedName == columnReference.FullyQualifiedObjectName ||
						 (String.IsNullOrEmpty(columnReference.FullyQualifiedObjectName.Owner) &&
						  tableReference.Type == TableReferenceType.PhysicalObject && tableReference.FullyQualifiedName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName)))
						columnReference.TableNodeReferences.Add(tableReference);

					int newTableReferences;
					{
						if (tableReference.Type == TableReferenceType.PhysicalObject)
						{
							if (tableReference.SearchResult.SchemaObject == null)
								continue;

							newTableReferences = tableReference.SearchResult.SchemaObject.Columns
								.Count(c => c.Name == columnReference.NormalizedName && (columnReference.TableNode == null || columnReference.NormalizedTableName == tableReference.FullyQualifiedName.NormalizedName));
						}
						else
						{
							newTableReferences = tableReference.QueryBlocks.SelectMany(qb => qb.Columns)
								.Count(c => c.NormalizedName == columnReference.NormalizedName && (columnReference.TableNode == null || columnReference.NormalizedTableName == tableReference.FullyQualifiedName.NormalizedName));
						}
					}

					if (newTableReferences > 0 &&
						(String.IsNullOrEmpty(columnReference.FullyQualifiedObjectName.NormalizedName) ||
						 columnReference.TableNodeReferences.Count > 0))
					{
						columnReference.ColumnNodeReferences.Add(tableReference);
					}
				}
			}
		}

		private void ResolveJoinColumnReferences(OracleQueryBlock queryBlock)
		{
			var fromClauses = queryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause);
			foreach (var fromClause in fromClauses)
			{
				var joinClauses = fromClause.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery && n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
				foreach (var joinClause in joinClauses)
				{
					var joinCondition = joinClause.GetPathFilterDescendants(n => n.Id != NonTerminals.JoinClause, NonTerminals.JoinColumnsOrCondition).SingleOrDefault();
					if (joinCondition == null)
						continue;

					var identifiers = joinCondition.GetDescendants(Terminals.Identifier);
					var columnReferences = new List<OracleColumnReference>();
					ResolveColumnReferenceFromIdentifiers(queryBlock, columnReferences, identifiers, ColumnReferenceType.Join);

					if (columnReferences.Count > 0)
					{
						_joinClauseColumnReferences.Add(columnReferences);
					}
				}
			}
		}

		private void ResolveWhereGroupByHavingReferences(OracleQueryBlock queryBlock)
		{
			var identifiers = GetIdentifiersFromNodesWithinSameQuery(queryBlock, NonTerminals.WhereClause, NonTerminals.GroupByClause, NonTerminals.HavingClause).ToArray();
			ResolveColumnReferenceFromIdentifiers(queryBlock, queryBlock.ColumnReferences, identifiers, ColumnReferenceType.WhereGroupHavingOrder);
		}

		private void ResolveColumnReferenceFromIdentifiers(OracleQueryBlock queryBlock, ICollection<OracleColumnReference> columnReferences, IEnumerable<StatementDescriptionNode> identifiers, ColumnReferenceType type)
		{
			foreach (var identifier in identifiers)
			{
				var prefixNonTerminal = identifier.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference)
					.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);

				var columnReference = CreateColumnReference(queryBlock, type, identifier, prefixNonTerminal);
				columnReferences.Add(columnReference);
			}
		}

		private IEnumerable<StatementDescriptionNode> GetIdentifiersFromNodesWithinSameQuery(OracleQueryBlock queryBlock, params string[] nonTerminalIds)
		{
			var identifiers = Enumerable.Empty<StatementDescriptionNode>();
			foreach (var nonTerminalId in nonTerminalIds)
			{
				var whereClause = queryBlock.RootNode.GetDescendantsWithinSameQuery(nonTerminalId).FirstOrDefault();
				if (whereClause == null)
					continue;

				var nodeIdentifiers = whereClause.GetDescendantsWithinSameQuery(Terminals.Identifier);
				identifiers = identifiers.Concat(nodeIdentifiers);
			}

			return identifiers;
		}

		private void ResolveSelectList(OracleQueryBlock item)
		{
			var queryBlock = item.RootNode;

			var selectList = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.SelectList).SingleOrDefault();
			if (selectList == null || selectList.FirstTerminalNode == null)
				return;

			if (selectList.FirstTerminalNode.Id == Terminals.Asterisk)
			{
				var asteriskNode = selectList.ChildNodes.Single();
				var column = new OracleSelectListColumn
				{
					RootNode = asteriskNode,
					Owner = item,
					ExplicitDefinition = true,
					IsAsterisk = true
				};

				column.ColumnReferences.Add(CreateColumnReference(item, ColumnReferenceType.SelectList, asteriskNode, null));

				_asteriskTableReferences[column] = new HashSet<OracleTableReference>(item.TableReferences);

				item.Columns.Add(column);
			}
			else
			{
				var columnExpressions = selectList.GetDescendantsWithinSameQuery(NonTerminals.AliasedExpressionOrAllTableColumns).ToArray();
				foreach (var columnExpression in columnExpressions)
				{
					var columnAliasNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();

					var column = new OracleSelectListColumn
					{
						AliasNode = columnAliasNode,
						RootNode = columnExpression,
						Owner = item,
						ExplicitDefinition = true
					};

					_asteriskTableReferences.Add(column, new List<OracleTableReference>());

					var asteriskNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Asterisk).SingleOrDefault();
					if (asteriskNode != null)
					{
						column.IsAsterisk = true;

						var prefixNonTerminal = asteriskNode.ParentNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);
						var columnReference = CreateColumnReference(item, ColumnReferenceType.SelectList, asteriskNode, prefixNonTerminal);
						column.ColumnReferences.Add(columnReference);

						var tableReferences = item.TableReferences.Where(t => t.FullyQualifiedName == columnReference.FullyQualifiedObjectName || (columnReference.TableNode == null && t.FullyQualifiedName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName));
						foreach (var tableReference in tableReferences)
						{
							_asteriskTableReferences[column].Add(tableReference);
						}
					}
					else
					{
						var identifiers = columnExpression.GetDescendantsWithinSameQuery(Terminals.Identifier).ToArray();
						foreach (var identifier in identifiers)
						{
							column.IsDirectColumnReference = columnAliasNode == null && identifier.GetAncestor(NonTerminals.Expression).ChildNodes.Count == 1;
							if (column.IsDirectColumnReference)
							{
								column.AliasNode = identifier;
							}

							var prefixNonTerminal = identifier.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference)
								.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);

							var columnReference = CreateColumnReference(item, ColumnReferenceType.SelectList, identifier, prefixNonTerminal);
							column.ColumnReferences.Add(columnReference);
						}
					}

					item.Columns.Add(column);
				}
			}
		}

		private static OracleColumnReference CreateColumnReference(OracleQueryBlock queryBlock, ColumnReferenceType type, StatementDescriptionNode rootNode, StatementDescriptionNode prefixNonTerminal)
		{
			var columnReference = new OracleColumnReference
			{
				ColumnNode = rootNode,
				Type = type,
				Owner = queryBlock
			};

			if (prefixNonTerminal != null)
			{
				var objectIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.ObjectIdentifier);
				var schemaIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.SchemaIdentifier);

				columnReference.OwnerNode = schemaIdentifier;
				columnReference.TableNode = objectIdentifier;
			}

			return columnReference;
		}

		private IEnumerable<StatementDescriptionNode> GetCommonTableExpressionReferences(StatementDescriptionNode node, string normalizedReferenceName, string sqlText)
		{
			var queryRoot = node.GetAncestor(NonTerminals.NestedQuery, false);
			var subQueryCompondentDistance = node.GetAncestorDistance(NonTerminals.SubqueryComponent);
			if (subQueryCompondentDistance != null &&
			    node.GetAncestorDistance(NonTerminals.NestedQuery) > subQueryCompondentDistance)
			{
				queryRoot = queryRoot.GetAncestor(NonTerminals.NestedQuery, false);
			}

			if (queryRoot == null)
				return Enumerable.Empty<StatementDescriptionNode>();

			var commonTableExpressions = queryRoot
				.GetPathFilterDescendants(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SubqueryComponent)
				.Where(cte => cte.ChildNodes.First().Token.Value.ToQuotedIdentifier() == normalizedReferenceName);
			return commonTableExpressions.Concat(GetCommonTableExpressionReferences(queryRoot, normalizedReferenceName, sqlText));
		}
	}

	public interface IOracleTableReference
	{
		//ICollection<OracleSelectListColumn> Columns { get; }
	}

	public interface IOracleSelectListColumn
	{
		
	}

	public enum TableReferenceType
	{
		PhysicalObject,
		CommonTableExpression,
		NestedQuery
	}

	public enum QueryBlockType
	{
		Normal,
		ScalarSubquery,
		CommonTableExpression
	}
}
