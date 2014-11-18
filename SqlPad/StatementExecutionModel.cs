using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SqlPad
{
	public class StatementExecutionModel
	{
		public const int DefaultRowBatchSize = 100;
		
		public StatementExecutionModel()
		{
			InitialFetchRowCount = DefaultRowBatchSize;
		}

		public string StatementText { get; set; }
		
		public ICollection<BindVariableModel> BindVariables { get; set; }
		
		public bool GatherExecutionStatistics { get; set; }
		
		public int InitialFetchRowCount { get; set; }
	}

	public struct ExplainPlanResult
	{
		public IReadOnlyCollection<ColumnHeader> ColumnHeaders { get; set; }

		public IReadOnlyCollection<object[]> ResultSet { get; set; }
	}

	public struct StatementExecutionResult
	{
		public int AffectedRowCount { get; set; }

		public bool ExecutedSuccessfully { get; set; }

		public IReadOnlyList<ColumnHeader> ColumnHeaders { get; set; }

		public IReadOnlyList<object[]> InitialResultSet { get; set; }
		
		public string DatabaseOutput { get; set; }
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
}