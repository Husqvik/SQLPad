using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NonTerminals = SqlPad.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.OracleGrammarDescription.Terminals;

namespace SqlPad
{
	public class OracleSemanticValidator
	{
		public SemanticModel Validate(OracleStatement statement, DatabaseModelFake databaseModel)
		{
			var model = new SemanticModel();

			var queryTableExpressions = statement.NodeCollection.SelectMany(t => t.GetDescendants(NonTerminals.QueryTableExpression)).ToList();
			foreach (var node in queryTableExpressions)
			{
				string owner;
				if (node.ChildNodes.Count == 2)
				{
					var ownerNode = node.GetDescendants(Terminals.SchemaIdentifier).First();
					owner = ownerNode.Token.Value;
					var objectNameNode = node.GetDescendants(Terminals.Identifier).First();

					model.NodeValidity[ownerNode] = databaseModel.Schemas.Any(s => s == owner.ToOracleIdentifier());
					model.NodeValidity[objectNameNode] = databaseModel.AllObjects.ContainsKey(OracleObjectIdentifier.Create(owner, objectNameNode.Token.Value));

				}
				else // TODO: Resolve if the identifier is a query name or an object name.
				{
					var objectNameNode = node.GetDescendants(Terminals.Identifier).FirstOrDefault();
					if (objectNameNode == null)
						continue;

					var factoredSubquery = node.GetAncestor(NonTerminals.SubqueryComponent);
					var factoredQueryExists = factoredSubquery == null &&
					                          node.GetAncestor(NonTerminals.NestedQuery)
						                          .GetDescendants(NonTerminals.SubqueryComponent)
						                          .SelectMany(s => s.ChildNodes).Where(n => n.Id == Terminals.Identifier)
												  .Select(i => i.Token.Value.ToOracleIdentifier()).Contains(objectNameNode.Token.Value.ToOracleIdentifier());

					owner = databaseModel.CurrentSchema;
					var currentSchemaObject = OracleObjectIdentifier.Create(owner, objectNameNode.Token.Value);
					var publicSchemaObject = OracleObjectIdentifier.Create(DatabaseModelFake.SchemaPublic, objectNameNode.Token.Value);

					var objectIsValid = databaseModel.AllObjects.ContainsKey(currentSchemaObject) || databaseModel.AllObjects.ContainsKey(publicSchemaObject) ||
						factoredQueryExists;

					model.NodeValidity[objectNameNode] = objectIsValid;
				}
			}

			return model;
		}

		public void ResolveReferences(string sqlText, OracleStatement statement, DatabaseModelFake databaseModel)
		{
			var model = new OracleStatementSemanticModel(sqlText, statement);
		}
	}

	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode})")]
	public class OracleQueryBlock
	{
		public string Alias { get; set; }

		public QueryBlockType Type { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public ICollection<OracleTableReference> TableReferences { get; set; }

		public ICollection<OracleSelectListColumn> ColumnReferences { get; set; }
	}

	[DebuggerDisplay("OracleTableReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={TableNameNode.Token.Value}; Type={Type})")]
	public class OracleTableReference
	{
		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNameNode { get; set; }

		public ICollection<StatementDescriptionNode> Nodes { get; set; }

		public TableReferenceType Type { get; set; }
	}

	public class OracleSelectListColumn
	{
	}

	public enum TableReferenceType
	{
		PhysicalTable,
		Subquery
	}

	public enum QueryBlockType
	{
		Normal,
		ScalarSubquery,
		CommonTableExpression
	}

	public class OracleStatementSemanticModel
	{
		private readonly OracleStatement _statement;

		public OracleStatementSemanticModel(string sqlText, OracleStatement statement)
		{
			if (statement == null)
				throw new ArgumentNullException("statement");

			var queryBlockResults = new List<OracleQueryBlock>();

			_statement = statement;

			var queryBlocks = statement.NodeCollection.SelectMany(n => n.GetDescendants(NonTerminals.QueryBlock))
				.OrderByDescending(q => q.Level).ToArray();

			foreach (var queryBlock in queryBlocks)
			{
				var item = new OracleQueryBlock
				           {
					           TableReferences = new List<OracleTableReference>(),
							   RootNode = queryBlock
				           };
				
				queryBlockResults.Add(item);

				var fromClause = queryBlock.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery, NonTerminals.FromClause).First();
				//var fromClause = queryBlock.GetDescendants(OracleGrammarDescription.NonTerminals.FromClause).First();
				var tableReferenceNonterminals = fromClause.GetPathFilterDescendants(nq => nq.Id != NonTerminals.NestedQuery, NonTerminals.TableReference).ToArray();

				var scalarSubqueryExpression = queryBlock.GetAncestor(NonTerminals.Expression, false);
				if (scalarSubqueryExpression != null)
				{
					item.Type = QueryBlockType.ScalarSubquery;
				}

				var factoredSubqueryReference = queryBlock.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.SubqueryComponent, false);
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

				foreach (var tableReferenceNonterminal in tableReferenceNonterminals)
				{
					var tableReferenceAliases = tableReferenceNonterminal.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery, Terminals.Alias).ToArray();
					if (tableReferenceAliases.Length > 0)
					{
						var tableReferenceAliasString = tableReferenceAliases[0].Token.Value.ToOracleIdentifier();
					}
					
					var queryTableExpression = tableReferenceNonterminal.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery, NonTerminals.QueryTableExpression).Single();
					var tableIdentifierNode = queryTableExpression.ChildNodes.FirstOrDefault(n => n.Id == Terminals.Identifier);

					if (tableIdentifierNode == null)
						continue;
					
					var schemaPrefixNode = queryTableExpression.ChildNodes.FirstOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
					if (schemaPrefixNode != null)
					{
						schemaPrefixNode = schemaPrefixNode.ChildNodes.First();
					}

					var tableName = tableIdentifierNode.Token.Value.ToOracleIdentifier();
					var commonTableExpressions = GetCommonTableExpressionReferences(queryBlock, tableName, sqlText).ToArray();
					var referenceType = commonTableExpressions.Length > 0 ? TableReferenceType.Subquery : TableReferenceType.PhysicalTable;

					item.TableReferences.Add(new OracleTableReference { TableNameNode = tableIdentifierNode, OwnerNode = schemaPrefixNode, Type = referenceType, Nodes = commonTableExpressions });
				}

				var selectList = queryBlock.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery, NonTerminals.SelectList).Single();
				var columnExpressions = selectList.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery, NonTerminals.AliasedExpressionOrAllTableColumns).ToArray();
				foreach (var columnExpression in columnExpressions)
				{
					var identifiers = columnExpression.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery, Terminals.ObjectIdentifier).ToArray();
				}
			}
		}

		private IEnumerable<StatementDescriptionNode> GetNestedTableReferences(StatementDescriptionNode node, string normalizedReferenceName)
		{
			return null;
		}

		private IEnumerable<StatementDescriptionNode> GetCommonTableExpressionReferences(StatementDescriptionNode node, string normalizedReferenceName, string sqlText)
		{
			var nestedQueryRoot = node.GetAncestor(NonTerminals.NestedQuery, false);
			if (nestedQueryRoot == null)
				return Enumerable.Empty<StatementDescriptionNode>();

			var commonTableExpressions = nestedQueryRoot
				.GetPathFilterDescendants(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SubqueryComponent)
				.Where(cte => cte.ChildNodes.First().Token.Value.ToOracleIdentifier() == normalizedReferenceName);
			return commonTableExpressions.Concat(GetCommonTableExpressionReferences(nestedQueryRoot, normalizedReferenceName, sqlText));
		}
	}

	public class SemanticModel
	{
		public Dictionary<StatementDescriptionNode, bool> NodeValidity = new Dictionary<StatementDescriptionNode, bool>();
	}
}