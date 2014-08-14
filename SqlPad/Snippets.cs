using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad
{
	public static class Snippets
	{
		public const string SnippetDirectoryName = "Snippets";

		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(Snippet));
		private static string _snippetDirectory = Path.Combine(App.FolderNameApplication, SnippetDirectoryName);
		private static readonly List<Snippet> SnippetCollectionInternal = new List<Snippet>();

		public static void SetSnippetDirectory(string directory)
		{
			_snippetDirectory = directory;
		}

		public static void ReloadSnippets()
		{
			SnippetCollectionInternal.Clear();

			foreach (var snippetFile in Directory.GetFiles(_snippetDirectory, "*.xml"))
			{
				using (var reader = XmlReader.Create(snippetFile))
				{
					SnippetCollectionInternal.Add((Snippet)XmlSerializer.Deserialize(reader));
				}
			}
		}

		public static ICollection<Snippet> SnippetCollection
		{
			get
			{
				if (SnippetCollectionInternal.Count == 0)
				{
					ReloadSnippets();
				}

				return SnippetCollectionInternal.AsReadOnly();
			}
		}
	}
}