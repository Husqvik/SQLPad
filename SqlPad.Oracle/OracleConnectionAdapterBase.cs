using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle
{
	public abstract class OracleConnectionAdapterBase : IConnectionAdapter
	{
		public virtual void Dispose() { }

		public abstract bool CanFetch { get; }

		public abstract bool IsExecuting { get; }

		public abstract bool EnableDatabaseOutput { get; set; }

		public abstract string Identifier { get; set; }

		public abstract Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<object[]>> FetchRecordsAsync(int rowCount, CancellationToken cancellationToken);

		public abstract bool HasActiveTransaction { get; }

		public abstract void CommitTransaction();

		public abstract Task RollbackTransaction();

		public abstract void CloseActiveReader();

		public abstract Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken);

		public abstract Task ActivateTraceEvents(IEnumerable<OracleTraceEvent> traceEvents, CancellationToken cancellationToken);

		public abstract Task StopTraceEvents(CancellationToken cancellationToken);

		public abstract string TraceFileName { get; }
	}
}
