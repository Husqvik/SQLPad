using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace SqlPad
{
	public interface ICodeSnippetProvider
	{
		ICollection<ICodeSnippet> GetSnippets(string statementText, int cursorPosition);
	}

	public interface ICodeSnippet
	{
		string Name { get; }

		string BaseText { get; }
	}

	public static class Snippets
	{
		private static readonly XmlSerializer XmlSerializer = new XmlSerializer(typeof(Snippet));
		private static readonly string SnippetDirectory = Path.Combine(new Uri(Path.GetDirectoryName(Assembly.GetAssembly(typeof(Snippets)).CodeBase)).LocalPath, "Snippets");
		private static readonly List<Snippet> SnippetCollectionInternal = new List<Snippet>(); 

		public static void ReloadSnippets()
		{
			SnippetCollectionInternal.Clear();

			foreach (var snippetFile in Directory.GetFiles(SnippetDirectory, "*.xml"))
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
