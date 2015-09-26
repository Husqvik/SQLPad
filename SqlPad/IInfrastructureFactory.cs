using System.Collections.Generic;
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

		IExecutionPlanViewer CreateExecutionPlanViewer(OutputViewer outputViewer);
		
		ITraceViewer CreateTraceViewer(IConnectionAdapter connectionAdapter);

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
		int? NavigateToQueryBlockRoot(ActionExecutionContext executionContext);

		int? NavigateToDefinition(ActionExecutionContext executionContext);

		IReadOnlyCollection<SourcePosition> FindCorrespondingSegments(ActionExecutionContext executionContext);

		void FindUsages(ActionExecutionContext executionContext);

		void DisplayBindVariableUsages(ActionExecutionContext executionContext);
	}

	public interface IMultiNodeEditorDataProvider
	{
		MultiNodeEditorData GetMultiNodeEditorData(ActionExecutionContext executionContext);
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
