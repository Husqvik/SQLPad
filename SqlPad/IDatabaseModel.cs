using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IDatabaseModel : IDisposable
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; set; }

		ICollection<string> Schemas { get; }

		bool CanFetch { get; }

		bool IsExecuting { get; }

		bool IsModelFresh { get; }

		void RefreshIfNeeded();

		Task Refresh(bool force);

		event EventHandler RefreshStarted;

		event EventHandler RefreshFinished;

		StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel);

		Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Task<string> GetExecutionPlanAsync(CancellationToken cancellationToken);

		IEnumerable<object[]> FetchRecords(int rowCount);

		ICollection<ColumnHeader> GetColumnHeaders();
	}

	public class ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }

		public IColumnValueConverter ValueConverter { get; set; }
	}

	public interface IColumnValueConverter
	{
		object ConvertToCellValue(object rawValue);
	}
}
