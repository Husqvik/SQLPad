using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad.Oracle
{
	public partial class OracleConfiguration
	{
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(OracleConfiguration));
		private static readonly string ConfigurationFilePath = Path.Combine(ConfigurationProvider.FolderNameApplication, "OracleConfiguration.xml");

		public static readonly OracleConfiguration Default =
			new OracleConfiguration
			{
				executionPlanField =
					new OracleConfigurationExecutionPlan
					{
						TargetTable = new OracleConfigurationExecutionPlanTargetTable()
					}
			};

		public static OracleConfiguration Configuration { get; private set; }

		static OracleConfiguration()
		{
			Configuration = Default;

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
			}
			catch (Exception e)
			{
				Trace.WriteLine("Configuration loading failed: " + e);
			}
		}
	}
}