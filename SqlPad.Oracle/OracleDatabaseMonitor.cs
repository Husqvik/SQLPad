using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Commands;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DebugTrace;

namespace SqlPad.Oracle
{
	public class OracleDatabaseMonitor : IDatabaseMonitor
	{
		private readonly ConnectionStringSettings _connectionString;

		private string BackgroundConnectionString => OracleConnectionStringRepository.GetBackgroundConnectionString(_connectionString.ConnectionString);

		public OracleDatabaseMonitor(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;
		}

		public async Task<DatabaseSessions> GetAllSessionDataAsync(CancellationToken cancellationToken)
		{
			var databaseSessions = new DatabaseSessions();

			using (var connection = new OracleConnection(BackgroundConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = OracleDatabaseCommands.SelectBasicSessionInformationCommandText;

					await connection.OpenAsynchronous(cancellationToken);

					connection.ModuleName = "Database monitor";

					using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
					{
						databaseSessions.ColumnHeaders = OracleConnectionAdapter.GetColumnHeadersFromReader(reader);

						var sessions = new Dictionary<int, DatabaseSession>();
						while (await reader.ReadAsynchronous(cancellationToken))
						{
							var oracleSession =
								new OracleSessionValues
								{
									Process = OracleReaderValueConvert.ToString(reader["PROCESS"]),
									ExecutionId = OracleReaderValueConvert.ToInt32(reader["SQL_EXEC_ID"]),
									Type = (string)reader["TYPE"],
									Id = Convert.ToInt32(reader["SID"]),
									ExecutionStart = OracleReaderValueConvert.ToDateTime(reader["SQL_EXEC_START"]),
									Action = OracleReaderValueConvert.ToString(reader["ACTION"]),
									State = (string)reader["STATE"],
									Status = (string)reader["STATUS"],
									AuditingSessionId = Convert.ToInt64(reader["AUDSID"]),
									ChildNumber = OracleReaderValueConvert.ToInt32(reader["SQL_CHILD_NUMBER"]),
									ClientInfo = OracleReaderValueConvert.ToString(reader["CLIENT_INFO"]),
									CurrentCommandText = OracleReaderValueConvert.ToString(await reader.GetValueAsynchronous(reader.GetOrdinal("CURRENT_COMMAND_TEXT"), cancellationToken)),
									Event = (string)reader["EVENT"],
									FailedOver = (string)reader["FAILED_OVER"],
									FailoverMethod = (string)reader["FAILOVER_METHOD"],
									FailoverType = (string)reader["FAILOVER_TYPE"],
									LockWait = OracleReaderValueConvert.ToString(reader["LOCKWAIT"]),
									LogonTime = (DateTime)reader["LOGON_TIME"],
									Machine = OracleReaderValueConvert.ToString(reader["MACHINE"]),
									Module = OracleReaderValueConvert.ToString(reader["MODULE"]),
									OperatingSystemUser = (string)reader["OSUSER"],
									ParallelDdlStatus = (string)reader["PDDL_STATUS"],
									ParallelDmlEnabled = (string)reader["PDML_ENABLED"],
									ParallelDmlStatus = (string)reader["PDML_STATUS"],
									ParallelQueryStatus = (string)reader["PQ_STATUS"],
									Parameter1 = Convert.ToDecimal(reader["P1"]),
									Parameter1Text = OracleReaderValueConvert.ToString(reader["P1TEXT"]),
									Parameter2 = Convert.ToDecimal(reader["P2"]),
									Parameter2Text = OracleReaderValueConvert.ToString(reader["P2TEXT"]),
									Parameter3 = Convert.ToDecimal(reader["P3"]),
									Parameter3Text = OracleReaderValueConvert.ToString(reader["P3TEXT"]),
									OwnerSessionId = OracleReaderValueConvert.ToInt32(reader["OWNER_SID"]),
									Port = OracleReaderValueConvert.ToInt32(reader["PORT"]),
									PrecedingChildNumber = OracleReaderValueConvert.ToInt32(reader["PREV_CHILD_NUMBER"]),
									PrecedingCommandText = OracleReaderValueConvert.ToString(await reader.GetValueAsynchronous(reader.GetOrdinal("PRECEDING_COMMAND_TEXT"), cancellationToken)),
									PrecedingExecutionId = OracleReaderValueConvert.ToInt32(reader["PREV_EXEC_ID"]),
									PrecedingExecutionStart = OracleReaderValueConvert.ToDateTime(reader["PREV_EXEC_START"]),
									PrecedingSqlId = OracleReaderValueConvert.ToString(reader["PREV_SQL_ID"]),
									ProcessAddress = (string)reader["PADDR"],
									Program = OracleReaderValueConvert.ToString(reader["PROGRAM"]),
									RemainingTimeMicroseconds = OracleReaderValueConvert.ToInt64(reader["TIME_REMAINING_MICRO"]),
									ResourceConsumeGroup = OracleReaderValueConvert.ToString(reader["RESOURCE_CONSUMER_GROUP"]),
									SchemaName = OracleReaderValueConvert.ToString(reader["SCHEMANAME"]),
									Serial = Convert.ToInt32(reader["SERIAL#"]),
									Server = (string)reader["SERVER"],
									ServiceName = OracleReaderValueConvert.ToString(reader["SERVICE_NAME"]),
									SessionAddress = (string)reader["SADDR"],
									SqlId = OracleReaderValueConvert.ToString(reader["SQL_ID"]),
									SqlTrace = (string)reader["SQL_TRACE"],
									TimeSinceLastWaitMicroseconds = OracleReaderValueConvert.ToInt64(reader["TIME_SINCE_LAST_WAIT_MICRO"]),
									TransactionAddress = OracleReaderValueConvert.ToString(reader["TADDR"]),
									UserName = OracleReaderValueConvert.ToString(reader["USERNAME"]),
									WaitClass = (string)reader["WAIT_CLASS"],
									WaitTime = OracleReaderValueConvert.ToInt64(reader["WAIT_TIME"]),
									WaitTimeMicroseconds = OracleReaderValueConvert.ToInt64(reader["WAIT_TIME_MICRO"]),
									ProcessIdentifier = Convert.ToInt32(reader["PID"]),
									OperatingSystemIdentifier = Convert.ToInt32(reader["SOSID"]),
									OperatingSystemProcessIdentifier = Convert.ToInt32(reader["SPID"]),
									TraceId = OracleReaderValueConvert.ToString(reader["TRACEID"]),
									TraceFile = OracleReaderValueConvert.ToString(reader["TRACEFILE"]),
									ProgramGlobalAreaUsedMemoryBytes = OracleReaderValueConvert.ToInt64(reader["PGA_USED_MEM"]),
									ProgramGlobalAreaAllocatedMemoryBytes = OracleReaderValueConvert.ToInt64(reader["PGA_ALLOC_MEM"]),
									ProgramGlobalAreaFreeableMemoryBytes = OracleReaderValueConvert.ToInt64(reader["PGA_FREEABLE_MEM"]),
									ProgramGlobalAreaMaximumMemoryBytes = OracleReaderValueConvert.ToInt64(reader["PGA_MAX_MEM"])
								};

							var databaseSession =
								new DatabaseSession
								{
									Id = oracleSession.Id,
									ProviderValues = oracleSession,
									Type = String.Equals(oracleSession.Type, "User") ? SessionType.User : SessionType.System,
									IsActive = Convert.ToString(oracleSession.Status) == "Active"
								};

							sessions.Add(databaseSession.Id, databaseSession);
						}

						foreach (var session in sessions.Values)
						{
							DatabaseSession ownerSession;
							var ownerSid = ((OracleSessionValues)session.ProviderValues).OwnerSessionId;
							if (ownerSid.HasValue && sessions.TryGetValue(ownerSid.Value, out ownerSession))
							{
								session.Owner = ownerSession;
								ownerSession.ChildSessions.Add(session);
							}
						}

						databaseSessions.Rows = sessions.Values.ToArray();
					}
				}

				await connection.CloseAsynchronous(cancellationToken);
			}

			return databaseSessions;
		}

		public IDatabaseSessionDetailViewer CreateSessionDetailViewer()
		{
			return new OracleSessionDetailViewer(_connectionString);
		}

		public IEnumerable<ContextAction> GetSessionContextActions(DatabaseSession databaseSession)
		{
			var sessionValues = (OracleSessionValues)databaseSession.ProviderValues;

			var executionHandler = new CommandExecutionHandler();
			string actionName;
			if (String.Equals(sessionValues.SqlTrace, "Disabled"))
			{
				actionName = "Enable trace";
				executionHandler.ExecutionHandlerAsync = (context, cancellationToken) => EnableSessionTracing(sessionValues, sessionValues.TraceId, true, true, cancellationToken);
			}
			else
			{
				actionName = "Disable trace";
				executionHandler.ExecutionHandlerAsync = (context, cancellationToken) => DisableSessionTracing(sessionValues, cancellationToken);
			}

			yield return new ContextAction(actionName, executionHandler, null, true);

			executionHandler =
				new CommandExecutionHandler
				{
					ExecutionHandler = context => OracleTraceViewer.NavigateToTraceFile(sessionValues.TraceFile)
				};

			yield return new ContextAction("Navigate to trace file", executionHandler, null);
		}

		private async Task EnableSessionTracing(OracleSessionValues sessionData, string traceIdentifier, bool waits, bool binds, CancellationToken cancellationToken)
		{
			traceIdentifier = traceIdentifier ?? String.Empty;

			var commandText =
$@"BEGIN
	EXECUTE IMMEDIATE 'ALTER SESSION SET TRACEFILE_IDENTIFIER = {OracleTraceIdentifier.Normalize(traceIdentifier)}';
	dbms_monitor.session_trace_enable(session_id => :sid, serial_num => :serial, waits => {waits}, binds => {binds});
END;";
			await SetSessionTracing(sessionData, commandText, cancellationToken);
		}

		private async Task DisableSessionTracing(OracleSessionValues sessionData, CancellationToken cancellationToken)
		{
			const string commandText = @"BEGIN
	dbms_monitor.session_trace_disable(session_id => :sid, serial_num => :serial);
END;";

			await SetSessionTracing(sessionData, commandText, cancellationToken);
		}

		private async Task SetSessionTracing(OracleSessionValues sessionData, string commandText, CancellationToken cancellationToken)
		{
			using (var connection = new OracleConnection(BackgroundConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;
					command.CommandText = commandText;

					command.AddSimpleParameter("SID", sessionData.Id);
					command.AddSimpleParameter("SERIAL", sessionData.Serial);

					await connection.OpenAsynchronous(cancellationToken);
					await command.ExecuteNonQueryAsynchronous(cancellationToken);
					await connection.CloseAsynchronous(cancellationToken);
				}
			}
		}
	}

	[DebuggerDisplay("OracleSessionValues(Id={Id}, Serial={Serial}, SqlId={SqlId}, ChildNumber={ChildNumber}, ExecutionId={ExecutionId}, ExecutionStart={ExecutionStart})")]
	public class OracleSessionValues : IDatabaseSessionValues
	{
		public object[] Values { get; } = new object[64];

		public string SessionAddress
		{
			get { return (string)Values[0]; }
			set { Values[0] = value; }
		}

		public int Id
		{
			get { return (int)Values[1]; }
			set { Values[1] = value; }
		}

		public int? OwnerSessionId
		{
			get { return (int?)Values[2]; }
			set { Values[2] = value; }
		}

		public int Serial
		{
			get { return (int)Values[3]; }
			set { Values[3] = value; }
		}

		public long AuditingSessionId
		{
			get { return (int)Values[4]; }
			set { Values[4] = value; }
		}

		public string ProcessAddress
		{
			get { return (string)Values[5]; }
			set { Values[5] = value; }
		}

		public string UserName
		{
			get { return (string)Values[6]; }
			set { Values[6] = value; }
		}

		public string TransactionAddress
		{
			get { return (string)Values[7]; }
			set { Values[7] = value; }
		}

		public string LockWait
		{
			get { return (string)Values[8]; }
			set { Values[8] = value; }
		}

		public string Status
		{
			get { return (string)Values[9]; }
			set { Values[9] = value; }
		}

		public string Server
		{
			get { return (string)Values[10]; }
			set { Values[10] = value; }
		}

		public string SchemaName
		{
			get { return (string)Values[11]; }
			set { Values[11] = value; }
		}

		public string OperatingSystemUser
		{
			get { return (string)Values[12]; }
			set { Values[12] = value; }
		}

		public string Process
		{
			get { return (string)Values[13]; }
			set { Values[13] = value; }
		}

		public string Machine
		{
			get { return (string)Values[14]; }
			set { Values[14] = value; }
		}

		public int? Port
		{
			get { return (int?)Values[15]; }
			set { Values[15] = value; }
		}

		public string Program
		{
			get { return (string)Values[16]; }
			set { Values[16] = value; }
		}

		public string Type
		{
			get { return (string)Values[17]; }
			set { Values[17] = value; }
		}

		public string SqlId
		{
			get { return (string)Values[18]; }
			set { Values[18] = value; }
		}

		public int? ChildNumber
		{
			get { return (int?)Values[19]; }
			set { Values[19] = value; }
		}

		public DateTime? ExecutionStart
		{
			get { return (DateTime?)Values[20]; }
			set { Values[20] = value; }
		}

		public int? ExecutionId
		{
			get { return (int?)Values[21]; }
			set { Values[21] = value; }
		}

		public string PrecedingSqlId
		{
			get { return (string)Values[22]; }
			set { Values[22] = value; }
		}

		public int? PrecedingChildNumber
		{
			get { return (int?)Values[23]; }
			set { Values[23] = value; }
		}

		public DateTime? PrecedingExecutionStart
		{
			get { return (DateTime?)Values[24]; }
			set { Values[24] = value; }
		}

		public int? PrecedingExecutionId
		{
			get { return (int?)Values[25]; }
			set { Values[25] = value; }
		}

		public string Module
		{
			get { return (string)Values[26]; }
			set { Values[26] = value; }
		}

		public string Action
		{
			get { return (string)Values[27]; }
			set { Values[27] = value; }
		}

		public string ClientInfo
		{
			get { return (string)Values[28]; }
			set { Values[28] = value; }
		}

		public DateTime LogonTime
		{
			get { return (DateTime)Values[29]; }
			set { Values[29] = value; }
		}

		public string ParallelDmlEnabled
		{
			get { return (string)Values[30]; }
			set { Values[30] = value; }
		}

		public string FailoverType
		{
			get { return (string)Values[31]; }
			set { Values[31] = value; }
		}

		public string FailoverMethod
		{
			get { return (string)Values[32]; }
			set { Values[32] = value; }
		}

		public string FailedOver
		{
			get { return (string)Values[33]; }
			set { Values[33] = value; }
		}

		public string ResourceConsumeGroup
		{
			get { return (string)Values[34]; }
			set { Values[34] = value; }
		}

		public string ParallelDmlStatus
		{
			get { return (string)Values[35]; }
			set { Values[35] = value; }
		}

		public string ParallelDdlStatus
		{
			get { return (string)Values[36]; }
			set { Values[36] = value; }
		}

		public string ParallelQueryStatus
		{
			get { return (string)Values[37]; }
			set { Values[37] = value; }
		}

		public string Event
		{
			get { return (string)Values[38]; }
			set { Values[38] = value; }
		}

		public string Parameter1Text
		{
			get { return (string)Values[39]; }
			set { Values[39] = value; }
		}

		public decimal Parameter1
		{
			get { return (decimal)Values[40]; }
			set { Values[40] = value; }
		}

		public string Parameter2Text
		{
			get { return (string)Values[41]; }
			set { Values[41] = value; }
		}

		public decimal Parameter2
		{
			get { return (decimal)Values[42]; }
			set { Values[42] = value; }
		}

		public string Parameter3Text
		{
			get { return (string)Values[43]; }
			set { Values[43] = value; }
		}

		public decimal Parameter3
		{
			get { return (decimal)Values[44]; }
			set { Values[44] = value; }
		}

		public string WaitClass
		{
			get { return (string)Values[45]; }
			set { Values[45] = value; }
		}

		public long? WaitTime
		{
			get { return (long?)Values[46]; }
			set { Values[46] = value; }
		}

		public string State
		{
			get { return (string)Values[47]; }
			set { Values[47] = value; }
		}

		public long? WaitTimeMicroseconds
		{
			get { return (long?)Values[48]; }
			set { Values[48] = value; }
		}

		public long? RemainingTimeMicroseconds
		{
			get { return (long?)Values[49]; }
			set { Values[49] = value; }
		}

		public long? TimeSinceLastWaitMicroseconds
		{
			get { return (long?)Values[50]; }
			set { Values[50] = value; }
		}

		public string ServiceName
		{
			get { return (string)Values[51]; }
			set { Values[51] = value; }
		}

		public string SqlTrace
		{
			get { return (string)Values[52]; }
			set { Values[52] = value; }
		}

		public string TraceId
		{
			get { return (string)Values[53]; }
			set { Values[53] = value; }
		}

		public string TraceFile
		{
			get { return (string)Values[54]; }
			set { Values[54] = value; }
		}

		public long? ProgramGlobalAreaUsedMemoryBytes
		{
			get { return (long?)Values[55]; }
			set { Values[55] = value; }
		}

		public long? ProgramGlobalAreaAllocatedMemoryBytes
		{
			get { return (long?)Values[56]; }
			set { Values[56] = value; }
		}

		public long? ProgramGlobalAreaFreeableMemoryBytes
		{
			get { return (long?)Values[57]; }
			set { Values[57] = value; }
		}

		public long? ProgramGlobalAreaMaximumMemoryBytes
		{
			get { return (long?)Values[58]; }
			set { Values[58] = value; }
		}

		public int ProcessIdentifier
		{
			get { return (int)Values[59]; }
			set { Values[59] = value; }
		}

		public int OperatingSystemIdentifier
		{
			get { return (int)Values[60]; }
			set { Values[60] = value; }
		}

		public int OperatingSystemProcessIdentifier
		{
			get { return (int)Values[61]; }
			set { Values[61] = value; }
		}

		public string CurrentCommandText
		{
			get { return ((OracleSimpleValue)Values[62]).Value; }
			set { Values[62] = new OracleSimpleValue(value); }
		}

		public string PrecedingCommandText
		{
			get { return ((OracleSimpleValue)Values[63]).Value; }
			set { Values[63] = new OracleSimpleValue(value); }
		}

		public OracleSessionValues Clone()
		{
			var clone = new OracleSessionValues();
			for (var i = 0; i < Values.Length; i++)
			{
				clone.Values[i] = Values[i];
			}

			return clone;
		}
	}
}
