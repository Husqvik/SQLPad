using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IConnectionAdapter : IDisposable
	{
		IDatabaseModel DatabaseModel { get; }

		IDebuggerSession DebuggerSession { get; }

		bool CanFetch(ResultInfo resultInfo);

		bool IsExecuting { get; }

		bool EnableDatabaseOutput { get; set; }

		string Identifier { get; set; }

		Task<StatementExecutionBatchResult> ExecuteStatementAsync(StatementBatchExecutionModel executionModel, CancellationToken cancellationToken);

		Task<StatementExecutionResult> ExecuteChildStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Task RefreshResult(StatementExecutionResult result, CancellationToken cancellationToken);

		Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		Task<IReadOnlyList<object[]>> FetchRecordsAsync(ResultInfo resultInfo, int rowCount, CancellationToken cancellationToken);

		bool HasActiveTransaction { get; }

		string TransanctionIdentifier { get; }

		Task CommitTransaction();

		Task RollbackTransaction();
	}
}
