using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IInfrastructureFactory
	{
		ICommandFactory CommandFactory { get; }

		ITokenReader CreateTokenReader(string sqlText);

		ISqlParser CreateParser();

		IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString);

		IStatementValidator CreateStatementValidator();

		ICodeCompletionProvider CreateCodeCompletionProvider();

		ICodeSnippetProvider CreateSnippetProvider();

		IContextActionProvider CreateContextActionProvider();

		IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider();

		IStatementFormatter CreateSqlFormatter(SqlFormatterOptions options);

		IToolTipProvider CreateToolTipProvider();

		INavigationService CreateNavigationService();

		IExecutionPlanViewer CreateExecutionPlanViewer(IDatabaseModel databaseModel);
	}

	public interface INavigationService
	{
		int? NavigateToQueryBlockRoot(SqlDocumentRepository documentRepository, int currentPosition);

		int? NavigateToDefinition(SqlDocumentRepository documentRepository, int currentPosition);
	}

	public interface IMultiNodeEditorDataProvider
	{
		MultiNodeEditorData GetMultiNodeEditorData(IDatabaseModel databaseModel, string sqlText, int currentPosition, int selectionStart, int selectionLength);
	}

	public interface IExecutionPlanViewer
	{
		Task<ActionResult> ExplainAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Control Control { get; }
	}
}
