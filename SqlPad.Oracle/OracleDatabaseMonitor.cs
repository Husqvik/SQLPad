using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle
{
	public class OracleDatabaseMonitor : IDatabaseMonitor
	{
		private readonly ConnectionStringSettings _connectionString;

		public OracleDatabaseMonitor(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;
		}

		public async Task<DatabaseSessions> GetAllSessionDataAsync(CancellationToken cancellationToken)
		{
			var databaseSessions = new DatabaseSessions();

			using (var connection = new OracleConnection(_connectionString.ConnectionString))
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
						var columnCount = databaseSessions.ColumnHeaders.Count;
						while (reader.Read())
						{
							var values = new object[columnCount];
							reader.GetValues(values);
							var sessionType = (string)values[17];
							var sessionId = Convert.ToInt32(values[1]);
							var databaseSession =
								new DatabaseSession
								{
									Id = sessionId,
									Values = values,
									Type = String.Equals(sessionType, "User") ? SessionType.User : SessionType.System,
									IsActive = Convert.ToString(values[9]) == "Active"
								};

							sessions.Add(sessionId, databaseSession);
						}

						foreach (var session in sessions.Values)
						{
							DatabaseSession parentSession;
							var parentSid = session.Values[53];
							if (parentSid != DBNull.Value && sessions.TryGetValue(Convert.ToInt32(parentSid), out parentSession))
							{
								session.ParentSession = parentSession;
								parentSession.ChildSessions.Add(session);
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
	}
}
