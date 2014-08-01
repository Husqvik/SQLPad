using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace SqlPad
{
	public class ConfigurationProvider
	{
		private static readonly Dictionary<string, ConnectionConfiguration> InternalInfrastructureFactories;

		static ConfigurationProvider()
		{
			var databaseConfiguration = (DatabaseConnectionConfigurationSection)ConfigurationManager.GetSection(DatabaseConnectionConfigurationSection.SectionName);
			if (databaseConfiguration == null)
			{
				throw new ConfigurationErrorsException("'databaseConfiguration' configuration section is missing. ");
			}

			InternalInfrastructureFactories = databaseConfiguration.Infrastructures
				.Cast<InfrastructureConfigurationSection>()
				.ToDictionary(s => s.ConnectionStringName, s => new ConnectionConfiguration(s));
		}

		public static ConnectionConfiguration GetConnectionCofiguration(string connectionStringName)
		{
			ConnectionConfiguration connectionConfiguration;
			if (InternalInfrastructureFactories.TryGetValue(connectionStringName, out connectionConfiguration))
			{
				return connectionConfiguration;
			}

			throw new ArgumentException(String.Format("Connection string '{0}' doesn't exist. ", connectionStringName), "connectionStringName");
		}

		public static ConnectionStringSettingsCollection ConnectionStrings { get { return ConfigurationManager.ConnectionStrings; } }
	}

	public class ConnectionConfiguration
	{
		internal ConnectionConfiguration(InfrastructureConfigurationSection infrastructureConfigurationSection)
		{
			InfrastructureFactory = (IInfrastructureFactory)Activator.CreateInstance(GetInfrastuctureFactoryType(infrastructureConfigurationSection.InfrastructureFactory));
			IsProduction = infrastructureConfigurationSection.IsProduction;
		}

		public IInfrastructureFactory InfrastructureFactory { get; private set; }

		public bool IsProduction { get; private set; }

		private static Type GetInfrastuctureFactoryType(string typeName)
		{
			var infrastructureFactoryType = Type.GetType(typeName);
			if (infrastructureFactoryType == null)
			{
				throw new ConfigurationErrorsException(String.Format("Infrastructure factory type '{0}' has not been found. ", typeName));
			}

			return infrastructureFactoryType;
		}
	}
}
