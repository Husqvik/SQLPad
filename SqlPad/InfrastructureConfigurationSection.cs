using System.Configuration;

namespace SqlPad
{
	public class InfrastructureConfigurationSection : ConfigurationSection
	{
		public const string SectionName = "infrastructureConfiguration";

		[ConfigurationProperty("ConnectionStringName", IsRequired = true)]
		public string ConnectionStringName 
		{
			get { return (string)this["ConnectionStringName"]; }
			set { this["ConnectionStringName"] = value; }
		}

		[ConfigurationProperty("InfrastructureFactory", IsRequired = true)]
		public string InfrastructureFactory
		{
			get { return (string)this["InfrastructureFactory"]; }
			set { this["InfrastructureFactory"] = value; }
		}
	}
}