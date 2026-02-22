namespace AngryCatalogEditor.GUI.IO
{
	public static class ProjectPaths
	{

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
						// Fallback
						_rootPath = Path.Combine(Directory.GetCurrentDirectory(), "AngryLevels");
						if (Directory.Exists(_rootPath) && Directory.Exists(Path.Combine(_rootPath, ".git")))
							return _rootPath;

						_rootPath = null;
						return null;
					}

					path = parent.FullName;
				}


				_rootPath = path;

				return _rootPath;
			}
		}
	}
}
