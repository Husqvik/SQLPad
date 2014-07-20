using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle
{
	public static class OracleCommandExtensions
	{
		internal static OracleCommand AddSimpleParameter(this OracleCommand command, string parameterName, object value)
		{
			var parameter = command.CreateParameter();
			parameter.ParameterName = parameterName;
			parameter.Value = value;

			command.Parameters.Add(parameter);

			return command;
		}

		public static Task<OracleDataReader> ExecuteReaderAsynchronous(this OracleCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
		{
			var source = new TaskCompletionSource<OracleDataReader>();
			var registration = new CancellationTokenRegistration();

			if (cancellationToken.CanBeCanceled)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					source.SetCanceled();
					return source.Task;
				}
				
				registration = cancellationToken.Register(command.CancelIgnoreFailure);
			}
			
			var mainTask = source.Task;

			try
			{
				Task.Factory.StartNew(() => command.ExecuteReader(behavior), cancellationToken)
					.ContinueWith(t => AfterExecutionHandler(t, source, registration), CancellationToken.None);
			}
			catch (Exception exception)
			{
				source.SetException(exception);
			}

			return mainTask;
		}

		private static void AfterExecutionHandler(Task<OracleDataReader> executionTask, TaskCompletionSource<OracleDataReader> source, CancellationTokenRegistration registration)
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
}
