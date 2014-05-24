using System.Configuration;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IInfrastructureFactory
	{
		ICommandFactory CommandFactory { get; }

		ITokenReader CreateTokenReader(string sqlText);

		ISqlParser CreateSqlParser();

		IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString);

		IStatementValidator CreateStatementValidator();

		ICodeCompletionProvider CreateCodeCompletionProvider();

		ICodeSnippetProvider CreateSnippetProvider();

		IContextActionProvider CreateContextActionProvider();

		IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider();

		IStatementFormatter CreateSqlFormatter(SqlFormatterOptions options);

		IToolTipProvider CreateToolTipProvider();

		INavigationService CreateNavigationService();
	}

	public interface INavigationService
	{
		int? NavigateToQueryBlockRoot(StatementCollection statementCollection, int currentPosition);
	}

	public interface IMultiNodeEditorDataProvider
	{
		MultiNodeEditorData GetMultiNodeEditorData(IDatabaseModel databaseModel, string sqlText, int currentPosition, int selectionStart, int selectionLength);
	}
}
