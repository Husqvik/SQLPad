using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IValidationModel ResolveReferences(string sqlText, IStatement statement, IDatabaseModel databaseModel)
		{
			var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)statement);

			var validationModel = new OracleValidationModel();

			foreach (var columnReference in semanticModel.QueryBlocks.SelectMany(qb => qb.Columns.SelectMany(c => c.ColumnReferences)))
			{
				//columnReference.
			}

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.TableReferences).Where(tr => tr.Type != TableReferenceType.NestedQuery))
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

		private ICollection<OracleColumnReference> ResolveTableColumn(OracleTableReference tableReference)
		{
			return null;
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementDescriptionNode, bool> _nodeValidity = new Dictionary<StatementDescriptionNode, bool>();

		public IDictionary<StatementDescriptionNode, bool> NodeValidity { get { return _nodeValidity; } }
	}
}