using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace SqlPad
{
	public class DatabaseProviderConfiguration
	{
		private const int MaxExecutedStatementHistoryLength = 1000;

		private Dictionary<string, BindVariableConfiguration> _bindVariables;
		private HashSet<StatementExecutionHistoryEntry> _statementExecutionHistory;

		public DatabaseProviderConfiguration(string providerName)
		{
			ProviderName = providerName;
		}

		public string ProviderName { get; private set; }

		public IDictionary<string, BindVariableConfiguration> BindVariablesInternal => _bindVariables ?? (_bindVariables = new Dictionary<string, BindVariableConfiguration>());

		public ICollection<StatementExecutionHistoryEntry> StatementExecutionHistory => _statementExecutionHistory ?? (_statementExecutionHistory = new HashSet<StatementExecutionHistoryEntry>());

		public ICollection<BindVariableConfiguration> BindVariables => BindVariablesInternal.Values;

		public BindVariableConfiguration GetBindVariable(string variableName)
		{
			BindVariableConfiguration bindVariable;
			return BindVariablesInternal.TryGetValue(variableName, out bindVariable)
				? bindVariable
				: null;
		}

		public void SetBindVariable(BindVariableConfiguration bindVariable)
		{
			BindVariablesInternal[bindVariable.Name] = bindVariable;
		}

		public void AddStatementExecution(StatementExecutionHistoryEntry entry)
		{
			if (!StatementExecutionHistory.Remove(entry) && StatementExecutionHistory.Count >= MaxExecutedStatementHistoryLength)
			{
				var recordsToRemove = StatementExecutionHistory.OrderByDescending(r => r.ExecutedAt).Skip(MaxExecutedStatementHistoryLength - 1).ToArray();
				foreach (var oldRecord in recordsToRemove)
				{
					StatementExecutionHistory.Remove(oldRecord);
				}

				Trace.WriteLine($"Statement execution history limit of {MaxExecutedStatementHistoryLength} entries has been reached. Oldest entries have been removed. ");
			}

			StatementExecutionHistory.Add(entry);
		}

		public void RemoveStatementExecutionHistoryEntry(StatementExecutionHistoryEntry entry)
		{
			StatementExecutionHistory.Remove(entry);
		}

		public void ClearStatementExecutionHistory()
		{
			StatementExecutionHistory.Clear();
		}
	}

	[DebuggerDisplay("StatementExecutionHistoryEntry (StatementText={StatementText}, ExecutedAt={ExecutedAt})")]
	public class StatementExecutionHistoryEntry
	{
		public string StatementText { get; set; }

		public DateTime ExecutedAt { get; set; }

		protected bool Equals(StatementExecutionHistoryEntry other)
		{
			return String.Equals(StatementText, other.StatementText);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			if (ReferenceEquals(this, obj))
			{
				return true;
			}
			
			return obj.GetType() == GetType() && Equals((StatementExecutionHistoryEntry)obj);
		}

		public override int GetHashCode()
		{
			return StatementText.GetHashCode();
		}
	}

	[DebuggerDisplay("BindVariableConfiguration (Name={Name}, DataType={DataType}, Value={Value})")]
	public class BindVariableConfiguration
	{
		private static readonly BinaryFormatter Formatter = new BinaryFormatter();
		private byte[] _internalValue;

		public string Name { get; set; }
		
		public string DataType { get; set; }

		public object Value
		{
			get
			{
				if (_internalValue == null)
					return null;

				using (var stream = new MemoryStream(_internalValue))
				{
					return Formatter.Deserialize(stream);
				}
			}
			set
			{
				if (value == null)
				{
					_internalValue = null;
					return;
				}

				using (var stream = new MemoryStream())
				{
					Formatter.Serialize(stream, value);
					stream.Seek(0, SeekOrigin.Begin);
					_internalValue = stream.ToArray();
				}
			}
		}

		public ReadOnlyDictionary<string, Type> DataTypes { get; set; }
		
		public IReadOnlyList<StatementGrammarNode> Nodes { get; set; }
	}
}
