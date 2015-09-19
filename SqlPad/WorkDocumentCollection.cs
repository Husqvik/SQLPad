using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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
			workingDocumentType.Add(nameof(WorkDocument.DocumentFileName), nameof(WorkDocument.DocumentId), nameof(WorkDocument.ConnectionName), nameof(WorkDocument.SchemaName), nameof(WorkDocument.CursorPosition),
				nameof(WorkDocument.SelectionStart), nameof(WorkDocument.SelectionLength), nameof(WorkDocument.IsModified), nameof(WorkDocument.VisualLeft), nameof(WorkDocument.VisualTop), nameof(WorkDocument.EditorGridRowHeight),
				nameof(WorkDocument.Text), nameof(WorkDocument.EditorGridColumnWidth), nameof(WorkDocument.TabIndex), "_foldingStates", nameof(WorkDocument.EnableDatabaseOutput), nameof(WorkDocument.KeepDatabaseOutputHistory),
				nameof(WorkDocument.HeaderBackgroundColorCode), nameof(WorkDocument.DocumentTitle), "_fontSize", nameof(WorkDocument.WatchItems), nameof(WorkDocument.DebuggerViewDefaultTabIndex), "_breakpoints");

			var windowPropertiesType = Serializer.Add(typeof(WindowProperties), false);
			windowPropertiesType.UseConstructor = false;
			windowPropertiesType.Add(nameof(WindowProperties.Left), nameof(WindowProperties.Top), nameof(WindowProperties.Width), nameof(WindowProperties.Height), nameof(WindowProperties.State));

			var sqlPadConfigurationType = Serializer.Add(typeof(DatabaseProviderConfiguration), false);
			sqlPadConfigurationType.UseConstructor = false;
			sqlPadConfigurationType.Add("_bindVariables", nameof(DatabaseProviderConfiguration.ProviderName), "_statementExecutionHistory");

			var bindVariableConfigurationType = Serializer.Add(typeof(BindVariableConfiguration), false);
			bindVariableConfigurationType.Add(nameof(BindVariableConfiguration.Name), nameof(BindVariableConfiguration.DataType), "_internalValue", nameof(BindVariableConfiguration.IsFilePath));

			var statementExecutionType = Serializer.Add(typeof(StatementExecutionHistoryEntry), false);
			statementExecutionType.UseConstructor = false;
			statementExecutionType.Add(nameof(StatementExecutionHistoryEntry.StatementText), nameof(StatementExecutionHistoryEntry.ExecutedAt), nameof(StatementExecutionHistoryEntry.Tags));

			var breakpointDataType = Serializer.Add(typeof(BreakpointData), false);
			breakpointDataType.UseConstructor = false;
			breakpointDataType.Add("_programIdentifier", nameof(BreakpointData.LineNumber), nameof(BreakpointData.IsEnabled));
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
					Trace.WriteLine($"WorkDocumentCollection ({_instance._workingDocuments.Count} document(s)) successfully loaded from '{fileName}'. ");
					return true;
				}
				catch (Exception e)
				{
					Trace.WriteLine($"WorkDocumentCollection deserialization from '{fileName}' failed: {e}");
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
				throw new ArgumentException($"Directory '{directory}' does not exist. ", nameof(directory));
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

		public static IReadOnlyList<WorkDocument> WorkingDocuments => Instance._workingDocuments.Values.ToArray();

	    public static IReadOnlyList<WorkDocument> RecentDocuments => Instance._recentFiles.AsReadOnly();

	    public static bool TryGetWorkingDocumentFor(string fileName, out WorkDocument workDocument)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			workDocument = Instance._workingDocuments.Values.FirstOrDefault(d => d.File != null && String.Equals(d.File.FullName.ToUpperInvariant(), fileName.ToUpperInvariant()));
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
				throw new ArgumentNullException(nameof(workDocument));
			}
		}

		public static void Save()
		{
			if (_lockFile == null)
			{
				Trace.WriteLine("Lock file has not been acquired. Work document collection cannot be saved. ");
				return;
			}

			lock (Instance)
			{
				CleanEmptyBindVariables();

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

		private static void CleanEmptyBindVariables()
		{
			foreach (var configuration in Instance._databaseProviderConfigurations.Values)
			{
				foreach (var variable in configuration.BindVariables.ToArray())
				{
					if (variable.Value == null || Equals(variable.Value, String.Empty) || Equals(variable.Value, DateTime.MinValue))
					{
						configuration.RemoveBindVariable(variable.Name);
					}
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

	[DebuggerDisplay("BreakpointData (ObjectIdentifier={ProgramIdentifier}, Offset={LineNumber}, IsEnabled={IsEnabled})")]
	public class BreakpointData
	{
		private static readonly BinaryFormatter Formatter = new BinaryFormatter();

		private readonly byte[] _programIdentifier;

		private object _programIdentifierValue;

		public object ProgramIdentifier
		{
			get
			{
				if (_programIdentifierValue != null)
				{
					return _programIdentifierValue;
				}

				using (var stream = new MemoryStream(_programIdentifier))
				{
					return _programIdentifierValue = Formatter.Deserialize(stream);
				}
			}
		}

		public int LineNumber { get; private set; }

		public bool IsEnabled { get; private set; }

		public BreakpointData(object programIdentifier, int lineNumber, bool isEnabled)
		{
			using (var stream = new MemoryStream())
			{
				Formatter.Serialize(stream, programIdentifier);
				stream.Seek(0, SeekOrigin.Begin);
				_programIdentifier = stream.ToArray();
			}

			_programIdentifierValue = programIdentifier;

			LineNumber = lineNumber;
			IsEnabled = isEnabled;
		}

		protected bool Equals(BreakpointData other)
		{
			return Equals(_programIdentifierValue, other._programIdentifierValue) && LineNumber == other.LineNumber;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((BreakpointData)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (_programIdentifierValue.GetHashCode() * 397) ^ LineNumber;
			}
		}
	}
}
