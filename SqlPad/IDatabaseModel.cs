using System;
using System.Collections.Generic;
using System.Configuration;

namespace SqlPad
{
	public interface IDatabaseModel : IDisposable
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; set; }

		ICollection<string> Schemas { get; }

		bool CanExecute { get; }

		bool CanFetch { get; }

		bool IsExecuting { get; }

		void Refresh();

		event EventHandler RefreshStarted;

		event EventHandler RefreshFinished;

		void ExecuteStatement(string commandText);

		IEnumerable<object[]> FetchRecords(int rowCount);

		ICollection<ColumnHeader> GetColumnHeaders();
	}

	public interface IDatabaseObject
	{
		string Name { get; }

		string Type { get; }

		string Owner { get; }
	}

	public struct ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }
	}

	public interface IColumn
	{
		string Name { get; }

		string FullTypeName { get; }
	}
}
