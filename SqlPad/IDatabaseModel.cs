using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IDatabaseModel : IDisposable
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; set; }

		bool IsInitialized { get; }

		ICollection<string> Schemas { get; }

		bool IsFresh { get; }

		void RefreshIfNeeded();

		Task Initialize();

		Task Refresh(bool force);

		event EventHandler Initialized;

		event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		event EventHandler RefreshStarted;

		event EventHandler RefreshCompleted;

		IConnectionAdapter CreateConnectionAdapter();
	}

	public interface IConnectionAdapter : IDisposable
	{
		IDatabaseModel DatabaseModel { get; }
		
		bool CanFetch { get; }

		bool IsExecuting { get; }

		bool EnableDatabaseOutput { get; set; }

		string Identifier { get; set; }

		Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		Task<IReadOnlyList<object[]>> FetchRecordsAsync(int rowCount, CancellationToken cancellationToken);

		bool HasActiveTransaction { get; }

		void CommitTransaction();

		Task RollbackTransaction();

		void CloseActiveReader();
	}

	public class ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }
		
		public StatementExecutionModel FetchReferenceDataExecutionModel { get; set; }

		public override string ToString()
		{
			return Name;
		}
	}

	public class DatabaseModelConnectionErrorArgs : EventArgs
	{
		public Exception Exception { get; private set; }

		public DatabaseModelConnectionErrorArgs(Exception exception)
		{
			if (exception == null)
			{
				throw new ArgumentNullException("exception");
			}
			
			Exception = exception;
		}
	}
}
