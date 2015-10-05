using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	public class StatementBatchExecutionModel
	{
		public bool GatherExecutionStatistics { get; set; }
		
		public bool EnableDebug { get; set; }

		public IReadOnlyList<StatementExecutionModel> Statements { get; set; }
	}

	public class StatementExecutionModel
	{
		public string StatementText { get; set; }

		public IValidationModel ValidationModel { get; set; }

		public StatementBase Statement => ValidationModel?.Statement;

		public IReadOnlyList<BindVariableModel> BindVariables { get; set; }

		public bool IsPartialStatement { get; set; }
	}

	public class StatementExecutionBatchResult
	{
		public StatementBatchExecutionModel ExecutionModel { get; set; }

		public IReadOnlyList<StatementExecutionResult> StatementResults { get; set; }

		public string DatabaseOutput { get; set; }
	}

	public class StatementExecutionResult
	{
		public StatementExecutionModel StatementModel { get; set; }

		public int? AffectedRowCount { get; set; }

		public int? ErrorPosition { get; set; }

		public Exception Exception { get; set; }

		public bool ExecutedSuccessfully => Exception == null;

		public DateTime? ExecutedAt { get; set; }

		public TimeSpan? Duration { get; set; }

		public IReadOnlyList<CompilationError> CompilationErrors { get; set; }

		public IReadOnlyDictionary<ResultInfo, IReadOnlyList<ColumnHeader>> ResultInfoColumnHeaders { get; set; }

		public string SuccessfulExecutionMessage { get; set; }
    }

	public class StatementExecutionException : Exception
	{
		public StatementExecutionBatchResult BatchResult { get; }

		public StatementExecutionException(StatementExecutionBatchResult batchResult, Exception inner) : base(inner.Message, inner)
		{
			BatchResult = batchResult;
		}
	}

	[DebuggerDisplay("SessionExecutionStatisticsRecord (Name={Name}; Value={Value})")]
	public class SessionExecutionStatisticsRecord : ModelBase
	{
		private decimal _value;

		public string Name { get; set; }

		public decimal Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}
	}

	public class SessionExecutionStatisticsCollection : ObservableCollection<SessionExecutionStatisticsRecord>
	{
		private readonly Dictionary<string, SessionExecutionStatisticsRecord> _statisticsRecordDictionary = new Dictionary<string, SessionExecutionStatisticsRecord>();

		public void MergeWith(IEnumerable<SessionExecutionStatisticsRecord> records)
		{
			foreach (var newRecord in records)
			{
				SessionExecutionStatisticsRecord existingRecord;
				if (_statisticsRecordDictionary.TryGetValue(newRecord.Name, out existingRecord))
				{
					existingRecord.Value = newRecord.Value;
				}
				else
				{
					_statisticsRecordDictionary.Add(newRecord.Name, newRecord);
					base.Add(newRecord);
				}
			}
		}

		public new void Clear()
		{
			base.Clear();
			_statisticsRecordDictionary.Clear();
		}

		public new void Add(SessionExecutionStatisticsRecord record)
		{
			MergeWith(Enumerable.Repeat(record, 1));
		}

		public new void RemoveAt(int index)
		{
			throw new NotSupportedException();
		}

		public new void Move(int oldIndex, int newIndex)
		{
			throw new NotSupportedException();
		}

		public new void Remove(SessionExecutionStatisticsRecord record)
		{
			throw new NotSupportedException();
		}
	}

	[DebuggerDisplay("CompilationError (Object={Owner + \".\" + ObjectName}; ObjectType={ObjectType}; Severity={Severity}; Line={Line}; Column={Column}; Message={Message})")]
	public class CompilationError
	{
		public static readonly CompilationError[] EmptyArray = new CompilationError[0];

		public string Owner { get; set; }

		public string ObjectName { get; set; }

		public string ObjectType { get; set; }

		public int Code { get; set; }

		public int Line { get; set; }

		public int Column { get; set; }

		public string Message { get; set; }

		public string Severity { get; set; }
		
		public StatementBase Statement { get; set; }
	}
}
