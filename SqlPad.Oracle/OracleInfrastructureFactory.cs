using System.Configuration;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

namespace SqlPad.Oracle
{
	public class OracleInfrastructureFactory : IInfrastructureFactory
	{
		private readonly OracleCommandFactory _commandFactory = new OracleCommandFactory();
		private readonly OracleSqlParser _parser = new OracleSqlParser();

		#region Implementation of IInfrastructureFactory
		public ICommandFactory CommandFactory { get { return _commandFactory; } }
		
		public ITokenReader CreateTokenReader(string sqlText)
		{
			return OracleTokenReader.Create(sqlText);
		}

		public ISqlParser CreateSqlParser()
		{
			return new OracleSqlParser();
		}

		public IStatementValidator CreateStatementValidator()
		{
			return new OracleStatementValidator();
		}

		public IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString)
		{
			return DatabaseModelFake.Instance;
		}

		public ICodeCompletionProvider CreateCodeCompletionProvider()
		{
			return new OracleCodeCompletionProvider();
		}

		public ICodeSnippetProvider CreateSnippetProvider()
		{
			return new OracleSnippetProvider();
		}

		public IContextActionProvider CreateContextActionProvider()
		{
			return new OracleContextActionProvider();

		}

		public IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider()
		{
			return new OracleMultiNodeEditorDataProvider();
		}
		#endregion
	}
}
