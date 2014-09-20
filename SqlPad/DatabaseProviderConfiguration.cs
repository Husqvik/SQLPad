using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SqlPad
{
	public class DatabaseProviderConfiguration
	{
		private readonly Dictionary<string, BindVariableConfiguration> _bindVariables = new Dictionary<string, BindVariableConfiguration>();

		public DatabaseProviderConfiguration(string providerName)
		{
			ProviderName = providerName;
		}

		public string ProviderName { get; private set; }

		public ICollection<BindVariableConfiguration> BindVariables
		{
			get { return _bindVariables.Values; }
		}

		public BindVariableConfiguration GetBindVariable(string variableName)
		{
			BindVariableConfiguration bindVariable;
			return _bindVariables.TryGetValue(variableName, out bindVariable)
				? bindVariable
				: null;
		}

		public void SetBindVariable(BindVariableConfiguration bindVariable)
		{
			_bindVariables[bindVariable.Name] = bindVariable;
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

		public IDictionary<string, Type> DataTypes { get; set; }
		
		public ICollection<StatementGrammarNode> Nodes { get; set; }
	}
}
