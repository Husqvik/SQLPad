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
		private static readonly string CacheConfigrationFileName = GetFullFileName("DatabaseModelCacheConfiguration.dat");
		private static readonly BinaryFormatter Serializer = new BinaryFormatter();
		private static readonly DatabaseModelCacheConfiguration DatabaseModelCacheConfiguration;

		static MetadataCache()
		{
			if (!Directory.Exists(ConfigurationProvider.FolderNameMetadataCache))
			{
				Directory.CreateDirectory(ConfigurationProvider.FolderNameMetadataCache);
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
					catch(Exception e)
					{
						TraceLog.WriteLine("DatabaseModelCacheConfiguration deserialization failed: " + e);
					}
				}
			}
		}

		public static string GetFullFileName(string fileName)
		{
			return Path.Combine(ConfigurationProvider.FolderNameMetadataCache, fileName);
		}

		public static bool TryLoadMetadata(string fileName, out Stream stream)
		{
			var fullName = GetFullFileName(fileName);

			stream = null;
			if (!File.Exists(fullName))
			{
				return false;
			}

			stream = File.OpenRead(fullName);
			return true;
		}

		public static void StoreDatabaseModelCache(string cacheKey, Action<Stream> storeAction)
		{
			if (DatabaseModelCacheConfiguration == null)
			{
				return;
			}

			lock (DatabaseModelCacheConfiguration)
			{
				if (!DatabaseModelCacheConfiguration.Files.TryGetValue(cacheKey, out CacheFile cacheFile))
				{
					DatabaseModelCacheConfiguration.Files[cacheKey] =
						cacheFile = new CacheFile { FileName = Thread.CurrentThread.ManagedThreadId + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture) + ".dat" };
				}

				var timer = Stopwatch.StartNew();
				var fileName = GetFullFileName(cacheFile.FileName);
				using (var stream = File.Create(fileName))
				{
					storeAction(stream);
				}

				timer.Stop();

				TraceLog.WriteLine($"Cache for '{cacheKey}' stored to '{fileName}' ({DataSpaceConverter.PrettyPrint(new FileInfo(fileName).Length, CultureInfo.InvariantCulture)}) in {timer.Elapsed}. ");

				using (var stream = File.Create(CacheConfigrationFileName))
				{
					Serializer.Serialize(stream, DatabaseModelCacheConfiguration);
				}
			}
		}

		public static bool TryLoadDatabaseModelCache(string cacheKey, out Stream stream)
		{
			stream = null;
			if (DatabaseModelCacheConfiguration == null ||
				!DatabaseModelCacheConfiguration.Files.TryGetValue(cacheKey, out CacheFile cacheFile))
			{
				return false;
			}

			var fullCacheFileName = GetFullFileName(cacheFile.FileName);
			if (!File.Exists(fullCacheFileName))
			{
				return false;
			}

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

		public IDictionary<string, CacheFile> Files { get; }
	}

	[Serializable]
	public class CacheFile
	{
		public string FileName { get; set; }
	}
}
