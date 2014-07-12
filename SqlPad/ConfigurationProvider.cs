using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace SqlPad
{
	public class ConfigurationProvider
	{
		private static readonly Dictionary<string, IInfrastructureFactory> InternalInfrastructureFactories;

		static ConfigurationProvider()
		{
			var databaseConfiguration = (DatabaseConnectionConfigurationSection)ConfigurationManager.GetSection(DatabaseConnectionConfigurationSection.SectionName);
			if (databaseConfiguration == null)
			{
				throw new ConfigurationErrorsException("'databaseConfiguration' configuration section is missing. ");
			}

			InternalInfrastructureFactories = databaseConfiguration.Infrastructures
				.Cast<InfrastructureConfigurationSection>()
				.ToDictionary(s => s.ConnectionStringName, s => (IInfrastructureFactory)Activator.CreateInstance(GetInfrastuctureFactoryType(s.InfrastructureFactory)));
		}

		private static Type GetInfrastuctureFactoryType(string typeName)
		{
			var infrastructureFactoryType = Type.GetType(typeName);
			if (infrastructureFactoryType == null)
			{
				throw new ConfigurationErrorsException(String.Format("Infrastructure factory type '{0}' has not been found. ", typeName));
			}

			return infrastructureFactoryType;
		}

		public static IInfrastructureFactory GetInfrastructureFactory(string connectionStringName)
		{
			IInfrastructureFactory infrastructureFactory;
			if (InternalInfrastructureFactories.TryGetValue(connectionStringName, out infrastructureFactory))
			{
				return infrastructureFactory;
			}

			throw new ArgumentException(String.Format("Connection string '{0}' doesn't exist. ", connectionStringName), "connectionStringName");
		}

		public static ConnectionStringSettingsCollection ConnectionStrings { get { return ConfigurationManager.ConnectionStrings; } }
	}
}