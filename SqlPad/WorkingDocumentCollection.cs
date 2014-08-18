using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ProtoBuf.Meta;

namespace SqlPad
{
	public class WorkingDocumentCollection
	{
		internal const string ConfigurationFileName = "WorkingDocuments.dat";
		private static string _fileName = GetWorkingDocumentConfigurationFileName(ConfigurationProvider.FolderNameWorkArea);
		private static readonly RuntimeTypeModel Serializer;
		private static WorkingDocumentCollection _instance;

		private Dictionary<Guid, WorkingDocument> _workingDocuments = new Dictionary<Guid, WorkingDocument>();
		private int _activeDocumentIndex;
		private WindowProperties _windowProperties;

		static WorkingDocumentCollection()
		{
			Serializer = TypeModel.Create();
			var workingDocumentCollectionType = Serializer.Add(typeof(WorkingDocumentCollection), false);
			workingDocumentCollectionType.UseConstructor = false;
			workingDocumentCollectionType.Add("_workingDocuments", "_activeDocumentIndex", "_windowProperties");

			var workingDocumentType = Serializer.Add(typeof(WorkingDocument), false);
			workingDocumentType.UseConstructor = false;
			workingDocumentType.Add("DocumentFileName", "DocumentId", "ConnectionName", "SchemaName", "CursorPosition", "SelectionStart", "SelectionLength", "IsModified", "VisualLeft", "VisualTop", "EditorGridRowHeight", "Text", "EditorGridColumnWidth", "TabIndex");

			var windowPropertiesType = Serializer.Add(typeof(WindowProperties), false);
			windowPropertiesType.UseConstructor = false;
			windowPropertiesType.Add("Left", "Top", "Width", "Height", "State");
		}

		private WorkingDocumentCollection() {}

		private static void Initialize()
		{
			if (File.Exists(_fileName))
			{
				using (var file = File.OpenRead(_fileName))
				{
					try
					{
						_instance = (WorkingDocumentCollection)Serializer.Deserialize(file, _instance, typeof(WorkingDocumentCollection));
					}
					catch (Exception e)
					{
						Trace.WriteLine("WorkingDocumentCollection deserialization failed: " + e);
					}
				}
			}

			if (_instance == null)
			{
				_instance = new WorkingDocumentCollection();
			}
			else if (_instance._workingDocuments == null)
			{
				_instance._workingDocuments = new Dictionary<Guid, WorkingDocument>();
			}
		}

		public static void Configure()
		{
			_instance = null;
			_fileName = GetWorkingDocumentConfigurationFileName(ConfigurationProvider.FolderNameWorkArea);
		}

		private static string GetWorkingDocumentConfigurationFileName(string directory)
		{
			if (!Directory.Exists(directory))
			{
				throw new ArgumentException(String.Format("Directory '{0}' does not exist. ", directory), "directory");
			}

			return Path.Combine(directory, ConfigurationFileName);
		}

		private static WorkingDocumentCollection Instance
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

		public static ICollection<WorkingDocument> WorkingDocuments { get { return Instance._workingDocuments.Values.ToArray(); } }

		public static bool TryGetWorkingDocumentFor(string fileName, out WorkingDocument workingDocument)
		{
			if (fileName == null)
				throw new ArgumentNullException("fileName");

			workingDocument = Instance._workingDocuments.Values.FirstOrDefault(d => d.File != null && d.File.FullName.ToLowerInvariant() == fileName.ToLowerInvariant());
			return workingDocument != null;
		}

		public static void AddDocument(WorkingDocument workingDocument)
		{
			ValidateWorkingDocument(workingDocument);

			Instance._workingDocuments[workingDocument.DocumentId] = workingDocument;
		}

		public static void CloseDocument(WorkingDocument workingDocument)
		{
			ValidateWorkingDocument(workingDocument);

			try
			{
				Instance._workingDocuments.Remove(workingDocument.DocumentId);

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

		private static void ValidateWorkingDocument(WorkingDocument workingDocument)
		{
			if (workingDocument == null)
			{
				throw new ArgumentNullException("workingDocument");
			}
		}

		public static void Save()
		{
			lock (Instance)
			{
				using (var file = File.Create(_fileName))
				{
					Serializer.Serialize(file, Instance);
				}
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

	public class WorkingDocument
	{
		public WorkingDocument()
		{
			DocumentId = Guid.NewGuid();
		}

		public Guid DocumentId { get; private set; }

		public FileInfo File { get { return String.IsNullOrEmpty(DocumentFileName) ? null : new FileInfo(DocumentFileName); } }
		
		public string DocumentFileName { get; set; }
		
		public string ConnectionName { get; set; }
		
		public string SchemaName { get; set; }

		public string Text { get; set; }
		
		public int CursorPosition { get; set; }
		
		public int SelectionStart { get; set; }
		
		public int SelectionLength { get; set; }

		public int TabIndex { get; set; }
		
		public bool IsModified { get; set; }

		public double VisualTop { get; set; }
		
		public double VisualLeft { get; set; }

		public double EditorGridRowHeight { get; set; }
		
		public double EditorGridColumnWidth { get; set; }
	}
}
