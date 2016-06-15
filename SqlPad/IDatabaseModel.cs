using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IDatabaseModel : IDisposable
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; set; }

		bool IsInitialized { get; }

		ICollection<string> Schemas { get; }

		bool IsFresh { get; }

		void RefreshIfNeeded();

		Task Initialize();

		Task Refresh(bool force);

		event EventHandler Initialized;

		event EventHandler CurrentSchemaChanged;

		event EventHandler<DatabaseModelPasswordArgs> PasswordRequired;

		event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		event EventHandler RefreshStarted;

		event EventHandler<DatabaseModelRefreshStatusChangedArgs> RefreshStatusChanged;

		event EventHandler RefreshCompleted;

		IConnectionAdapter CreateConnectionAdapter();
	}

	public interface IReferenceDataSource
	{
		IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

		string ObjectName { get; }

		string ConstraintName { get; }

		StatementExecutionModel CreateExecutionModel(object[] keys);
	}

	public interface IValueAggregator
	{
		bool AggregatedValuesAvailable { get; }

		bool LimitValuesAvailable { get; }

		object Minimum { get; }

		object Maximum { get; }

		object Average { get; }

		object Sum { get; }

		Mode Mode { get; }

		object Median { get; }

		long Count { get; }

		long? DistinctCount { get; }

		void AddValue(object value);
	}

	[DebuggerDisplay("Mode (Value={Value}; Count={Count})")]
	public struct Mode
	{
		public static readonly Mode Empty = new Mode();

		public object Value { get; set; }

		public long? Count { get; set; }
	}

	public class DatabaseModelConnectionErrorArgs : EventArgs
	{
		public Exception Exception { get; }

		public DatabaseModelConnectionErrorArgs(Exception exception)
		{
			if (exception == null)
			{
				throw new ArgumentNullException(nameof(exception));
			}
			
			Exception = exception;
		}
	}

	public class DatabaseModelPasswordArgs : EventArgs
	{
		public bool CancelConnection { get; set; }

		public SecureString Password { get; set; }
	}

	public class DatabaseModelRefreshStatusChangedArgs : EventArgs
	{
		public string Message { get; }

		public DatabaseModelRefreshStatusChangedArgs(string message)
		{
			Message = message;
		}
	}
}
