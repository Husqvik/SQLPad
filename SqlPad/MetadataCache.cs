using System.IO;

namespace SqlPad
{
	public static class MetadataCache
	{
		public static readonly string CacheDirectory = Path.Combine(App.FolderNameCommonData, "MetadataCache");

		static MetadataCache()
		{
			if (!Directory.Exists(CacheDirectory))
			{
				Directory.CreateDirectory(CacheDirectory);
			}
		}

		public static string GetFullFileName(string fileName)
		{
			return Path.Combine(CacheDirectory, fileName);
		}

		public static bool TryLoadMetadata(string fileName, out string metadata)
		{
			var fullName = GetFullFileName(fileName);

			metadata = null;
			if (!File.Exists(fullName))
				return false;

			metadata = File.ReadAllText(fullName);
			return true;
		}
	}
}