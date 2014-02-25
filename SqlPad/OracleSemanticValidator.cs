using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public class OracleSemanticValidator
	{
		public SemanticModel Validate(OracleStatement statement, DatabaseModelFake databaseModel)
		{
			var model = new SemanticModel();

			var queryTableExpressions = statement.TokenCollection.SelectMany(t => t.GetDescendants(OracleGrammarDescription.NonTerminals.QueryTableExpression)).ToList();
			foreach (var node in queryTableExpressions)
			{
				StatementDescriptionNode objectNameNode;
				string owner;
				if (node.ChildNodes.Count == 2)
				{
					var ownerNode = node.GetDescendants(OracleGrammarDescription.Terminals.Identifier).First();
					owner = ownerNode.Token.Value;
					objectNameNode = node.GetDescendants(OracleGrammarDescription.Terminals.Identifier).Last();

					model.NodeValidity.Add(ownerNode, databaseModel.Schemas.Any(s => s == owner.ToOracleIdentifier()));
					model.NodeValidity.Add(objectNameNode, databaseModel.AllObjects.ContainsKey(OracleObjectIdentifier.Create(owner, objectNameNode.Token.Value)));

				}
				else // TODO: Resolve if the identifier is a query name or an object name.
				{
					owner = databaseModel.CurrentSchema;
					objectNameNode = node.GetDescendants(OracleGrammarDescription.Terminals.Identifier).Single();
					var currentSchemaObject = OracleObjectIdentifier.Create(owner, objectNameNode.Token.Value);
					var publicSchemaObject = OracleObjectIdentifier.Create(DatabaseModelFake.SchemaPublic, objectNameNode.Token.Value);
					model.NodeValidity.Add(objectNameNode, databaseModel.AllObjects.ContainsKey(currentSchemaObject) || databaseModel.AllObjects.ContainsKey(publicSchemaObject));
				}
			}

			return model;
		}
	}

	public class SemanticModel
	{
		public Dictionary<StatementDescriptionNode, bool> NodeValidity = new Dictionary<StatementDescriptionNode, bool>();
	}
}