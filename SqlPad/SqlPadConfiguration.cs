using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using ProtoBuf.Meta;

namespace SqlPad
{
	public class SqlPadConfiguration
	{
		private static readonly RuntimeTypeModel Serializer;
		private static readonly Dictionary<string, SqlPadConfiguration> ProviderConfigurations = new Dictionary<string, SqlPadConfiguration>();

		static SqlPadConfiguration()
		{
			Serializer = TypeModel.Create();
			var sqlPadConfigurationType = Serializer.Add(typeof(SqlPadConfiguration), false);
			sqlPadConfigurationType.UseConstructor = false;
			sqlPadConfigurationType.Add("_bindVariables", "ProviderName");

			var bindVariableConfigurationType = Serializer.Add(typeof(BindVariableConfiguration), false);
			bindVariableConfigurationType.Add("Name", "DataType", "_internalValue");
		}

		private readonly Dictionary<string, BindVariableConfiguration> _bindVariables = new Dictionary<string, BindVariableConfiguration>();
		private bool _isModified;

		private SqlPadConfiguration(string providerName)
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
			_isModified = true;
		}

		public static SqlPadConfiguration GetConfiguration(string providerName)
		{
			SqlPadConfiguration configuration;
			if (!ProviderConfigurations.TryGetValue(providerName, out configuration))
			{
				var fileName = GetFileName(providerName);
				if (File.Exists(fileName))
				{
					using (var file = File.OpenRead(fileName))
					{
						try
						{
							configuration = (SqlPadConfiguration) Serializer.Deserialize(file, new SqlPadConfiguration(providerName), typeof (SqlPadConfiguration));
						}
						catch (Exception e)
						{
							Trace.WriteLine("SqlPadConfiguration deserialization failed: " + e);
						}
					}
				}
				else
				{
					configuration = new SqlPadConfiguration(providerName);
				}
				
				ProviderConfigurations.Add(providerName, configuration);
			}

			return configuration;
		}

		public static void StoreConfiguration()
		{
			foreach (var configuration in ProviderConfigurations.Values.Where(c => c._isModified))
			{
				using (var file = File.Create(configuration.FileName))
				{
					Serializer.Serialize(file, configuration);
				}
			}
		}

		private string FileName
		{
			get { return GetFileName(ProviderName); }
		}

		private static string GetFileName(string providerName)
		{
			return Path.Combine(ConfigurationProvider.FolderNameWorkArea, String.Format("{0}.dat", providerName));
		}
	}

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
	}
}
