using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		string DefaultBindVariableType { get; }

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

		IDatabaseMonitor CreateDatabaseMonitor(ConnectionStringSettings connectionString);
	}

	public interface IDatabaseMonitor
	{
		Task<DatabaseSessions> GetAllSessionDataAsync(CancellationToken cancellationToken);

		IDatabaseSessionDetailViewer CreateSessionDetailViewer();

		IEnumerable<ContextAction> GetSessionContextActions(DatabaseSession databaseSession);
	}

	public interface IDatabaseSessionDetailViewer
	{
		Control Control { get; }

		DatabaseSession DatabaseSession { get; }

		bool AutoRefreshEnabled { get; set; }

		Task Initialize(DatabaseSession databaseSession, CancellationToken cancellationToken);

		Task Refresh(CancellationToken cancellationToken);

		void Shutdown();
	}

	public struct DatabaseSessions
	{
		public IReadOnlyList<ColumnHeader> ColumnHeaders { get; set; }

		public IReadOnlyList<DatabaseSession> Rows { get; set; }
	}

	public class DatabaseSession : ModelBase
	{
		private bool _isActive;
		private SessionType _type;
		private IDatabaseSessionValues _providerValues;

		public ObservableCollection<DatabaseSession> ChildSessions { get; } = new ObservableCollection<DatabaseSession>();

		public DatabaseSession Owner { get; set; }

		public int Id { get; set; }

		public SessionType Type
		{
			get { return _type; }
			set { UpdateValueAndRaisePropertyChanged(ref _type, value); }
		}

		public bool IsActive
		{
			get { return _isActive; }
			set { UpdateValueAndRaisePropertyChanged(ref _isActive, value); }
		}

		public IDatabaseSessionValues ProviderValues
		{
			get { return _providerValues; }
			set
			{
				_providerValues = value;
				RaisePropertyChanged(nameof(Values));
			}
		}

		public object[] Values => _providerValues.Values;
	}

	public interface IDatabaseSessionValues
	{
		object[] Values { get; }
	}

	public enum SessionType
	{
		System,
		User
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
