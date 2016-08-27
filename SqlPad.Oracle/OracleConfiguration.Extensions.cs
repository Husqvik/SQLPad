using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle
{
	public partial class OracleConfiguration
	{
		private const string AllConnections = "*";
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(OracleConfiguration));
		private static readonly string ConfigurationFilePath = Path.Combine(ConfigurationProvider.FolderNameApplication, "OracleConfiguration.xml");

		public static OracleConfiguration Configuration { get; }

		private IReadOnlyDictionary<string, OracleConfigurationConnection> _connectionConfigurations = new Dictionary<string, OracleConfigurationConnection>().AsReadOnly();

		public string GetRemoteTraceDirectory(string connectionName)
		{
			OracleConfigurationConnection configuration;
			return
				(_connectionConfigurations.TryGetValue(connectionName, out configuration) && !String.IsNullOrWhiteSpace(configuration.RemoteTraceDirectory)) ||
				_connectionConfigurations.TryGetValue(AllConnections, out configuration)
					? configuration.RemoteTraceDirectory
					: String.Empty;
		}

		public string GetConnectionStartupScript(string connectionName)
		{
			OracleConfigurationConnection configuration;
			return _connectionConfigurations.TryGetValue(connectionName, out configuration)
				? configuration.StartupScript
				: String.Empty;
		}

		public OracleObjectIdentifier GetExplainPlanTargetTable(string connectionName)
		{
			OracleConfigurationConnection configuration;
			if ((_connectionConfigurations.TryGetValue(connectionName, out configuration) && configuration.ExecutionPlan?.TargetTable != null) ||
				_connectionConfigurations.TryGetValue(AllConnections, out configuration) && configuration.ExecutionPlan?.TargetTable != null)
			{
				var targetTable = configuration.ExecutionPlan.TargetTable;
				return OracleObjectIdentifier.Create(targetTable.Schema, targetTable.Name);
			}

			return OracleObjectIdentifier.Empty;
		}

		static OracleConfiguration()
		{
			var defaultFormatSettings =
				new OracleConfigurationFormatter
				{
					FormatOptions = new OracleConfigurationFormatterFormatOptions()
				};

			Configuration =
				new OracleConfiguration
				{
					Connections = new OracleConfigurationConnection[0],
					Formatter = defaultFormatSettings
				};

			if (!File.Exists(ConfigurationFilePath))
			{
				return;
			}

			try
			{
				using (var reader = XmlReader.Create(ConfigurationFilePath))
				{
					Configuration = (OracleConfiguration)XmlSerializer.Deserialize(reader);
				}

				if (Configuration.Connections != null)
				{
					Configuration._connectionConfigurations = Configuration.Connections.ToDictionary(c => c.ConnectionName);
				}

				if (Configuration.Formatter == null)
				{
					Configuration.Formatter = defaultFormatSettings;
				}
			}
			catch (Exception e)
			{
				Trace.WriteLine("Configuration loading failed: " + e);
			}
		}
	}

	public partial class OracleConfigurationFormatterFormatOptions
	{
		public OracleConfigurationFormatterFormatOptions()
		{
			Reset();
		}

		public void Reset()
		{
			Identifier = FormatOption.Keep;
			Alias = FormatOption.Keep;
			Keyword = FormatOption.Keep;
			ReservedWord = FormatOption.Keep;
		}
	}
}