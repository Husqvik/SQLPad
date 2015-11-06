using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.DatabaseConnection
{
	public abstract class OracleConnectionAdapterBase : IConnectionAdapter
	{
		public abstract IDatabaseModel DatabaseModel { get; }

		public abstract IDebuggerSession DebuggerSession { get; }

		public virtual void Dispose() { }

		public abstract bool CanFetch(ResultInfo resultInfo);

		public abstract bool IsExecuting { get; }

		public abstract bool EnableDatabaseOutput { get; set; }

		public abstract string Identifier { get; set; }

		public abstract Task<StatementExecutionBatchResult> ExecuteStatementAsync(StatementBatchExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract Task<StatementExecutionResult> ExecuteChildStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<ColumnHeader>> RefreshResult(ResultInfo resultInfo, CancellationToken cancellationToken);

		public abstract Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<object[]>> FetchRecordsAsync(ResultInfo resultInfo, int rowCount, CancellationToken cancellationToken);

		public abstract bool HasActiveTransaction { get; }

		public abstract string TransanctionIdentifier { get; }

		public abstract Task CommitTransaction();

		public abstract Task RollbackTransaction();

		public abstract Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken);

		public abstract Task ActivateTraceEvents(IEnumerable<OracleTraceEvent> traceEvents, string traceIdentifier, CancellationToken cancellationToken);

		public abstract Task StopTraceEvents(CancellationToken cancellationToken);

		public abstract string TraceFileName { get; }

		public abstract int? SessionId { get; }
	}
}
