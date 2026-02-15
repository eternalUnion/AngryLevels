namespace AngryCatalogEditor.GUI.IO
{
	public class TempFile : IDisposable
	{
		private string _tempFilePath;
		public string tempFilePath { get => _tempFilePath; }

		public TempFile(string originalPath, string tempFileName)
		{
			string appDir = Directory.GetCurrentDirectory();
			string tempDir = Path.Combine(appDir, "temp");
			if (!Directory.Exists(tempDir))
				Directory.CreateDirectory(tempDir);

			_tempFilePath = Path.Combine(tempDir, tempFileName);
			File.Copy(originalPath, _tempFilePath, true);
		}

		public TempFile(Stream stream, string tempFileName)
		{
			string appDir = Directory.GetCurrentDirectory();
			string tempDir = Path.Combine(appDir, "temp");
			if (!Directory.Exists(tempDir))
				Directory.CreateDirectory(tempDir);

			_tempFilePath = Path.Combine(tempDir, tempFileName);
			using (FileStream tempFileStream = File.Open(_tempFilePath, FileMode.Create, FileAccess.Write))
			{
				stream.CopyTo(tempFileStream);
			}
		}

		public void Dispose()
		{
			if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(_tempFilePath);
				}
				catch (Exception e)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Warning: Could not delete {tempFilePath}!");
					Console.WriteLine($"{e.GetType().Name}: {e.Message}");
					Console.WriteLine(e.StackTrace);
					Console.ForegroundColor = ConsoleColor.White;
				}
			}
		}
	}
}
