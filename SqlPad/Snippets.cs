using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad
{
	public static class Snippets
	{
		public const string SnippetDirectoryName = "Snippets";
		public const string CodeGenerationItemDirectoryName = "CodeGenerationItems";

		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(Snippet));
		private static readonly List<Snippet> SnippetCollectionInternal = new List<Snippet>();
		private static readonly List<Snippet> CodeGenerationItemCollectionInternal = new List<Snippet>();

		public static void ReloadSnippets()
		{
			ReloadSnippetCollection(ConfigurationProvider.FolderNameSnippets, SnippetCollectionInternal);
			ReloadSnippetCollection(ConfigurationProvider.FolderNameCodeGenerationItems, CodeGenerationItemCollectionInternal);
		}

		private static void ReloadSnippetCollection(string folderName, ICollection<Snippet> snippetCollection)
		{
			snippetCollection.Clear();

			foreach (var snippetFile in Directory.GetFiles(folderName, "*.xml"))
			{
				using (var reader = XmlReader.Create(snippetFile))
				{
					snippetCollection.Add((Snippet)XmlSerializer.Deserialize(reader));
				}
			}
		}

		public static IReadOnlyCollection<Snippet> SnippetCollection
		{
			get
			{
				EnsureSnippetsLoaded();

				return SnippetCollectionInternal.AsReadOnly();
			}
		}

		private static void EnsureSnippetsLoaded()
		{
			lock (SnippetCollectionInternal)
			{
				if (SnippetCollectionInternal.Count == 0 || CodeGenerationItemCollectionInternal.Count == 0)
				{
					ReloadSnippets();
				}
			}
		}

		public static IReadOnlyCollection<Snippet> CodeGenerationItemCollection
		{
			get
			{
				EnsureSnippetsLoaded();

				return CodeGenerationItemCollectionInternal.AsReadOnly();
			}
		}
	}
}