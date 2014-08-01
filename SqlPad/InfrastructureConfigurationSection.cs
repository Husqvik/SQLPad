using System;
using System.Configuration;
using System.Diagnostics;

namespace SqlPad
{
	public class DatabaseConnectionConfigurationSection : ConfigurationSection
	{
		public const string SectionName = "databaseConnectionConfiguration";
		private const string ElementInfrastructureConfigurations = "infrastructureConfigurations";

		[ConfigurationProperty(ElementInfrastructureConfigurations)]
		public InfrastructureConfigurationElementCollection Infrastructures
		{
			get { return base[ElementInfrastructureConfigurations] as InfrastructureConfigurationElementCollection; }
		}
	}

	[DebuggerDisplay("InfrastructureConfigurationSection (ConnectionStringName={ConnectionStringName}, InfrastructureFactory={InfrastructureFactory})")]
	public class InfrastructureConfigurationSection : ConfigurationSection
	{
		private const string AttributeConnectionStringName = "ConnectionStringName";
		private const string AttributeInfrastructureFactory = "InfrastructureFactory";
		private const string AttributeIsProduction = "IsProduction";
		public const string SectionName = "infrastructureConfiguration";

		[ConfigurationProperty(AttributeConnectionStringName, IsRequired = true)]
		public string ConnectionStringName 
		{
			get { return (string)this[AttributeConnectionStringName]; }
			set { this[AttributeConnectionStringName] = value; }
		}

		[ConfigurationProperty(AttributeInfrastructureFactory, IsRequired = true)]
		public string InfrastructureFactory
		{
			get { return (string)this[AttributeInfrastructureFactory]; }
			set { this[AttributeInfrastructureFactory] = value; }
		}

		[ConfigurationProperty(AttributeIsProduction, IsRequired = false)]
		public bool IsProduction
		{
			get { return (bool)this[AttributeIsProduction]; }
			set { this[AttributeIsProduction] = value; }
		}
	}

	[ConfigurationCollection(typeof(InfrastructureConfigurationSection), AddItemName = "infrastructure", CollectionType = ConfigurationElementCollectionType.BasicMap)]
	public class InfrastructureConfigurationElementCollection : ConfigurationElementCollection
	{
		public InfrastructureConfigurationSection this[int index]
		{
			get { return (InfrastructureConfigurationSection)BaseGet(index); }
			set
			{
				if (BaseGet(index) != null)
				{
					BaseRemoveAt(index);
				}
				
				BaseAdd(index, value);
			}
		}

		public void Add(InfrastructureConfigurationSection serviceConfig)
		{
			BaseAdd(serviceConfig);
		}

		public void Clear()
		{
			BaseClear();
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new InfrastructureConfigurationSection();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			return ((InfrastructureConfigurationSection)element).ConnectionStringName;
		}

		public void Remove(InfrastructureConfigurationSection serviceConfig)
		{
			BaseRemove(serviceConfig.ConnectionStringName);
		}

		public void RemoveAt(int index)
		{
			BaseRemoveAt(index);
		}

		public void Remove(String name)
		{
			BaseRemove(name);
		}
	}
}
