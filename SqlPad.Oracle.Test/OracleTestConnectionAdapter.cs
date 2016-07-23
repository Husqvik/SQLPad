using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.DebugTrace;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.Test
{
	internal class OracleTestConnectionAdapter : OracleConnectionAdapterBase
	{
		public static readonly OracleTestConnectionAdapter Instance = new OracleTestConnectionAdapter();

		private static readonly IReadOnlyList<ColumnHeader> ColumnHeaders =
			new List<ColumnHeader>
			{
				new ColumnHeader
				{
					ColumnIndex = 0,
					DataType = typeof (String),
					DatabaseDataType = TerminalValues.Varchar2,
					Name = "DUMMY"
				}
			}.AsReadOnly();

		private int _generatedRowCount;

		public override bool CanFetch(ResultInfo resultInfo)
		{
			return true;
		}

		public override bool IsExecuting { get; } = false;

		public override bool EnableDatabaseOutput { get; set; }

		public override string Identifier { get; set; }

		public override IDatabaseModel DatabaseModel { get; } = OracleTestDatabaseModel.Instance;

		public override IDebuggerSession DebuggerSession { get; } = null;

		public override string TraceFileName { get; } = "OracleTestTraceFile.trc";

		public override SessionIdentifier? SessionIdentifier { get; } = new SessionIdentifier(1, 123);

		public override Task RefreshResult(StatementExecutionResult result, CancellationToken cancellationToken)
		{
			return Task.FromResult(result);
		}

		public override Task<StatementExecutionBatchResult> ExecuteStatementAsync(StatementBatchExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var result =
				new StatementExecutionBatchResult
				{
					ExecutionModel = executionModel,
					DatabaseOutput = "Test database output",
					StatementResults = executionModel.Statements.Select(BuildStatementExecutionResult).ToArray()
				};

			return Task.FromResult(result);
		}

		private static StatementExecutionResult BuildStatementExecutionResult(StatementExecutionModel statement)
		{
			return
				new StatementExecutionResult
				{
					StatementModel = statement,
					ExecutedAt = DateTime.Now,
					Duration = TimeSpan.FromMilliseconds(1),
					Exception = null,
					SuccessfulExecutionMessage = OracleStatement.DefaultMessageCommandExecutedSuccessfully,
					CompilationErrors =
						new[]
						{
							new CompilationError { Code = 942, Column = 999, Line = 999, Message = "table or view does not exist", ObjectName = "TEST_OBJECT", ObjectType = "TEST_TYPE", Severity = "WARNING", Statement = statement.Statement }
						},
					ResultInfoColumnHeaders =
						new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>
						{
							{ new ResultInfo(null, "Test result set", ResultIdentifierType.UserDefined), ColumnHeaders }
						}
				};
		}

		public override Task<StatementExecutionResult> ExecuteChildStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			return Task.FromResult(BuildStatementExecutionResult(executionModel));
		}

		public override Task ActivateTraceEvents(IEnumerable<OracleTraceEvent> traceEvents, string traceIdentifier, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public override Task StopTraceEvents(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public override Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			ICollection<SessionExecutionStatisticsRecord> statistics =
				new[]
				{
					new SessionExecutionStatisticsRecord { Name = OracleTestDatabaseModel.StatisticsDescriptionBytesReceivedViaSqlNetFromClient, Value = 124 },
					new SessionExecutionStatisticsRecord { Name = OracleTestDatabaseModel.StatisticsDescriptionBytesSentViaSqlNetToClient, Value = 24316 },
					new SessionExecutionStatisticsRecord { Name = OracleTestDatabaseModel.StatisticsDescriptionConsistentGets, Value = 16 },
					new SessionExecutionStatisticsRecord { Name = OracleTestDatabaseModel.StatisticsDescriptionPhysicalReadTotalBytes, Value = 1336784 },
					new SessionExecutionStatisticsRecord { Name = OracleTestDatabaseModel.StatisticsDescriptionSessionLogicalReads, Value = 16 },
					new SessionExecutionStatisticsRecord { Name = OracleTestDatabaseModel.StatisticsDescriptionSqlNetRoundtripsToOrFromClient, Value = 2 }
				};

			return Task.FromResult(statistics);
		}

		public override Task<IReadOnlyList<object[]>> FetchRecordsAsync(ResultInfo resultInfo, int rowCount, CancellationToken cancellationToken)
		{
			IReadOnlyList<object[]> resultRow = new List<object[]> { new object[] { $"Dummy Value {++_generatedRowCount}" } };
			return Task.FromResult(resultRow);
		}

		public override bool HasActiveTransaction { get; } = true;

		public override string TransanctionIdentifier { get; } = "1.2.3456 (read committed)";

		public override Task CommitTransaction() => Task.CompletedTask;

		public override Task RollbackTransaction() => Task.CompletedTask;

		public override Task<ExecutionPlanItemCollection> ExplainPlanAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var rootItem = new ExecutionPlanItem();
			SetBasePlanItemData(rootItem);

			var planItemCollection = new ExecutionPlanItemCollection();
			planItemCollection.Add(rootItem);
			planItemCollection.SetAllItems();

			return Task.FromResult(planItemCollection);
		}

		public override Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken)
		{
			var rootItem = new ExecutionStatisticsPlanItem();
			SetBasePlanItemData(rootItem);

			var planItemCollection = new ExecutionStatisticsPlanItemCollection();
			planItemCollection.Add(rootItem);
			planItemCollection.PlanText = DummyPlanText;
			planItemCollection.SetAllItems();

			return Task.FromResult(planItemCollection);
		}

		internal const string DummyPlanText =
			@"SQL_ID  9g6pyx7qz035v, child number 0
-------------------------------------
SELECT * FROM DUAL
 
Plan hash value: 272002086
 
---------------------------------------------------------------------------
| Id  | Operation         | Name | E-Rows |E-Bytes| Cost (%CPU)| E-Time   |
---------------------------------------------------------------------------
|   0 | SELECT STATEMENT  |      |        |       |     2 (100)|          |
|   1 |  TABLE ACCESS FULL| DUAL |      1 |     2 |     2   (0)| 00:00:01 |
---------------------------------------------------------------------------
 
Query Block Name / Object Alias (identified by operation id):
-------------------------------------------------------------
 
   1 - SEL$1 / DUAL@SEL$1
 
Outline Data
-------------
 
  /*+
      BEGIN_OUTLINE_DATA
      IGNORE_OPTIM_EMBEDDED_HINTS
      OPTIMIZER_FEATURES_ENABLE('12.1.0.1')
      DB_VERSION('12.1.0.1')
      ALL_ROWS
      OUTLINE_LEAF(@""SEL$1"")
      FULL(@""SEL$1"" ""DUAL""@""SEL$1"")
      END_OUTLINE_DATA
  */
 
Column Projection Information (identified by operation id):
-----------------------------------------------------------
 
   1 - ""DUAL"".""DUMMY""[VARCHAR2,1]
 
Note
-----
   - Warning: basic plan statistics not available. These are only collected when:
       * hint 'gather_plan_statistics' is used for the statement or
       * parameter 'statistics_level' is set to 'ALL', at session or system level
";

		private static void SetBasePlanItemData(ExecutionPlanItem planItem)
		{
			planItem.Operation = "Operation";
			planItem.Options = "Options";
			planItem.Optimizer = "Optimizer";
			planItem.ObjectOwner = "ObjectOwner";
			planItem.ObjectName = "ObjectName";
			planItem.ObjectAlias = "ObjectAlias";
			planItem.ObjectType = "ObjectType";
			planItem.Cost = 1234;
			planItem.Cardinality = 5678;
			planItem.Bytes = 9123;
			planItem.PartitionStart = "PartitionStart";
			planItem.PartitionStop = "PartitionStop";
			planItem.Distribution = "Distribution";
			planItem.CpuCost = 9876;
			planItem.IoCost = 123;
			planItem.TempSpace = 54321;
			planItem.AccessPredicates = "AccessPredicates";
			planItem.FilterPredicates = "FilterPredicates";
			planItem.Time = TimeSpan.FromSeconds(144);
			planItem.QueryBlockName = "QueryBlockName";
			planItem.Other = null;
		}
	}
}
