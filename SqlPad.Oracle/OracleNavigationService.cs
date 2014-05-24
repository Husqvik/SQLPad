namespace SqlPad.Oracle
{
	public class OracleNavigationService : INavigationService
	{
		public int? NavigateToQueryBlockRoot(StatementCollection statementCollection, int currentPosition)
		{
			var statement = statementCollection.GetStatementAtPosition(currentPosition);
			if (statement == null)
				return null;

			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)statement);
			var queryBlock = semanticModel.GetQueryBlock(currentPosition);
			return queryBlock == null ? null : (int?)queryBlock.RootNode.SourcePosition.IndexStart;
		}
	}
}
