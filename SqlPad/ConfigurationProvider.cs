using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace SqlPad
{
	public static class ConfigurationProvider
	{
		private const string FolderNameSqlPad = "SQL Pad";
		private const string PostfixErrorLog = "ErrorLog";
		private const string PostfixWorkArea = "WorkArea";
		private const string PostfixMetadataCache = "MetadataCache";

		private static readonly string DefaultUserDataFolderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderNameSqlPad);

		public static string FolderNameUserData = DefaultUserDataFolderName;
		private static string _folderNameErrorLog;
		private static string _folderNameWorkArea;
		private static string _folderNameMetadataCache;

		public static readonly string FolderNameApplication = Path.GetDirectoryName(typeof(App).Assembly.Location);

		private static string _folderNameSnippets = Path.Combine(FolderNameApplication, Snippets.SnippetDirectoryName);

		private static readonly Dictionary<string, ConnectionConfiguration> InternalInfrastructureFactories;

		static ConfigurationProvider()
		{
			SetWorkAreaAndErrorLogFolders();

			CreateDirectoryIfNotExists(DefaultUserDataFolderName, _folderNameWorkArea);

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

		public static string FolderNameErrorLog
		{
			get
			{
				CreateDirectoryIfNotExists(_folderNameErrorLog);
				return _folderNameErrorLog;
			}
		}

		public static string FolderNameWorkArea { get { return _folderNameWorkArea; } }
		
		public static string FolderNameMetadataCache { get { return _folderNameMetadataCache; } }
		
		public static string FolderNameSnippets { get { return _folderNameSnippets; } }

		public static void SetUserDataFolder(string directoryName)
		{
			CheckDirectoryExists(directoryName);

			FolderNameUserData = directoryName;

			SetWorkAreaAndErrorLogFolders();

			ConfigureWorkArea();
		}

		public static void SetSnippetsFolder(string directoryName)
		{
			CheckDirectoryExists(directoryName);
			
			_folderNameSnippets = directoryName;
		}

		private static void CheckDirectoryExists(string directoryName)
		{
			if (!Directory.Exists(directoryName))
			{
				throw new ArgumentException(String.Format("Directory '{0}' does not exist. ", directoryName));
			}
		}

		private static void SetWorkAreaAndErrorLogFolders()
		{
			_folderNameErrorLog = Path.Combine(FolderNameUserData, PostfixErrorLog);
			_folderNameWorkArea = Path.Combine(FolderNameUserData, PostfixWorkArea);
			_folderNameMetadataCache = Path.Combine(FolderNameUserData, PostfixMetadataCache);
		}

		private static void ConfigureWorkArea()
		{
			CreateDirectoryIfNotExists(_folderNameWorkArea);
			WorkingDocumentCollection.Configure();
		}

		private static void CreateDirectoryIfNotExists(params string[] directoryNames)
		{
			foreach (var directoryName in directoryNames)
			{
				if (!Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
			}
		}
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
