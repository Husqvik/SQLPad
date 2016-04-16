using System.Configuration;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.DebugTrace;
using SqlPad.Oracle.ToolTips;

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

		public string SchemaLabel { get; } = "Schema";

		public string DefaultBindVariableType { get; } = OracleGrammarDescription.TerminalValues.Varchar2;

		public IDataExportConverter DataExportConverter => ExportConverter;

	    public ICommandFactory CommandFactory => _commandFactory;

	    public ITokenReader CreateTokenReader(string sqlText)
		{
			return OracleTokenReader.Create(sqlText);
		}

		public ISqlParser CreateParser()
		{
			return OracleSqlParser.Instance;
		}

		public IStatementValidator CreateStatementValidator()
		{
			return new OracleStatementValidator();
		}

		public IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString, string identifier)
		{
			return OracleDatabaseModel.GetDatabaseModel(connectionString, identifier);
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
			return new OracleContextActionProvider(new OracleCommandSettingsProviderFactory());
		}

		public IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider()
		{
			return new OracleMultiNodeEditorDataProvider();
		}

		public IStatementFormatter CreateSqlFormatter()
		{
			return new OracleStatementFormatter();
		}

		public IToolTipProvider CreateToolTipProvider()
		{
			return new OracleToolTipProvider();
		}

		public INavigationService CreateNavigationService()
		{
			return new OracleNavigationService();
		}

		public IExecutionPlanViewer CreateExecutionPlanViewer(OutputViewer outputViewer)
		{
			return new ExecutionPlanViewer(outputViewer);
		}

		public ITraceViewer CreateTraceViewer(IConnectionAdapter connectionAdapter)
		{
			return new OracleTraceViewer((OracleConnectionAdapterBase)connectionAdapter);
		}

		public IHelpProvider CreateHelpProvider()
		{
			return new OracleHelpProvider();
		}

		public IDatabaseMonitor CreateDatabaseMonitor(ConnectionStringSettings connectionString)
		{
			return new OracleDatabaseMonitor(connectionString);
		}

		public IValueAggregator CreateValueAggregator()
		{
			return new OracleValueAggregator();
		}
		#endregion
	}
}
