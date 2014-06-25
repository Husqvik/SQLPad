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

		void RefreshIfNeeded();
		
		void Refresh();

		event EventHandler RefreshStarted;

		event EventHandler RefreshFinished;

		int ExecuteStatement(string statementText, bool returnDataset);

		IEnumerable<object[]> FetchRecords(int rowCount);

		ICollection<ColumnHeader> GetColumnHeaders();
	}

	public interface IDatabaseObject
	{
		string Name { get; }

		string Type { get; }

		string Owner { get; }
	}

	public class ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }

		public Func<ColumnHeader, object, object> ValueConverterFunction { get; set; }
	}

	public interface IColumn
	{
		string Name { get; }

		string FullTypeName { get; }
	}
}
