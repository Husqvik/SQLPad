using System;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif

namespace SqlPad.Oracle
{
	public class OracleDebuggerSession : IDebuggerSession, IDisposable
	{
		private readonly OracleCommand _debuggedSessionCommand;
		private readonly OracleConnection _debuggerSession;
		private readonly OracleCommand _debuggerSessionCommand;

		private string _debuggerSessionId;

		public OracleDebuggerSession(OracleConnection debuggedSession)
		{
			_debuggedSessionCommand = debuggedSession.CreateCommand();
			_debuggedSessionCommand.BindByName = true;
			_debuggerSession = (OracleConnection)debuggedSession.Clone();
			_debuggerSessionCommand = _debuggerSession.CreateCommand();
			_debuggerSessionCommand.BindByName = true;
		}

		public void Start()
		{
			_debuggedSessionCommand.CommandText = DatabaseCommands.StartDebuggee;
			_debuggedSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", null, OracleBindVariable.DataTypeVarchar2, 12);
			var debuggerSessionIdParameter = _debuggedSessionCommand.Parameters[0];

			_debuggedSessionCommand.ExecuteNonQuery();
			_debuggerSessionId = ((OracleString)debuggerSessionIdParameter.Value).Value;

			_debuggerSessionCommand.CommandText = DatabaseCommands.StartDebugger;
			_debuggerSessionCommand.AddSimpleParameter("DEBUG_SESSION_ID", _debuggerSessionId);
			_debuggerSessionCommand.AddSimpleParameter("SYNCHRONIZE_STATUS", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("BREAKPOINT", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("INTERPRETERDEPTH", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("LINE", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("OER", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("REASON", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("STACKDEPTH", null, OracleBindVariable.DataTypeNumber);
			_debuggerSessionCommand.AddSimpleParameter("TERMINATED", null, OracleBindVariable.DataTypeNumber);

			_debuggerSession.Open();
			_debuggerSessionCommand.ExecuteNonQuery();
		}

		public void Continue()
		{
			
		}

		public void Detach()
		{
			
		}

		public void Dispose()
		{
			_debuggedSessionCommand.Dispose();
			_debuggerSessionCommand.Dispose();
			_debuggerSession.Dispose();
		}
	}
}