using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace SqlPad
{
	public class DatabaseProviderConfiguration
	{
		private Dictionary<string, BindVariableConfiguration> _bindVariables;
		private HashSet<StatementExecutionHistoryEntry> _statementExecutionHistory;
		private DatabaseMonitorConfiguration _databaseMonitorConfiguration;

		public DatabaseProviderConfiguration(string providerName)
		{
			ProviderName = providerName;
		}

		public string ProviderName { get; private set; }

		private IDictionary<string, BindVariableConfiguration> BindVariablesInternal => _bindVariables ?? (_bindVariables = new Dictionary<string, BindVariableConfiguration>());

		public ICollection<StatementExecutionHistoryEntry> StatementExecutionHistory => _statementExecutionHistory ?? (_statementExecutionHistory = new HashSet<StatementExecutionHistoryEntry>());

		public DatabaseMonitorConfiguration DatabaseMonitorConfiguration => _databaseMonitorConfiguration ?? (_databaseMonitorConfiguration = new DatabaseMonitorConfiguration { UserSessionOnly = true });

		public ICollection<BindVariableConfiguration> BindVariables => BindVariablesInternal.Values;

		public BindVariableConfiguration GetBindVariable(string variableName)
		{
			return BindVariablesInternal.TryGetValue(variableName, out BindVariableConfiguration bindVariable)
				? bindVariable
				: null;
		}

		public void SetBindVariable(BindVariableConfiguration bindVariable)
		{
			BindVariablesInternal[bindVariable.Name] = bindVariable;
		}

		public void RemoveBindVariable(string bindVariableName)
		{
			BindVariablesInternal.Remove(bindVariableName);
		}

		public void AddStatementExecution(StatementExecutionHistoryEntry entry)
		{
			var maximumHistoryEntries = ConfigurationProvider.Configuration.Miscellaneous.MaximumHistoryEntries;

			if (!StatementExecutionHistory.Remove(entry) && StatementExecutionHistory.Count >= maximumHistoryEntries)
			{
				var recordsToRemove = StatementExecutionHistory.OrderByDescending(r => r.ExecutedAt).Skip(maximumHistoryEntries - 1).ToArray();
				foreach (var oldRecord in recordsToRemove)
				{
					StatementExecutionHistory.Remove(oldRecord);
				}

				TraceLog.WriteLine($"Statement execution history limit of {maximumHistoryEntries} entries has been reached. Oldest entries have been removed. ");
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

	public class DatabaseMonitorConfiguration
	{
		public string SortMemberPath { get; set; }

		public ListSortDirection SortColumnOrder { get; set; }

		public bool ActiveSessionOnly { get; set; }

		public bool UserSessionOnly { get; set; }

		public bool MasterSessionOnly { get; set; }
	}

	[DebuggerDisplay("StatementExecutionHistoryEntry (StatementText={StatementText}, ExecutedAt={ExecutedAt}, Tags={Tags})")]
	public class StatementExecutionHistoryEntry
	{
		public string StatementText { get; private set; }

		public DateTime ExecutedAt { get; private set; }

		public string Tags { get; set; }

		public StatementExecutionHistoryEntry(string statementText, DateTime executedAt)
		{
			StatementText = statementText;
			ExecutedAt = executedAt;
		}

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
		private object _value;

		public string Name { get; set; }

		public string DataType { get; set; }

		public bool IsFilePath { get; set; }

		public object Value
		{
			get
			{
				if (_value != null)
				{
					return _value;
				}

				if (_internalValue == null)
				{
					return null;
				}

				using (var stream = new MemoryStream(_internalValue))
				{
					return _value = Formatter.Deserialize(stream);
				}
			}
			set
			{
				if (value == null)
				{
					_internalValue = null;
					return;
				}

				if (Equals(_value, value))
				{
					return;
				}

				_value = value;

				using (var stream = new MemoryStream())
				{
					Formatter.Serialize(stream, value);
					stream.Seek(0, SeekOrigin.Begin);
					_internalValue = stream.ToArray();
				}
			}
		}

		public ReadOnlyDictionary<string, BindVariableType> DataTypes { get; set; }

		public IReadOnlyList<StatementGrammarNode> Nodes { get; set; }
	}

	public class BindVariableType
	{
		public string Name { get; }

		public bool HasFileSupport { get; }

		public Type InputType { get; }

		public BindVariableType(string name, Type inputType, bool hasFileSupport)
		{
			Name = name;
			InputType = inputType;
			HasFileSupport = hasFileSupport;
		}

		protected bool Equals(BindVariableType other)
		{
			return string.Equals(Name, other.Name, StringComparison.InvariantCulture) && HasFileSupport == other.HasFileSupport && InputType == other.InputType;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((BindVariableType)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = StringComparer.InvariantCulture.GetHashCode(Name);
				hashCode = (hashCode * 397) ^ HasFileSupport.GetHashCode();
				hashCode = (hashCode * 397) ^ InputType.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(BindVariableType left, BindVariableType right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(BindVariableType left, BindVariableType right)
		{
			return !Equals(left, right);
		}
	}
}
