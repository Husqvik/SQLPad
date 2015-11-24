using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle.DebugTrace
{
	public class OracleTraceEvent
	{
		public static readonly OracleTraceEvent[] AllTraceEvents =
		{
			new OracleTraceEvent(10032, "Dump sort statistics", TraceEventScope.Session, "'10032 trace name context forever, level {0}'", "'10032 trace name context off'", false, new OracleTraceEventLevel[] { 10 }) { Level = 10 },
			new OracleTraceEvent(10033, "Dump sort intermediate run statistics", TraceEventScope.Session, "'10033 trace name context forever, level {0}'", "'10033 trace name context off'", false, new OracleTraceEventLevel[] { 10 }) { Level = 10 },
			new OracleTraceEvent(10046, "Extended SQL trace", TraceEventScope.Session, "'10046 trace name context forever, level {0}'", "'10046 trace name context off'", false,
				new []
				{
					new OracleTraceEventLevel(1, "Print SQL statements, execution plans and execution statistics"),
					new OracleTraceEventLevel(4, "As level 1 plus bind variables"),
					new OracleTraceEventLevel(8, "As level 1 plus wait statistics"),
					new OracleTraceEventLevel(12, "As level 1 plus bind variables and wait statistics"),
				}) { Level = 12 },
			new OracleTraceEvent(10053, "Cost based optimizer", TraceEventScope.Session, "'10053 trace name context forever, level {0}'", "'10053 trace name context off'", false,
				new []
				{
					new OracleTraceEventLevel(1, "Print statistics and computations"),
					new OracleTraceEventLevel(2, "Print computations only")
				}) { Level = 1 },
			new OracleTraceEvent(10079, "Dump SQL*Net statistics", TraceEventScope.Session, "'10079 trace name context forever, level {0}'", "'10079 trace name context off'", true, new OracleTraceEventLevel[] { 1, 2, 4, 8 }) { Level = 1 },
			new OracleTraceEvent(10081, "Trace high water mark changes", TraceEventScope.Session, "'10081 trace name context forever, level {0}'", "'10081 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10104, "Hash join trace", TraceEventScope.Session, "'10104 trace name context forever, level {0}'", "'10104 trace name context off'", false, new OracleTraceEventLevel[] { 1, 2, 10 }) { Level = 10 },
			new OracleTraceEvent(10128, "Dump partition pruning information", TraceEventScope.Session, "'10128 trace name context forever, level {0}'", "'10128 trace name context off'", false,
				new []
				{
					new OracleTraceEventLevel(1, "Dump pruning descriptor for each partitioned object"),
					new OracleTraceEventLevel(2, "Dump partition iterators"),
					new OracleTraceEventLevel(4, "Dump optimizer decisions about partition-wise joins"),
					new OracleTraceEventLevel(8, "Dump ROWID range scan pruning information")
				}) { Level = 1 },
			new OracleTraceEvent(10200, "Dump consistent reads", TraceEventScope.Session, "'10200 trace name context forever, level {0}'", "'10200 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10201, "Dump consistent read undo application", TraceEventScope.Session, "'10201 trace name context forever, level {0}'", "'10201 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10224, "Dump index block splits/deletes", TraceEventScope.Session, "'10224 trace name context forever, level {0}'", "'10224 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10241, "Dump remote SQL execution", TraceEventScope.Session, "'10241 trace name context forever, level {0}'", "'10241 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10357, "Debug direct path", TraceEventScope.Session, "'10357 trace name context forever, level {0}'", "'10357 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10390, "Dump parallel execution slave statistics", TraceEventScope.Session, "'10390 trace name context forever, level {0}'", "'10390 trace name context off'", false,
				new []
				{
					new OracleTraceEventLevel(1, "Slave-side execution messages"),
					new OracleTraceEventLevel(2, "Coordinator-side execution messages"),
					new OracleTraceEventLevel(4, "Slave context state changes"),
					new OracleTraceEventLevel(8, "Slave ROWID range bind variables and xty"),
					new OracleTraceEventLevel(16, "Slave fetched rows as enqueued to TQ"),
					new OracleTraceEventLevel(32, "Coordinator wait reply handling"),
					new OracleTraceEventLevel(64, "Coordinator wait message buffering"),
					new OracleTraceEventLevel(128, "Slave dump timing"),
					new OracleTraceEventLevel(256, "Coordinator dump timing"),
					new OracleTraceEventLevel(512, "Slave dump allocation file number"),
					new OracleTraceEventLevel(1024, "Terse format for debug dumps"),
					new OracleTraceEventLevel(2048, "Trace CRI random sampling"),
					new OracleTraceEventLevel(4096, "Trace signals"),
					new OracleTraceEventLevel(8192, "Trace parallel execution granule operations"),
					new OracleTraceEventLevel(16384, "Force compilation by slave 0")
				}) { Level = 1 },
			new OracleTraceEvent(10391, "Dump parallel execution granule allocation", TraceEventScope.Session, "'10391 trace name context forever, level {0}'", "'10391 trace name context off'", false,
				new []
				{
					new OracleTraceEventLevel(1, "Dump summary of each object scanned in parallel"),
					new OracleTraceEventLevel(2, "Full dump of each object except extent map"),
					new OracleTraceEventLevel(4, "Full dump of each object including extent map"),
					new OracleTraceEventLevel(16, "Dump summary of each granule generators"),
					new OracleTraceEventLevel(32, "Full dump of granule generators except granule instances"),
					new OracleTraceEventLevel(64, "Full dump of granule generators including granule instances"),
					new OracleTraceEventLevel(128, "Dump system information"),
					new OracleTraceEventLevel(256, "Dump reference object for the query"),
					new OracleTraceEventLevel(512, "Gives timing in kxfralo"),
					new OracleTraceEventLevel(1024, "Trace affinity module"),
					new OracleTraceEventLevel(2048, "Trace granule allocation during query execution"),
					new OracleTraceEventLevel(4096, "Trace object flush"),
					new OracleTraceEventLevel(8192, "Unknown")
				}) { Level = 1 },
			new OracleTraceEvent(10393, "Dump parallel execution statistics", TraceEventScope.Session, "'10393 trace name context forever, level {0}'", "'10393 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10608, "Trace bitmap index creation", TraceEventScope.Session, "'10608 trace name context forever, level {0}'", "'10608 trace name context off'", false, new OracleTraceEventLevel[] { 10 }) { Level = 10 },
			new OracleTraceEvent(10704, "Trace enqueues", TraceEventScope.Session, "'10704 trace name context forever, level {0}'", "'10704 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10708, "Trace RAC buffer cache", TraceEventScope.Session, "'10708 trace name context forever, level {0}'", "'10708 trace name context off'", false, new OracleTraceEventLevel[] { 10 }) { Level = 10 },
			new OracleTraceEvent(10710, "Trace bitmap index access", TraceEventScope.Session, "'10710 trace name context forever, level {0}'", "'10710 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10730, "Trace fine grained access predicates", TraceEventScope.Session, "'10730 trace name context forever, level {0}'", "'10730 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10731, "Trace CURSOR statements", TraceEventScope.Session, "'10731 trace name context forever, level {0}'", "'10731 trace name context off'", false,
				new []
				{
					new OracleTraceEventLevel(1, "Print parent query and subquery"),
					new OracleTraceEventLevel(2, "Print subquery only")
				}) { Level = 1 },
			new OracleTraceEvent(10928, "Trace PL/SQL execution", TraceEventScope.Session, "'10928 trace name context forever, level {0}'", "'10928 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 },
			new OracleTraceEvent(10938, "Dump PL/SQL execution statistics", TraceEventScope.Session, "'10938 trace name context forever, level {0}'", "'10938 trace name context off'", false, new OracleTraceEventLevel[] { 1 }) { Level = 1 }
		};

		private OracleTraceEventLevel _level;
		private readonly Dictionary<int, OracleTraceEventLevel> _supportedLevels; 
		private readonly string _enableEventTraceParameter;
		private readonly string _disableEventTraceParameter;

		public int EventCode { get; private set; }

		public bool RequiresDbaPrivilege { get; private set; }

		public string Title { get; }

		public TraceEventScope Scope { get; }

		public OracleTraceEventLevel Level
		{
			get { return _level; }
			set
			{
				if (SupportedLevels == null)
				{
					throw new InvalidOperationException($"Event trace '{Title}' does not support level option. ");
				}

				if (!_supportedLevels.TryGetValue(value.Value, out _level))
				{
					throw new ArgumentException($"Level {value} is not supported. Supported levels are: {String.Join(", ", SupportedLevels.Select(l => l.Value))} ", nameof(value));
				}
			}
		}

		public IReadOnlyCollection<OracleTraceEventLevel> SupportedLevels => _supportedLevels.Values;

		public OracleTraceEvent(int eventCode, string title, TraceEventScope scope, string enableEventTraceParameter, string disableEventTraceParameter, bool requiresDbaPrivilege, IReadOnlyList<OracleTraceEventLevel> supportedLevels)
		{
			EventCode = eventCode;
			Title = title;
			Scope = scope;
			_enableEventTraceParameter = enableEventTraceParameter;
			_disableEventTraceParameter = disableEventTraceParameter;
			RequiresDbaPrivilege = requiresDbaPrivilege;
			_supportedLevels = supportedLevels.ToDictionary(l => l.Value);
		}

		public string CommandTextEnable
		{
			get
			{
				var parameter = SupportedLevels == null ? _enableEventTraceParameter : String.Format(_enableEventTraceParameter, Level.Value);
				return BuildCommandText(parameter);
			}
		}

		public string CommandTextDisable => BuildCommandText(_disableEventTraceParameter);

		private string BuildCommandText(string parameter)
		{
			return $"ALTER {Scope.ToString().ToUpperInvariant()} SET EVENTS {parameter}";
		}
	}

	public class OracleTraceEventLevel
	{
		public int Value { get; }

		public string Description { get; }

		public OracleTraceEventLevel(int value, string description)
		{
			Value = value;
			Description = description;
		}

		public static implicit operator OracleTraceEventLevel(int level)
		{
			return new OracleTraceEventLevel(level, null);
		}
	}

	public enum TraceEventScope
	{
		Session,
		System
	}
}
