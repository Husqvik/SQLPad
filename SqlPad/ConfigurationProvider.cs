using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad
{
	public static class ConfigurationProvider
	{
		private const string FolderNameSqlPad = "SQL Pad";
		private const string PostfixErrorLog = "ErrorLog";
		private const string PostfixWorkArea = "WorkArea";
		private const string PostfixMetadataCache = "MetadataCache";
		private const string ConfigurationFileName = "Configuration.xml";

		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(Configuration));
		private static readonly string DefaultUserDataFolderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderNameSqlPad);

		public static string FolderNameUserData = DefaultUserDataFolderName;
		private static string _folderNameErrorLog;

	    public static readonly string FolderNameApplication = Path.GetDirectoryName(typeof(App).Assembly.Location);
		private static readonly string ConfigurationFilePath = Path.Combine(FolderNameApplication, ConfigurationFileName);

	    private static readonly Dictionary<string, ConnectionConfiguration> InternalInfrastructureFactories;
		
		private static readonly FileSystemWatcher ConfigurationWatcher;
		private static DateTime _lastConfigurationFileChange;

		static ConfigurationProvider()
		{
			SetWorkAreaAndErrorLogFolders();

			CreateDirectoryIfNotExists(DefaultUserDataFolderName, FolderNameWorkArea);

			var databaseConfiguration = (DatabaseConnectionConfigurationSection)ConfigurationManager.GetSection(DatabaseConnectionConfigurationSection.SectionName);
			if (databaseConfiguration == null)
			{
				throw new ConfigurationErrorsException("'databaseConfiguration' configuration section is missing. ");
			}

			InternalInfrastructureFactories = databaseConfiguration.Infrastructures
				.Cast<InfrastructureConfigurationSection>()
				.ToDictionary(s => s.ConnectionStringName, s => new ConnectionConfiguration(s));

			Configuration = Configuration.Default;

			Configure();

			ConfigurationWatcher =
				new FileSystemWatcher(FolderNameApplication, ConfigurationFileName)
				{
					EnableRaisingEvents = true,
					NotifyFilter = NotifyFilters.LastWrite
				};

			ConfigurationWatcher.Changed += ConfigurationChangedHandler;
		}

		public static event EventHandler ConfigurationChanged;

		public static void Dispose()
		{
			ConfigurationWatcher.Dispose();
			WorkDocumentCollection.ReleaseConfigurationLock();
		}

		private static void ConfigurationChangedHandler(object sender, FileSystemEventArgs fileSystemEventArgs)
		{
			var writeTime = File.GetLastWriteTimeUtc(fileSystemEventArgs.FullPath);
			if (writeTime == _lastConfigurationFileChange)
			{
				return;
			}

			Thread.Sleep(100);

			_lastConfigurationFileChange = writeTime;

			TraceLog.WriteLine("Configuration file has changed. ");

			if (Configure())
			{
				ConfigurationChanged?.Invoke(Configuration, EventArgs.Empty);
			}
		}

		private static bool Configure()
		{
			try
			{
				if (!File.Exists(ConfigurationFilePath))
				{
					return false;
				}
				
				using (var reader = XmlReader.Create(ConfigurationFilePath))
				{
					Configuration = (Configuration)XmlSerializer.Deserialize(reader);
					Configuration.Validate();
					return true;
				}
			}
			catch (Exception e)
			{
				TraceLog.WriteLine("Configuration loading failed: " + e);
				return false;
			}
		}

		public static ConnectionConfiguration GetConnectionConfiguration(string connectionStringName)
		{
			if (InternalInfrastructureFactories.TryGetValue(connectionStringName, out var connectionConfiguration))
			{
				return connectionConfiguration;
			}

			throw new ArgumentException($"Connection string '{connectionStringName}' doesn't exist. ", nameof(connectionStringName));
		}

		public static Configuration Configuration { get; private set; }

		public static ConnectionStringSettingsCollection ConnectionStrings => ConfigurationManager.ConnectionStrings;

	    public static string FolderNameErrorLog
		{
			get
			{
				CreateDirectoryIfNotExists(_folderNameErrorLog);
				return _folderNameErrorLog;
			}
		}

		public static string FolderNameWorkArea { get; private set; }

	    public static string FolderNameMetadataCache { get; private set; }

	    public static string FolderNameSnippets { get; private set; } = Path.Combine(FolderNameApplication, Snippets.SnippetDirectoryName);

	    public static string FolderNameCodeGenerationItems { get; private set; } = Path.Combine(FolderNameApplication, Snippets.CodeGenerationItemDirectoryName);

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
			
			FolderNameSnippets = directoryName;
		}

		public static void SetCodeGenerationItemFolder(string directoryName)
		{
			CheckDirectoryExists(directoryName);

			FolderNameCodeGenerationItems = directoryName;
		}

		private static void CheckDirectoryExists(string directoryName)
		{
			if (!Directory.Exists(directoryName))
			{
				throw new ArgumentException($"Directory '{directoryName}' does not exist. ");
			}
		}

		private static void SetWorkAreaAndErrorLogFolders()
		{
			_folderNameErrorLog = Path.Combine(FolderNameUserData, PostfixErrorLog);
			FolderNameWorkArea = Path.Combine(FolderNameUserData, PostfixWorkArea);
			FolderNameMetadataCache = Path.Combine(FolderNameUserData, PostfixMetadataCache);
		}

		private static void ConfigureWorkArea()
		{
			CreateDirectoryIfNotExists(FolderNameWorkArea);
			WorkDocumentCollection.Configure();
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
				throw new ConfigurationErrorsException($"Infrastructure factory type '{typeName}' has not been found. ");
			}

			return infrastructureFactoryType;
		}
	}
}
