using AssetRipper.Addressables;
using Newtonsoft.Json;
using System.Text.Json.Nodes;

namespace AngryCatalogEditor.GUI
{
	public class BundleInfo
	{
		public class UpdateInfo
		{
			public long Date { get; set; }
			public string Hash { get; set; }
			public string Message { get; set; }
		}

		public class LevelInfo
		{
			public string LevelName { get; set; }
			public string LevelId { get; set; }

			public bool isSecretLevel { get; set; }
			public List<string> requiredCompletedLevelIdsForUnlock;

			public int secretCount { get; set; }

			public bool levelChallengeEnabled { get; set; }
			public string levelChallengeText { get; set; }

			public List<string> requiredDllNames;
		}

		public string Name { get; set; }
		public string Author { get; set; }
		public int Size { get; set; }
		public string Guid { get; set; }
		public string Hash { get; set; }
		public string ThumbnailHash { get; set; }

		public bool Locked { get; set; }
		public List<string> Parts;
		public long LastUpdate { get; set; }
		public List<UpdateInfo> Updates;

		public List<LevelInfo> Levels;
	}

	public class LevelCatalog
	{
		public List<BundleInfo> Levels;
	}

	public class ScriptInfo
	{
		public string FileName { get; set; }
		public string Hash { get; set; }
		public int Size { get; set; }
		public List<string> Updates;
	}

	public class ScriptCatalog
	{
		public List<ScriptInfo> Scripts;
	}



	public static class AngryCatalogHandler
	{
		private static LevelCatalog? _catalog = null;

		private static string? _rootPath = null;
		public static string? rootPath
		{
			get
			{
				if (_rootPath != null)
					return _rootPath;

				string path = Directory.GetCurrentDirectory();
				while (Directory.Exists(path) && !Directory.Exists(Path.Combine(path, ".git")))
				{
					var parent = Directory.GetParent(path);
					if (parent == null)
					{
						_rootPath = null;
						return null;
					}

					path = parent.FullName;
				}

				_rootPath = path;

				return _rootPath;
			}
		}

		private static string? _catalogPath = null;
		public static string? catalogPath
		{
			get
			{
				if (_catalogPath != null)
					return _catalogPath;

				if (rootPath != null)
					_catalogPath = Path.Combine(rootPath, "V2", "LevelCatalog.json");

				return _catalogPath;
			}
		}

		private static string? _catalogHashPath = null;
		public static string? catalogHashPath
		{
			get
			{
				if (_catalogHashPath != null)
					return _catalogHashPath;

				if (rootPath != null)
					_catalogHashPath = Path.Combine(rootPath, "V2", "LevelCatalogHash.txt");

				return _catalogHashPath;
			}
		}

		public static bool TryGetCatalog(out LevelCatalog catalog)
		{
			if (_catalog != null)
			{
				catalog = _catalog;
				return true;
			}

			if (catalogPath == null)
			{
				catalog = null;
				return false;
			}

			_catalog = catalog = JsonConvert.DeserializeObject<LevelCatalog>(File.ReadAllText(catalogPath));
			return true;
		}

		public static void SaveLevelCatalog()
		{
			if (_catalog == null)
				return;

			string catalogSerialized = JsonConvert.SerializeObject(_catalog, Formatting.Indented);
			catalogSerialized = catalogSerialized.Replace("\r", "");

			string hash = CryptologyUtils.GetMD5Hash(catalogSerialized);

			File.WriteAllText(catalogPath, catalogSerialized);
			File.WriteAllText(catalogHashPath, hash);
		}
	}
}
