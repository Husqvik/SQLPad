using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public class OracleSemanticValidator
	{
		public SemanticModel Validate(OracleStatement statement, DatabaseModelFake databaseModel)
		{
			var model = new SemanticModel();

			var queryTableExpressions = statement.NodeCollection.SelectMany(t => t.GetDescendants(OracleGrammarDescription.NonTerminals.QueryTableExpression)).ToList();
			foreach (var node in queryTableExpressions)
			{
				string owner;
				if (node.ChildNodes.Count == 2)
				{
					var ownerNode = node.GetDescendants(OracleGrammarDescription.Terminals.Identifier).First();
					owner = ownerNode.Token.Value;
					var objectNameNode = node.GetDescendants(OracleGrammarDescription.Terminals.Identifier).Last();

					model.NodeValidity[ownerNode] = databaseModel.Schemas.Any(s => s == owner.ToOracleIdentifier());
					model.NodeValidity[objectNameNode] = databaseModel.AllObjects.ContainsKey(OracleObjectIdentifier.Create(owner, objectNameNode.Token.Value));

				}
				else // TODO: Resolve if the identifier is a query name or an object name.
				{
					var objectNameNode = node.GetDescendants(OracleGrammarDescription.Terminals.Identifier).FirstOrDefault();
					if (objectNameNode == null)
						continue;

					var factoredSubquery = node.GetAncestor(OracleGrammarDescription.NonTerminals.SubqueryComponent);
					var factoredQueryExists = factoredSubquery == null &&
					                          node.GetAncestor(OracleGrammarDescription.NonTerminals.NestedQuery)
						                          .GetDescendants(OracleGrammarDescription.NonTerminals.SubqueryComponent)
						                          .SelectMany(s => s.ChildNodes).Where(n => n.Id == OracleGrammarDescription.Terminals.Identifier)
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
			var factoredSubqueries = statement.NodeCollection.SelectMany(n => n.GetDescendants(OracleGrammarDescription.NonTerminals.SubqueryComponent))
				.SelectMany(s => s.GetDescendants(OracleGrammarDescription.NonTerminals.Subquery)).Distinct().ToArray();
			var nestedSubqueries = statement.NodeCollection.SelectMany(n => n.GetDescendants(OracleGrammarDescription.NonTerminals.NestedQuery)).ToArray();
			var scalarSubqueries = nestedSubqueries.Where(n => n.HasAncestor(OracleGrammarDescription.NonTerminals.Expression)).ToArray();
			var references = statement.NodeCollection.SelectMany(n => n.GetDescendants(OracleGrammarDescription.Terminals.Identifier, OracleGrammarDescription.Terminals.Alias)).ToArray();
			var selectListIdentifiers = references.Where(r => r.HasAncestor(OracleGrammarDescription.NonTerminals.SelectList)).ToArray();
			var tableReferences = references.Where(r => r.HasAncestor(OracleGrammarDescription.NonTerminals.TableReference)).ToArray();

			var fs = factoredSubqueries.ToDictionary(q => q, q => sqlText.Substring(q.SourcePosition.IndexStart, q.SourcePosition.IndexEnd - q.SourcePosition.IndexStart + 1));
			var ns = nestedSubqueries.ToDictionary(q => q, q => sqlText.Substring(q.SourcePosition.IndexStart, q.SourcePosition.IndexEnd - q.SourcePosition.IndexStart + 1));
		}
	}

	public class SemanticModel
	{
		public Dictionary<StatementDescriptionNode, bool> NodeValidity = new Dictionary<StatementDescriptionNode, bool>();
	}
}