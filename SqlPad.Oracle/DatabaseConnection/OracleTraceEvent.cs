using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleTraceEvent
	{
		public static readonly OracleTraceEvent[] AllTraceEvents =
		{
			new OracleTraceEvent(10032, "Dump sort statistics", TraceEventScope.Session, "'10032 trace name context forever, level {0}'", "'10032 trace name context off'", false, new [] { 10 }) { Level = 10 },
			new OracleTraceEvent(10033, "Dump sort intermediate run statistics", TraceEventScope.Session, "'10033 trace name context forever, level {0}'", "'10033 trace name context off'", false, new [] { 10 }) { Level = 10 },
			new OracleTraceEvent(10046, "Extended SQL trace", TraceEventScope.Session, "'10046 trace name context forever, level {0}'", "'10046 trace name context off'", false, new [] { 1, 4, 8, 12 }) { Level = 12 },
			new OracleTraceEvent(10053, "Cost based optimizer", TraceEventScope.Session, "'10053 trace name context forever, level {0}'", "'10053 trace name context off'", false, new [] { 1, 2 }) { Level = 1 },
			new OracleTraceEvent(10079, "Dump SQL*Net statistics", TraceEventScope.Session, "'10079 trace name context forever, level {0}'", "'10079 trace name context off'", true, new [] { 1, 2, 4, 8 }) { Level = 1 },
			new OracleTraceEvent(10081, "Trace high water mark changes", TraceEventScope.Session, "'10081 trace name context forever, level {0}'", "'10081 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10104, "Hash join trace", TraceEventScope.Session, "'10104 trace name context forever, level {0}'", "'10104 trace name context off'", false, new [] { 1, 2, 10 }) { Level = 10 },
			new OracleTraceEvent(10128, "Dump partition pruning information", TraceEventScope.Session, "'10128 trace name context forever, level {0}'", "'10128 trace name context off'", false, new [] { 1, 2, 4, 8 }) { Level = 1 },
			new OracleTraceEvent(10200, "Dump consistent reads", TraceEventScope.Session, "'10200 trace name context forever, level {0}'", "'10200 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10201, "Dump consistent read undo application", TraceEventScope.Session, "'10201 trace name context forever, level {0}'", "'10201 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10224, "Dump index block splits/deletes", TraceEventScope.Session, "'10224 trace name context forever, level {0}'", "'10224 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10241, "Dump remote SQL execution", TraceEventScope.Session, "'10241 trace name context forever, level {0}'", "'10241 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10357, "Debug direct path", TraceEventScope.Session, "'10357 trace name context forever, level {0}'", "'10357 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10390, "Dump parallel execution slave statistics", TraceEventScope.Session, "'10390 trace name context forever, level {0}'", "'10390 trace name context off'", false, new [] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 }) { Level = 1 },
			new OracleTraceEvent(10393, "Dump parallel execution statistics", TraceEventScope.Session, "'10393 trace name context forever, level {0}'", "'10393 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10608, "Trace bitmap index creation", TraceEventScope.Session, "'10608 trace name context forever, level {0}'", "'10608 trace name context off'", false, new [] { 10 }) { Level = 10 },
			new OracleTraceEvent(10704, "Trace enqueues", TraceEventScope.Session, "'10704 trace name context forever, level {0}'", "'10704 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10708, "Trace RAC buffer cache", TraceEventScope.Session, "'10708 trace name context forever, level {0}'", "'10708 trace name context off'", false, new [] { 10 }) { Level = 10 },
			new OracleTraceEvent(10710, "Trace bitmap index access", TraceEventScope.Session, "'10710 trace name context forever, level {0}'", "'10710 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10730, "Trace fine grained access predicates", TraceEventScope.Session, "'10730 trace name context forever, level {0}'", "'10730 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10928, "Trace PL/SQL execution", TraceEventScope.Session, "'10928 trace name context forever, level {0}'", "'10928 trace name context off'", false, new [] { 1 }) { Level = 1 },
			new OracleTraceEvent(10938, "Dump PL/SQL execution statistics", TraceEventScope.Session, "'10938 trace name context forever, level {0}'", "'10938 trace name context off'", false, new [] { 1 }) { Level = 1 }
		};

		private int _level;
		private readonly string _enableEventTraceParameter;
		private readonly string _disableEventTraceParameter;
		
		public int EventCode { get; private set; }
		
		public bool RequiresDbaPrivilege { get; private set; }
		
		public string Title { get; private set; }
		
		public TraceEventScope Scope { get; private set; }

		public int Level
		{
			get { return _level; }
			set
			{
				if (SupportedLevels == null)
				{
					throw new InvalidOperationException(String.Format("Event trace '{0}' does not support level option. ", Title));
				}

				if (!SupportedLevels.Contains(value))
				{
					throw new ArgumentException(String.Format("Level {0} is not supported. Supported levels are: {1} ", value, String.Join(", ", SupportedLevels)), "value");
				}

				_level = value;
			}
		}

		public IReadOnlyList<int> SupportedLevels { get; private set; }

		public OracleTraceEvent(int eventCode, string title, TraceEventScope scope, string enableEventTraceParameter, string disableEventTraceParameter, bool requiresDbaPrivilege, IReadOnlyList<int> supportedLevels)
		{
			EventCode = eventCode;
			Title = title;
			Scope = scope;
			_enableEventTraceParameter = enableEventTraceParameter;
			_disableEventTraceParameter = disableEventTraceParameter;
			RequiresDbaPrivilege = requiresDbaPrivilege;
			SupportedLevels = supportedLevels;
		}

		public string CommandTextEnable
		{
			get
			{
				var parameter = SupportedLevels == null ? _enableEventTraceParameter : String.Format(_enableEventTraceParameter, Level);
				return BuildCommandText(parameter);
			}
		}

		public string CommandTextDisable
		{
			get { return BuildCommandText(_disableEventTraceParameter); }
		}

		private string BuildCommandText(string parameter)
		{
			return String.Format("ALTER {0} SET EVENTS {1}", Scope.ToString().ToUpperInvariant(), parameter);
		}
	}

	public enum TraceEventScope
	{
		Session,
		System
	}
}