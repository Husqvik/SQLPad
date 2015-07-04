using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif

using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	public static class OracleCommandExtensions
	{
		internal static OracleCommand AddSimpleParameter(this OracleCommand command, string parameterName, object value, string databaseType = null, int? size = null)
		{
			var parameter = command.CreateParameter();
			parameter.ParameterName = parameterName;
			parameter.Direction = ParameterDirection.InputOutput;
			parameter.Value = Equals(value, String.Empty) ? null : value;

			if (size.HasValue)
			{
				parameter.Size = size.Value;
			}

			switch (databaseType)
			{
				case TerminalValues.Char:
					parameter.OracleDbType = OracleDbType.Char;
					break;
				case TerminalValues.Clob:
					parameter.OracleDbType = OracleDbType.Clob;
					break;
				case TerminalValues.Timestamp:
					parameter.OracleDbType = OracleDbType.TimeStamp;
					break;
				case TerminalValues.Date:
					parameter.OracleDbType = OracleDbType.Date;
					break;
				case TerminalValues.Number:
					parameter.OracleDbType = OracleDbType.Decimal;
					break;
				case TerminalValues.NChar:
					parameter.OracleDbType = OracleDbType.NChar;
					break;
				case OracleBindVariable.DataTypeUnicodeClob:
					parameter.OracleDbType = OracleDbType.NClob;
					break;
				case TerminalValues.NVarchar2:
					parameter.OracleDbType = OracleDbType.NVarchar2;
					break;
				case TerminalValues.Varchar2:
					parameter.OracleDbType = OracleDbType.Varchar2;
					break;
			}

			command.Parameters.Add(parameter);

			return command;
		}

		public static Task<bool> ReadAsynchronous(this OracleDataReader reader, CancellationToken cancellationToken)
		{
			return ExecuteAsynchronous(reader.Close, reader.Read, cancellationToken);
		}

		public static Task<int> ExecuteNonQueryAsynchronous(this OracleCommand command, CancellationToken cancellationToken)
		{
			return ExecuteAsynchronous(command.CancelIgnoreFailure, command.ExecuteNonQuery, cancellationToken);
		}

		public static Task<object> ExecuteScalarAsynchronous(this OracleCommand command, CancellationToken cancellationToken)
		{
			return ExecuteAsynchronous(command.CancelIgnoreFailure, command.ExecuteScalar, cancellationToken);
		}

		public static Task<OracleDataReader> ExecuteReaderAsynchronous(this OracleCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
		{
			return ExecuteAsynchronous(command.CancelIgnoreFailure, () => command.ExecuteReader(behavior), cancellationToken);
		}

		public static Task OpenAsynchronous(this OracleConnection connection, CancellationToken cancellationToken)
		{
			return ExecuteAsynchronous<object>(delegate { /* ODAC does not support cancellation of a connection being opened. */ }, delegate { connection.Open(); return null; }, cancellationToken);
		}

		public static async Task<bool> EnsureConnectionOpen(this OracleConnection connection, CancellationToken cancellationToken)
		{
			if (connection.State == ConnectionState.Open)
			{
				return false;
			}

			await connection.OpenAsynchronous(cancellationToken);
			return true;
		}

		public static async Task SetSchema(this OracleCommand command, string schema, CancellationToken cancellationToken)
		{
			command.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = \"{0}\"", schema);
			await command.ExecuteNonQueryAsynchronous(cancellationToken);
		}

		private static Task<T> ExecuteAsynchronous<T>(Action cancellationAction, Func<T> synchronousOperation, CancellationToken cancellationToken)
		{
			var source = new TaskCompletionSource<T>();
			var registration = new CancellationTokenRegistration();

			if (cancellationToken.CanBeCanceled)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					source.SetCanceled();
					return source.Task;
				}

				registration = cancellationToken.Register(cancellationAction);
			}

			try
			{
				Task.Factory.StartNew(synchronousOperation, cancellationToken)
					.ContinueWith(t => AfterExecutionHandler(t, source, registration), CancellationToken.None);
			}
			catch (Exception exception)
			{
				source.SetException(exception);
			}

			return source.Task;
		}

		private static void AfterExecutionHandler<T>(Task<T> executionTask, TaskCompletionSource<T> source, CancellationTokenRegistration registration)
		{
			registration.Dispose();

			if (executionTask.IsFaulted)
			{
				source.SetException(executionTask.Exception.InnerException);
			}
			else if (executionTask.IsCanceled)
			{
				source.SetCanceled();
			}
			else
			{
				source.SetResult(executionTask.Result);
			}
		}

		private static void CancelIgnoreFailure(this OracleCommand command)
		{
			try
			{
				command.Cancel();
			}
			catch { }
		}
	}

	public static class OracleReaderValueConvert
	{
		public static int? ToInt32(object value)
		{
			return value.IsNull() ? null : (int?)Convert.ToInt32(value);
		}

		public static long? ToInt64(object value)
		{
			return value.IsNull() ? null : (long?)Convert.ToInt64(value);
		}

		public static decimal? ToDecimal(object value)
		{
			return value.IsNull() ? null : (decimal?)Convert.ToDecimal(value);
		}

		public static DateTime? ToDateTime(object value)
		{
			return value.IsNull() ? null : (DateTime?)Convert.ToDateTime(value);
		}

		public static string ToString(object value)
		{
			return value.IsNull() ? String.Empty : (string)value;
		}

		public static bool? ToBoolean(object value)
		{
			return value.IsNull() ? null : (bool?)value;
		}

		private static bool IsNull(this object value)
		{
			return value == DBNull.Value;
		}
	}
}
