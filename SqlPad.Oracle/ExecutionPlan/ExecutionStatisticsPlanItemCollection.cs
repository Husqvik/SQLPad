using System;
using System.Diagnostics;

namespace SqlPad.Oracle.ExecutionPlan
{
	public class ExecutionStatisticsPlanItemCollection : ExecutionPlanItemCollectionBase<ExecutionStatisticsPlanItem>
	{
		public string PlanText { get; set; }
	}

	[DebuggerDisplay("ExecutionStatisticsPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class ExecutionStatisticsPlanItem : ExecutionPlanItem
	{
		public DateTime Timestamp { get; set; }

		public int Executions { get; set; }

		public int? LastStarts { get; set; }

		public int? TotalStarts { get; set; }

		public long? LastOutputRows { get; set; }

		public long? TotalOutputRows { get; set; }

		public long? LastConsistentReadBufferGets { get; set; }

		public long? TotalConsistentReadBufferGets { get; set; }

		public long? LastCurrentReadBufferGets { get; set; }

		public long? TotalCurrentReadBufferGets { get; set; }

		public long? LastDiskReads { get; set; }

		public long? TotalDiskReads { get; set; }

		public long? LastDiskWrites { get; set; }

		public long? TotalDiskWrites { get; set; }

		public TimeSpan? LastElapsedTime { get; set; }

		public TimeSpan? TotalElapsedTime { get; set; }

		public string WorkAreaSizingPolicy { get; set; }

		public long? EstimatedOptimalSizeBytes { get; set; }

		public long? EstimatedOnePassSizeBytes { get; set; }

		public long? LastMemoryUsedBytes { get; set; }

		public string LastExecutionMethod { get; set; }

		public int? LastParallelDegree { get; set; }

		public int? TotalWorkAreaExecutions { get; set; }

		public int? OptimalWorkAreaExecutions { get; set; }

		public int? OnePassWorkAreaExecutions { get; set; }

		public int? MultiPassWorkAreaExecutions { get; set; }

		public TimeSpan? ActiveTime { get; set; }

		public long? MaxTemporarySizeBytes { get; set; }

		public long? LastTemporarySizeBytes { get; set; }
	}
}