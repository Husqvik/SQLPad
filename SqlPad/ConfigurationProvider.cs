using System;
using System.Configuration;

namespace SqlPad
{
	public class ConfigurationProvider
	{
		private static readonly IInfrastructureFactory InternalInfrastructureFactory;

		static ConfigurationProvider()
		{
			var infrastructureConfigurationSection = (InfrastructureConfigurationSection)ConfigurationManager.GetSection(InfrastructureConfigurationSection.SectionName);
			if (infrastructureConfigurationSection == null)
			{
				throw new ConfigurationErrorsException("'infrastructureConfiguration' configuration section is missing. ");
			}

			var infrastructureFactoryType = Type.GetType(infrastructureConfigurationSection.InfrastructureFactory);
			if (infrastructureFactoryType == null)
			{
				throw new ConfigurationErrorsException(String.Format("Infrastructure factory type '{0}' has not been found. ", infrastructureConfigurationSection.InfrastructureFactory));
			}
			
			InternalInfrastructureFactory = (IInfrastructureFactory)Activator.CreateInstance(infrastructureFactoryType);
		}

		public static IInfrastructureFactory InfrastructureFactory { get { return InternalInfrastructureFactory; } }

		public static ConnectionStringSettingsCollection ConnectionStrings { get { return ConfigurationManager.ConnectionStrings; } }
	}
}