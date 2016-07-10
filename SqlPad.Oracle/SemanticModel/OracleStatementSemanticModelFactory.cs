using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.SemanticModel
{
	public static class OracleStatementSemanticModelFactory
	{
		public static OracleStatementSemanticModel Build(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
		{
			return BuildInternal(statementText, statement, databaseModel, CancellationToken.None);
		}

		private static OracleStatementSemanticModel BuildInternal(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel, CancellationToken cancellationToken)
		{
			var semanticModel = statement != null && statement.IsPlSql
				? new OraclePlSqlStatementSemanticModel(statementText, statement, databaseModel)
				: new OracleStatementSemanticModel(statementText, statement, databaseModel);
			return semanticModel.Build(cancellationToken);
		}

		public static Task<OracleStatementSemanticModel> BuildAsync(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel, CancellationToken cancellationToken)
		{
			return Task.Run(() => BuildInternal(statementText, statement, databaseModel, cancellationToken), cancellationToken);
		}
	}
}
