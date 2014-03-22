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
		//private readonly OracleStatement _statement;

		public ICollection<OracleQueryBlock> QueryBlocks
		{
			get { return _queryBlockResults.Values; }
		}

		public OracleStatementSemanticModel(string sqlText, OracleStatement statement, DatabaseModelFake databaseModel)
		{
			if (statement == null)
				throw new ArgumentNullException("statement");

			//_statement = statement;

			_queryBlockResults = statement.NodeCollection.SelectMany(n => n.GetDescendants(NonTerminals.QueryBlock))
				.OrderByDescending(q => q.Level).ToDictionary(n => n, n => new OracleQueryBlock { RootNode = n });

			var allColumnReferences = new Dictionary<OracleSelectListColumn, ICollection<OracleTableReference>>();

			foreach (var queryBlockNode in _queryBlockResults)
			{
				var queryBlock = queryBlockNode.Key;
				var item = queryBlockNode.Value;

				var scalarSubqueryExpression = queryBlock.GetAncestor(NonTerminals.Expression, false);
				if (scalarSubqueryExpression != null)
				{
					item.Type = QueryBlockType.ScalarSubquery;
				}

				var factoredSubqueryReference = queryBlock.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, NonTerminals.SubqueryComponent, false);
				if (factoredSubqueryReference != null)
				{
					item.Alias = factoredSubqueryReference.ChildNodes.First().Token.Value.ToOracleIdentifier();
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
							item.Alias = nestedSubqueryAlias.Token.Value.ToOracleIdentifier();
						}
					}
				}

				var fromClause = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
				var tableReferenceNonterminals = fromClause == null
					? Enumerable.Empty<StatementDescriptionNode>()
					: fromClause.GetDescendantsWithinSameQuery(NonTerminals.TableReference).ToArray();

				foreach (var tableReferenceNonterminal in tableReferenceNonterminals)
				{
					var queryTableExpression = tableReferenceNonterminal.GetDescendantsWithinSameQuery(NonTerminals.QueryTableExpression).Single();

					var tableReferenceAlias = tableReferenceNonterminal.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();
					
					var nestedQueryTableReference = queryTableExpression.GetPathFilterDescendants(f => f.Id != NonTerminals.Subquery, NonTerminals.NestedQuery).SingleOrDefault();
					if (nestedQueryTableReference != null)
					{
						var nestedQueryTableReferenceQueryBlock = nestedQueryTableReference.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery && n.Id != NonTerminals.SubqueryFactoringClause, NonTerminals.QueryBlock).Single();

						item.TableReferences.Add(new OracleTableReference
						{
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

					var tableName = tableIdentifierNode.Token.Value.ToOracleIdentifier();
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
						                         OwnerNode = schemaPrefixNode,
						                         TableNode = tableIdentifierNode,
						                         Type = referenceType,
												 Nodes = commonTableExpressions,
												 AliasNode = tableReferenceAlias,
												 SearchResult = result
					                         });
				}

				var selectList = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.SelectList).SingleOrDefault();
				if (selectList == null)
					continue;

				if (selectList.ChildNodes.Count == 1 && selectList.ChildNodes.Single().Id == Terminals.Asterisk)
				{
					var asteriskNode = selectList.ChildNodes.Single();
					var column = new OracleSelectListColumn
					{
						RootNode = asteriskNode,
						Owner = item,
						ExplicitDefinition = true,
						IsAsterisk = true
					};

					column.ColumnReferences.Add(CreateColumnReference(column, asteriskNode, null));

					allColumnReferences[column] = new HashSet<OracleTableReference>(item.TableReferences);

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

						allColumnReferences.Add(column, new List<OracleTableReference>());

						var asteriskNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Asterisk).SingleOrDefault();
						if (asteriskNode != null)
						{
							column.IsAsterisk = true;

							var prefixNonTerminal = asteriskNode.ParentNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);
							var columnReference = CreateColumnReference(column, asteriskNode, prefixNonTerminal);
							column.ColumnReferences.Add(columnReference);

							var tableReferences = item.TableReferences.Where(t => t.FullyQualifiedName == columnReference.FullyQualifiedObjectName || (columnReference.TableNode == null && t.FullyQualifiedName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName));
							foreach (var tableReference in tableReferences)
							{
								allColumnReferences[column].Add(tableReference);
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

								var columnReference = CreateColumnReference(column, identifier, prefixNonTerminal);
								column.ColumnReferences.Add(columnReference);
							}
						}

						item.Columns.Add(column);
					}
				}
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
						foreach (var cteNode in nestedQueryReference.Nodes)
							nestedQueryReference.QueryBlocks.Add(_queryBlockResults[cteNode.GetDescendantsWithinSameQuery(NonTerminals.QueryBlock).Single()]);
					}
				}
			}

			foreach (var asteriskTableReference in allColumnReferences)
			{
				foreach (var tableReference in asteriskTableReference.Value)
				{
					if (tableReference.Type == TableReferenceType.PhysicalObject)
					{
						var result = databaseModel.GetObject(tableReference.FullyQualifiedName);
						if (result.SchemaObject == null)
							continue;

						foreach (OracleColumn physicalColumn in result.SchemaObject.Columns)
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
				foreach (var columnReference in queryBlock.Columns.SelectMany(c => c.ColumnReferences))
				{
					foreach (var tableReference in queryBlock.TableReferences)
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
								newTableReferences = tableReference.QueryBlocks.Single().Columns
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
		}

		public OracleQueryBlock GetQueryBlock(StatementDescriptionNode node)
		{
			var queryBlockNode = node.GetAncestor(NonTerminals.QueryBlock, false);
			return queryBlockNode == null ? null : _queryBlockResults[queryBlockNode];
		}

		private static OracleColumnReference CreateColumnReference(OracleSelectListColumn owner, StatementDescriptionNode rootNode, StatementDescriptionNode prefixNonTerminal)
		{
			var columnReference = new OracleColumnReference
			{
				ColumnNode = rootNode,
				Owner = owner
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
				.Where(cte => cte.ChildNodes.First().Token.Value.ToOracleIdentifier() == normalizedReferenceName);
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

	public static class NodeFilters
	{
		public static bool BreakAtNestedQueryBoundary(StatementDescriptionNode node)
		{
			return node.Id != NonTerminals.NestedQuery;
		}
	}
}
