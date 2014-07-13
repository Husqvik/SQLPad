using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace SqlPad
{
	public static class MetadataCache
	{
		public static readonly string CacheDirectory = Path.Combine(App.FolderNameUserData, "MetadataCache");

		private static readonly string CacheConfigrationFileName = GetFullFileName("DatabaseModelCacheConfiguration.dat");
		private static readonly BinaryFormatter Serializer = new BinaryFormatter();
		private static readonly DatabaseModelCacheConfiguration DatabaseModelCacheConfiguration;

		static MetadataCache()
		{
			if (!Directory.Exists(CacheDirectory))
			{
				Directory.CreateDirectory(CacheDirectory);
			}

			if (!File.Exists(CacheConfigrationFileName))
			{
				DatabaseModelCacheConfiguration = new DatabaseModelCacheConfiguration();
			}
			else
			{
				try
				{
					using (var stream = File.OpenRead(CacheConfigrationFileName))
					{
						DatabaseModelCacheConfiguration = (DatabaseModelCacheConfiguration)Serializer.Deserialize(stream);
					}
				}
				catch
				{
					try
					{
						File.Delete(CacheConfigrationFileName);
						DatabaseModelCacheConfiguration = new DatabaseModelCacheConfiguration();
					}
					catch { }
				}
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

		public static void StoreDatabaseModelCache(string cacheKey, Action<Stream> storeAction)
		{
			if (DatabaseModelCacheConfiguration == null)
				return;

			CacheFile cacheFile;
			if (!DatabaseModelCacheConfiguration.Files.TryGetValue(cacheKey, out cacheFile))
			{
				DatabaseModelCacheConfiguration.Files[cacheKey] =
					cacheFile = new CacheFile { FileName = Thread.CurrentThread.ManagedThreadId + DateTime.Now.Ticks.ToString(CultureInfo.CurrentUICulture) + ".dat" };
			}

			var timer = Stopwatch.StartNew();
			
			using (var stream = File.Create(GetFullFileName(cacheFile.FileName)))
			{
				storeAction(stream);
			}

			timer.Stop();

			Trace.WriteLine(String.Format("{0} - Cache for '{1}' stored in {2}", DateTime.Now, cacheKey, timer.Elapsed));
			
			lock (DatabaseModelCacheConfiguration)
			{
				using (var stream = File.Create(CacheConfigrationFileName))
				{
					Serializer.Serialize(stream, DatabaseModelCacheConfiguration);
				}
			}
		}

		public static bool TryLoadDatabaseModelCache(string cacheKey, out Stream stream)
		{
			stream = null;
			CacheFile cacheFile;
			if (DatabaseModelCacheConfiguration == null ||
			    !DatabaseModelCacheConfiguration.Files.TryGetValue(cacheKey, out cacheFile))
			{
				return false;
			}

			var fullCacheFileName = GetFullFileName(cacheFile.FileName);
			if (!File.Exists(fullCacheFileName))
				return false;

			stream = File.OpenRead(fullCacheFileName);
			return true;
		}
	}

	[Serializable]
	public class DatabaseModelCacheConfiguration
	{
		public DatabaseModelCacheConfiguration()
		{
			Files = new Dictionary<string, CacheFile>();
		}

		public IDictionary<string, CacheFile> Files { get; private set; }
	}

	[Serializable]
	public class CacheFile
	{
		public string FileName { get; set; }
	}
}
