using Newtonsoft.Json;

namespace AngryCatalogEditor.GUI.IO
{
	public static class AngryCatalogHandler
	{
		private static LevelCatalog? _catalog = null;
		private static string? _catalogPath = null;
		public static string? catalogPath
		{
			get
			{
				if (_catalogPath != null)
					return _catalogPath;

				if (ProjectPaths.rootPath != null)
					_catalogPath = Path.Combine(ProjectPaths.rootPath, "V2", "LevelCatalog.json");

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

				if (ProjectPaths.rootPath != null)
					_catalogHashPath = Path.Combine(ProjectPaths.rootPath, "V2", "LevelCatalogHash.txt");

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



		private static ScriptCatalog? _scriptCatalog = null;
		private static string? _scriptCatalogPath = null;
		public static string? scriptCatalogPath
		{
			get
			{
				if (_scriptCatalogPath != null)
					return _scriptCatalogPath;

				if (ProjectPaths.rootPath != null)
					_scriptCatalogPath = Path.Combine(ProjectPaths.rootPath, "ScriptCatalog.json");

				return _scriptCatalogPath;
			}
		}

		private static string? _scriptCatalogHashPath = null;
		public static string? scriptCatalogHashPath
		{
			get
			{
				if (_scriptCatalogHashPath != null)
					return _scriptCatalogHashPath;

				if (ProjectPaths.rootPath != null)
					_scriptCatalogHashPath = Path.Combine(ProjectPaths.rootPath, "ScriptCatalogHash.txt");

				return _scriptCatalogHashPath;
			}
		}

		public static bool TryGetScriptCatalog(out ScriptCatalog catalog)
		{
			if (_scriptCatalog != null)
			{
				catalog = _scriptCatalog;
				return true;
			}

			if (scriptCatalogPath == null)
			{
				catalog = null;
				return false;
			}

			_scriptCatalog = catalog = JsonConvert.DeserializeObject<ScriptCatalog>(File.ReadAllText(scriptCatalogPath));
			return true;
		}

		public static void SaveScriptCatalog()
		{
			if (_scriptCatalog == null)
				return;

			string catalogSerialized = JsonConvert.SerializeObject(_scriptCatalog, Formatting.Indented);
			catalogSerialized = catalogSerialized.Replace("\r", "");

			string hash = CryptologyUtils.GetMD5Hash(catalogSerialized);

			File.WriteAllText(scriptCatalogPath, catalogSerialized);
			File.WriteAllText(scriptCatalogHashPath, hash);
		}
	}
}
