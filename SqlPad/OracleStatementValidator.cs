using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public class OracleStatementValidator
	{
		public OracleValidationModel ResolveReferences(string sqlText, OracleStatement statement, DatabaseModelFake databaseModel)
		{
			var semanticModel = new OracleStatementSemanticModel(sqlText, statement);

			var validationModel = new OracleValidationModel();

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.TableReferences))
			{
				if (tableReference.Type == TableReferenceType.CommonTableExpression)
				{
					validationModel.NodeValidity[tableReference.TableNode] = true;
					continue;
				}

				var objectName = tableReference.TableNode.Token.Value;
				bool objectValid;
				if (tableReference.OwnerNode != null)
				{
					var owner = tableReference.OwnerNode.Token.Value;
					validationModel.NodeValidity[tableReference.OwnerNode] = databaseModel.Schemas.Any(s => s == owner.ToOracleIdentifier());
					objectValid = databaseModel.AllObjects.ContainsKey(OracleObjectIdentifier.Create(owner, objectName));
				}
				else
				{
					var currentSchemaObject = OracleObjectIdentifier.Create(databaseModel.CurrentSchema, objectName);
					var publicSchemaObject = OracleObjectIdentifier.Create(DatabaseModelFake.SchemaPublic, objectName);
					objectValid = databaseModel.AllObjects.ContainsKey(currentSchemaObject) || databaseModel.AllObjects.ContainsKey(publicSchemaObject);
				}

				validationModel.NodeValidity[tableReference.TableNode] = objectValid;
			}

			return validationModel;
		}
	}

	public class OracleValidationModel
	{
		public Dictionary<StatementDescriptionNode, bool> NodeValidity = new Dictionary<StatementDescriptionNode, bool>();
	}
}