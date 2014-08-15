using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ProtoBuf.Meta;

namespace SqlPad
{
	public class WorkingDocumentCollection
	{
		internal const string ConfigurationFileName = "WorkingDocuments.dat";
		private static string _fileName = GetWorkingDocumentConfigurationFileName(App.FolderNameWorkArea);
		private static readonly RuntimeTypeModel Serializer;
		private static WorkingDocumentCollection _instance;

		private Dictionary<string, WorkingDocument> _workingDocuments = new Dictionary<string, WorkingDocument>();
		private int _activeDocumentIndex;

		static WorkingDocumentCollection()
		{
			Serializer = TypeModel.Create();
			var workingDocumentCollectionType = Serializer.Add(typeof(WorkingDocumentCollection), false);
			workingDocumentCollectionType.UseConstructor = false;
			workingDocumentCollectionType.Add("_workingDocuments", "_activeDocumentIndex");

			var workingDocumentType = Serializer.Add(typeof(WorkingDocument), false);
			workingDocumentType.UseConstructor = false;
			workingDocumentType.Add("DocumentFileName", "_workingFileName", "ConnectionName", "SchemaName", "CursorPosition", "SelectionStart", "SelectionLength", "IsModified");
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
						Trace.WriteLine("WorkingDocumentCollection deserialization failed: " + e.Message);
					}
				}
			}

			if (_instance == null)
			{
				_instance = new WorkingDocumentCollection();
			}
			else if (_instance._workingDocuments == null)
			{
				_instance._workingDocuments = new Dictionary<string, WorkingDocument>();
			}
		}

		public static void SetWorkingDocumentDirectory(string directory)
		{
			_instance = null;
			_fileName = GetWorkingDocumentConfigurationFileName(directory);
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

			Instance._workingDocuments[workingDocument.WorkingFile.FullName] = workingDocument;
		}

		public static void CloseDocument(WorkingDocument workingDocument)
		{
			ValidateWorkingDocument(workingDocument);

			try
			{
				if (File.Exists(workingDocument.WorkingFile.FullName))
				{
					File.Delete(workingDocument.WorkingFile.FullName);
				}
				
				Instance._workingDocuments.Remove(workingDocument.WorkingFile.FullName);

				Save();
			}
			catch (Exception e)
			{
				Trace.WriteLine("Close working document failed: " + e.Message);
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

			if (workingDocument.WorkingFile == null)
			{
				throw new ArgumentException("WorkingFile property is mandatory. ", "workingDocument");
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
	}

	public class WorkingDocument
	{
		private readonly string _workingFileName;

		public WorkingDocument()
		{
			_workingFileName = Path.Combine(App.FolderNameWorkArea, String.Format("WorkingDocument_{0}.working", Guid.NewGuid().ToString("N")));
		}

		public static string GetWorkingFileName(string documentFileName)
		{
			if (!System.IO.File.Exists(documentFileName))
			{
				throw new ArgumentException(String.Format("File '{0}' does not exist. ", documentFileName));
			}

			var fileInfo = new FileInfo(documentFileName);
			return Path.Combine(App.FolderNameWorkArea, fileInfo.Name + ".working");
		}

		public FileInfo File { get { return String.IsNullOrEmpty(DocumentFileName) ? null : new FileInfo(DocumentFileName); } }
		
		public FileInfo WorkingFile { get { return new FileInfo(_workingFileName); } }

		public string DocumentFileName { get; set; }
		
		public string ConnectionName { get; set; }
		
		public string SchemaName { get; set; }
		
		public int CursorPosition { get; set; }
		
		public int SelectionStart { get; set; }
		
		public int SelectionLength { get; set; }
		
		public bool IsModified { get; set; }
	}
}
