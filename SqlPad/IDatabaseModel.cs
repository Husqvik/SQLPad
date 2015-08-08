using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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

		bool CanFetch(ResultInfo resultInfo);

		bool IsExecuting { get; }

		bool EnableDatabaseOutput { get; set; }

		string Identifier { get; set; }

		Task<StatementExecutionBatchResult> ExecuteStatementAsync(StatementBatchExecutionModel executionModel, CancellationToken cancellationToken);

		Task<StatementExecutionResult> ExecuteChildStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		Task<IReadOnlyList<object[]>> FetchRecordsAsync(ResultInfo resultInfo, int rowCount, CancellationToken cancellationToken);

		bool HasActiveTransaction { get; }

		Task CommitTransaction();

		Task RollbackTransaction();
	}

	public class ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }

		public IReadOnlyCollection<IReferenceDataSource> ParentReferenceDataSources { get; set; }

		public override string ToString()
		{
			return Name;
		}
	}

	public interface IReferenceDataSource
	{
		IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

		string ObjectName { get; }

		string ConstraintName { get; }

		StatementExecutionModel CreateExecutionModel(object[] keys);
	}

	public class DatabaseModelConnectionErrorArgs : EventArgs
	{
		public Exception Exception { get; private set; }

		public DatabaseModelConnectionErrorArgs(Exception exception)
		{
			if (exception == null)
			{
				throw new ArgumentNullException(nameof(exception));
			}
			
			Exception = exception;
		}
	}

	[DebuggerDisplay("ResultInfo (ResultIdentifier={ResultIdentifier}; Type={Type})")]
	public struct ResultInfo
	{
		public readonly string Title;
		public readonly string ResultIdentifier;
		public readonly ResultIdentifierType Type;

		public ResultInfo(string resultIdentifier, string title, ResultIdentifierType type)
		{
			ResultIdentifier = resultIdentifier;
			Title = title;
			Type = type;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is ResultInfo && Equals((ResultInfo)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((ResultIdentifier?.GetHashCode() ?? 0) * 397) ^ Type.GetHashCode();
			}
		}

		private bool Equals(ResultInfo other)
		{
			return string.Equals(ResultIdentifier, other.ResultIdentifier) && Type == other.Type;
		}
	}

	public enum ResultIdentifierType
	{
		SystemGenerated,
		UserDefined
	}
}
