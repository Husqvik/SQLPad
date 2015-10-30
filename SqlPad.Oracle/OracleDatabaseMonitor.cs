using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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

					using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
					{
						databaseSessions.ColumnHeaders = OracleConnectionAdapter.GetColumnHeadersFromReader(reader);
						var rows = await OracleConnectionAdapter.FetchRecordsFromReader(reader, Int32.MaxValue, true).EnumerateAsync(cancellationToken);

						var sessions = new List<DatabaseSession>();
						foreach (var row in rows)
						{
							var sessionType = (OracleSimpleValue)row[17];
							sessions.Add(new DatabaseSession { Values = row, Type = String.Equals((string)sessionType.RawValue, "User") ? SessionType.User : SessionType.System });
						}

						databaseSessions.Rows = sessions.AsReadOnly();
					}
				}

				await connection.CloseAsynchronous(cancellationToken);
			}

			return databaseSessions;
		}
	}
}
