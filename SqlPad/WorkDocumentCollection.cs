using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ProtoBuf.Meta;

namespace SqlPad
{
	public class WorkDocumentCollection
	{
		internal const string ConfigurationFileName = "WorkDocuments.dat";
		private const string ConfigurationBackupFileName = "WorkDocuments.backup.dat";
		private const string LockFileName = "Configuration.lock";
		private const int MaxRecentDocumentCount = 16;
		private static string _fileName = GetWorkingDocumentConfigurationFileName(ConfigurationProvider.FolderNameWorkArea);
		private static string _backupFileName;
		private static bool _allowBackupOverwrite = true;
		private static FileStream _lockFile;
		private static readonly RuntimeTypeModel Serializer;
		private static WorkDocumentCollection _instance;

		private Dictionary<string, DatabaseProviderConfiguration> _databaseProviderConfigurations = new Dictionary<string, DatabaseProviderConfiguration>();
		private Dictionary<Guid, WorkDocument> _workingDocuments = new Dictionary<Guid, WorkDocument>();
		private int _activeDocumentIndex;
		private WindowProperties _windowProperties;
		private List<WorkDocument> _recentFiles = new List<WorkDocument>();

		static WorkDocumentCollection()
		{
			ConfigureBackupFile();

			Serializer = TypeModel.Create();
			var workingDocumentCollectionType = Serializer.Add(typeof(WorkDocumentCollection), false);
			workingDocumentCollectionType.UseConstructor = false;
			workingDocumentCollectionType.Add("_workingDocuments", "_activeDocumentIndex", "_windowProperties", "_databaseProviderConfigurations", "_recentFiles");

			var workingDocumentType = Serializer.Add(typeof(WorkDocument), false);
			workingDocumentType.UseConstructor = false;
			workingDocumentType.Add("DocumentFileName", "DocumentId", "ConnectionName", "SchemaName", "CursorPosition", "SelectionStart", "SelectionLength", "IsModified", "VisualLeft", "VisualTop", "EditorGridRowHeight", "Text", "EditorGridColumnWidth", "TabIndex", "_foldingStates", "EnableDatabaseOutput", "KeepDatabaseOutputHistory", "HeaderBackgroundColorCode", "DocumentTitle", "_fontSize");

			var windowPropertiesType = Serializer.Add(typeof(WindowProperties), false);
			windowPropertiesType.UseConstructor = false;
			windowPropertiesType.Add("Left", "Top", "Width", "Height", "State");

			var sqlPadConfigurationType = Serializer.Add(typeof(DatabaseProviderConfiguration), false);
			sqlPadConfigurationType.UseConstructor = false;
			sqlPadConfigurationType.Add("_bindVariables", "ProviderName", "_statementExecutionHistory");

			var bindVariableConfigurationType = Serializer.Add(typeof(BindVariableConfiguration), false);
			bindVariableConfigurationType.Add("Name", "DataType", "_internalValue");

			var statementExecutionType = Serializer.Add(typeof(StatementExecutionHistoryEntry), false);
			statementExecutionType.Add("StatementText", "ExecutedAt");
		}

		private WorkDocumentCollection() {}

		private static void Initialize()
		{
			ReleaseConfigurationLock();

			try
			{
				_lockFile = new FileStream(Path.Combine(ConfigurationProvider.FolderNameWorkArea, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			}
			catch (Exception e)
			{
				Trace.WriteLine("Configuration lock aquire failed: " + e);
			}

			if (!ReadConfiguration(_fileName))
			{
				_allowBackupOverwrite = ReadConfiguration(_backupFileName);
			}

			if (_instance == null)
			{
				_instance = new WorkDocumentCollection();
			}
			else
			{
				if (_instance._workingDocuments == null)
				{
					_instance._workingDocuments = new Dictionary<Guid, WorkDocument>();
				}

				if (_instance._recentFiles == null)
				{
					_instance._recentFiles = new List<WorkDocument>();
				}
			}

			if (_instance._databaseProviderConfigurations == null)
			{
				_instance._databaseProviderConfigurations = new Dictionary<string, DatabaseProviderConfiguration>();
			}
		}

		private static bool ReadConfiguration(string fileName)
		{
			if (!File.Exists(fileName))
			{
				return true;
			}
			
			using (var file = File.OpenRead(fileName))
			{
				try
				{
					_instance = (WorkDocumentCollection)Serializer.Deserialize(file, _instance, typeof(WorkDocumentCollection));
					Trace.WriteLine(String.Format("WorkDocumentCollection ({0} document(s)) successfully loaded from '{1}'. ", _instance._workingDocuments.Count, fileName));
					return true;
				}
				catch (Exception e)
				{
					Trace.WriteLine(String.Format("WorkDocumentCollection deserialization from '{0}' failed: {1}", fileName, e));
				}
			}

			return false;
		}

		public static void Configure()
		{
			_instance = null;
			_fileName = GetWorkingDocumentConfigurationFileName(ConfigurationProvider.FolderNameWorkArea);
			ConfigureBackupFile();
		}

		private static void ConfigureBackupFile()
		{
			_backupFileName = Path.Combine(ConfigurationProvider.FolderNameWorkArea, ConfigurationBackupFileName);
		}

		public static void ReleaseConfigurationLock()
		{
			if (_lockFile == null)
			{
				return;
			}

			_lockFile.Dispose();
			_lockFile = null;
		}

		public static DatabaseProviderConfiguration GetProviderConfiguration(string providerName)
		{
			DatabaseProviderConfiguration configuration;
			if (!Instance._databaseProviderConfigurations.TryGetValue(providerName, out configuration))
			{
				configuration = new DatabaseProviderConfiguration(providerName);
				Instance._databaseProviderConfigurations.Add(providerName, configuration);
			}

			return configuration;
		}

		private static string GetWorkingDocumentConfigurationFileName(string directory)
		{
			if (!Directory.Exists(directory))
			{
				throw new ArgumentException(String.Format("Directory '{0}' does not exist. ", directory), "directory");
			}

			return Path.Combine(directory, ConfigurationFileName);
		}

		private static WorkDocumentCollection Instance
		{
			get
			{
				lock (Serializer)
				{
					if (_instance == null)
					{
						Initialize();
					}

					return _instance;
				}
			}
		}

		public static int ActiveDocumentIndex
		{
			get { return Instance._activeDocumentIndex; }
			set { Instance._activeDocumentIndex = value; }
		}

		public static IReadOnlyList<WorkDocument> WorkingDocuments { get { return Instance._workingDocuments.Values.ToArray(); } }
		
		public static IReadOnlyList<WorkDocument> RecentDocuments { get { return Instance._recentFiles.AsReadOnly(); } }

		public static bool TryGetWorkingDocumentFor(string fileName, out WorkDocument workDocument)
		{
			if (fileName == null)
				throw new ArgumentNullException("fileName");

			workDocument = Instance._workingDocuments.Values.FirstOrDefault(d => d.File != null && d.File.FullName.ToLowerInvariant() == fileName.ToLowerInvariant());
			return workDocument != null;
		}

		public static void AddDocument(WorkDocument workDocument)
		{
			ValidateWorkingDocument(workDocument);

			Instance._workingDocuments[workDocument.DocumentId] = workDocument;

			AddRecentDocument(workDocument);
		}

		public static void RemoveRecentDocument(WorkDocument workDocument)
		{
			var index = FindRecentFileIndex(workDocument);
			if (index != -1)
			{
				Instance._recentFiles.RemoveAt(index);
			}
		}

		private static int FindRecentFileIndex(WorkDocument workDocument)
		{
			return Instance._recentFiles.FindIndex(f => String.Equals(f.DocumentFileName, workDocument.DocumentFileName, StringComparison.InvariantCultureIgnoreCase));
		}

		public static void AddRecentDocument(WorkDocument workDocument)
		{
			if (workDocument.File == null || !workDocument.File.Exists)
			{
				return;
			}

			lock (Instance._recentFiles)
			{
				var existingIndex = FindRecentFileIndex(workDocument);
				if (existingIndex != -1)
				{
					Instance._recentFiles.RemoveAt(existingIndex);
				}

				Instance._recentFiles.Insert(0, workDocument.CloneAsRecent());

				if (Instance._recentFiles.Count > MaxRecentDocumentCount)
				{
					Instance._recentFiles.RemoveRange(MaxRecentDocumentCount, Instance._recentFiles.Count - MaxRecentDocumentCount);
				}
			}
		}

		public static void CloseDocument(WorkDocument workDocument)
		{
			ValidateWorkingDocument(workDocument);

			AddRecentDocument(workDocument);

			try
			{
				Instance._workingDocuments.Remove(workDocument.DocumentId);

				Save();
			}
			catch (Exception e)
			{
				Trace.WriteLine("Close working document failed: " + e);
			}
		}

		public static void CloseAllDocuments()
		{
			foreach (var document in WorkingDocuments)
			{
				CloseDocument(document);
			}
		}

		private static void ValidateWorkingDocument(WorkDocument workDocument)
		{
			if (workDocument == null)
			{
				throw new ArgumentNullException("workDocument");
			}
		}

		public static void Save()
		{
			if (_lockFile == null)
			{
				Trace.WriteLine("Lock file has not been ackquired. Work document collection cannot be saved. ");
				return;
			}

			lock (Instance)
			{
				_allowBackupOverwrite &= File.Exists(_fileName);
				if (_allowBackupOverwrite)
				{
					File.Copy(_fileName, _backupFileName, true);
				}

				using (var file = File.Create(_fileName))
				{
					Serializer.Serialize(file, Instance);
				}

				if (_allowBackupOverwrite)
				{
					File.Delete(_backupFileName);
				}
			}
		}

		public static void SaveDocumentAsFile(WorkDocument workDocument)
		{
			using (var file = File.Create(workDocument.File.FullName))
			{
				Serializer.Serialize(file, workDocument);
			}
		}

		public static WorkDocument LoadDocumentFromFile(string fileName)
		{
			using (var file = File.OpenRead(fileName))
			{
				return (WorkDocument)Serializer.Deserialize(file, null, typeof(WorkDocument));
			}
		}

		public static void SetApplicationWindowProperties(Window window)
		{
			Instance._windowProperties = new WindowProperties(window);
		}

		public static void RestoreApplicationWindowProperties(Window window)
		{
			var properties = Instance._windowProperties;
			if (properties == null)
				return;

			window.Left = properties.Left;
			window.Top = properties.Top;
			window.Width = properties.Width;
			window.Height = properties.Height;
			window.WindowState = properties.State;
		}
	}

	internal class WindowProperties
	{
		public WindowProperties(Window window)
		{
			Left = window.Left;
			Top = window.Top;
			Width = window.Width;
			Height = window.Height;
			State = window.WindowState;
		}

		public double Left { get; private set; }
		public double Top { get; private set; }
		public double Width { get; private set; }
		public double Height { get; private set; }
		public WindowState State { get; private set; }
	}
}
