using System.Linq;

namespace SqlPad
{
	public class OracleSemanticValidator
	{
		public SemanticModel Validate(OracleStatement statement, DatabaseModelFake databaseModel)
		{
			var model = new SemanticModel();

			var queryTableExpressions = statement.TokenCollection.SelectMany(t => t.GetDescendants(OracleGrammarDescription.NonTerminals.QueryTableExpression)).ToList();

			return model;
		}
	}

	public class SemanticModel
	{
		
	}
}