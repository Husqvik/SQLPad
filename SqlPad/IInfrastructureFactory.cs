using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IInfrastructureFactory
	{
		string SchemaLabel { get; }

		IDataExportConverter DataExportConverter { get; }

		ICommandFactory CommandFactory { get; }

		ITokenReader CreateTokenReader(string sqlText);

		ISqlParser CreateParser();

		IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString, string identifier);

		IStatementValidator CreateStatementValidator();

		ICodeCompletionProvider CreateCodeCompletionProvider();

		ICodeSnippetProvider CreateSnippetProvider();

		IContextActionProvider CreateContextActionProvider();

		IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider();

		IStatementFormatter CreateSqlFormatter(SqlFormatterOptions options);

		IToolTipProvider CreateToolTipProvider();

		INavigationService CreateNavigationService();

		IExecutionPlanViewer CreateExecutionPlanViewer(IDatabaseModel databaseModel);
		
		ITraceViewer CreateTraceViewer(IDatabaseModel databaseModel);

		IHelpProvider CreateHelpProvider();
	}

	public interface IDataExportConverter
	{
		string ToColumnName(string columnHeader);

		string ToSqlValue(object value);

		string ToXml(object value);

		string ToJson(object value);
	}

	public interface INavigationService
	{
		int? NavigateToQueryBlockRoot(CommandExecutionContext executionContext);

		int? NavigateToDefinition(CommandExecutionContext executionContext);

		void FindUsages(CommandExecutionContext executionContext);
	}

	public interface IMultiNodeEditorDataProvider
	{
		MultiNodeEditorData GetMultiNodeEditorData(IDatabaseModel databaseModel, string sqlText, int currentPosition, int selectionStart, int selectionLength);
	}

	public interface IExecutionPlanViewer
	{
		Task ExplainAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Task ShowActualAsync(IConnectionAdapter connectionAdapter, CancellationToken cancellationToken);

		Control Control { get; }
	}

	public interface ITraceViewer
	{
		Control Control { get; }
	}
}
