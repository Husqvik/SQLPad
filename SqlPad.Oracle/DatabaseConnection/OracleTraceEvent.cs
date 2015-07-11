using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleTraceEvent
	{
		public static readonly OracleTraceEvent[] AllTraceEvents =
		{
			new OracleTraceEvent(10046, "Extended SQL trace", TraceEventScope.Session, "'10046 trace name context forever, level {0}'", "'10046 trace name context off'", new [] { 1, 4, 8, 12 }) { Level = 12 },
			new OracleTraceEvent(10053, "Cost based optimizer", TraceEventScope.Session, "'10053 trace name context forever, level {0}'", "'10053 trace name context off'", new [] { 1, 2 }) { Level = 1 }
		};

		private int _level;
		private readonly string _enableEventTraceParameter;
		private readonly string _disableEventTraceParameter;
		
		public int EventCode { get; private set; }
		
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

		public OracleTraceEvent(int eventCode, string title, TraceEventScope scope, string enableEventTraceParameter, string disableEventTraceParameter, IReadOnlyList<int> supportedLevels)
		{
			EventCode = eventCode;
			Title = title;
			Scope = scope;
			_enableEventTraceParameter = enableEventTraceParameter;
			_disableEventTraceParameter = disableEventTraceParameter;
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