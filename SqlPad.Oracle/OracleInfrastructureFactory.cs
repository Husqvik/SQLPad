using System.Configuration;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle
{
	public class OracleInfrastructureFactory : IInfrastructureFactory
	{
		private static readonly OracleDataExportConverter ExportConverter = new OracleDataExportConverter();
		private readonly OracleCommandFactory _commandFactory = new OracleCommandFactory();

		static OracleInfrastructureFactory()
		{
			OracleDatabaseModel.ValidateConfiguration();
		}

		#region Implementation of IInfrastructureFactory
		public string SchemaLabel { get { return "Schema"; } }

		public IDataExportConverter DataExportConverter { get { return ExportConverter; } }

		public ICommandFactory CommandFactory { get { return _commandFactory; } }
		
		public ITokenReader CreateTokenReader(string sqlText)
		{
			return OracleTokenReader.Create(sqlText);
		}

		public ISqlParser CreateParser()
		{
			return new OracleSqlParser();
		}

		public IStatementValidator CreateStatementValidator()
		{
			return new OracleStatementValidator();
		}

		public IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString)
		{
			return OracleDatabaseModel.GetDatabaseModel(connectionString);
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

		public IStatementFormatter CreateSqlFormatter(SqlFormatterOptions options)
		{
			return new OracleStatementFormatter(options);
		}

		public IToolTipProvider CreateToolTipProvider()
		{
			return new OracleToolTipProvider();
		}

		public INavigationService CreateNavigationService()
		{
			return new OracleNavigationService();
		}

		public IExecutionPlanViewer CreateExecutionPlanViewer(IDatabaseModel databaseModel)
		{
			return new ExecutionPlanViewer((OracleDatabaseModelBase)databaseModel);
		}

		public IHelpProvider CreateHelpProvider()
		{
			return new OracleHelpProvider();
		}
		#endregion
	}
}
