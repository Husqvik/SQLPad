using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle
{
	public static class OracleCommandExtensions
	{
		internal static OracleCommand AddSimpleParameter(this OracleCommand command, string parameterName, object value, string databaseType = null)
		{
			var parameter = command.CreateParameter();
			parameter.ParameterName = parameterName;
			parameter.Direction = System.Data.ParameterDirection.InputOutput;
			parameter.Value = Equals(value, String.Empty) ? null : value;

			switch (databaseType)
			{
				case OracleBindVariable.DataTypeChar:
					parameter.OracleDbType = OracleDbType.Char;
					break;
				case OracleBindVariable.DataTypeClob:
					parameter.OracleDbType = OracleDbType.Clob;
					break;
				case OracleBindVariable.DataTypeDate:
					parameter.OracleDbType = OracleDbType.Date;
					break;
				case OracleBindVariable.DataTypeNumber:
					parameter.OracleDbType = OracleDbType.Decimal;
					break;
				case OracleBindVariable.DataTypeUnicodeChar:
					parameter.OracleDbType = OracleDbType.NChar;
					break;
				case OracleBindVariable.DataTypeUnicodeClob:
					parameter.OracleDbType = OracleDbType.NClob;
					break;
				case OracleBindVariable.DataTypeUnicodeVarchar2:
					parameter.OracleDbType = OracleDbType.NVarchar2;
					break;
				case OracleBindVariable.DataTypeVarchar2:
					parameter.OracleDbType = OracleDbType.Varchar2;
					break;
			}

			command.Parameters.Add(parameter);

			return command;
		}

		public static Task<bool> ReadAsynchronous(this OracleDataReader reader, CancellationToken cancellationToken)
		{
			return ExecuteCommandAsynchronous(reader.Close, reader.Read, cancellationToken);
		}

		public static Task<int> ExecuteNonQueryAsynchronous(this OracleCommand command, CancellationToken cancellationToken)
		{
			return ExecuteCommandAsynchronous(command.CancelIgnoreFailure, command.ExecuteNonQuery, cancellationToken);
		}

		public static Task<object> ExecuteScalarAsynchronous(this OracleCommand command, CancellationToken cancellationToken)
		{
			return ExecuteCommandAsynchronous(command.CancelIgnoreFailure, command.ExecuteScalar, cancellationToken);
		}

		public static Task<OracleDataReader> ExecuteReaderAsynchronous(this OracleCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
		{
			return ExecuteCommandAsynchronous(command.CancelIgnoreFailure, () => command.ExecuteReader(behavior), cancellationToken);
		}

		private static Task<T> ExecuteCommandAsynchronous<T>(Action cancellationAction, Func<T> synchronousOperation, CancellationToken cancellationToken)
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
			return value.IsNull() ? null : (string)value;
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
