namespace AngryCombiner
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Console.Write("File path: ");
			string path = Console.ReadLine();

			if (path.StartsWith('"'))
				path = path.Substring(1, path.Length - 2);

			if (!File.Exists(path))
			{
				Console.WriteLine("File not found");
				Console.ReadKey();
				return;
			}

			if (Path.GetExtension(path) != ".angry1")
			{
				Console.WriteLine("Is not a .angry1 file");
				Console.ReadKey();
				return;
			}

			string fname = Path.GetFileNameWithoutExtension(path);
			string fdir = Path.GetDirectoryName(path);

			string targetFile = Path.Combine(fdir, fname + ".angry");
			if (File.Exists(targetFile))
			{
				Console.WriteLine("Target file already exists");
				Console.ReadKey();
			}

			using (var fs = File.Open(targetFile, FileMode.Create, FileAccess.Write))
			{
				for (int partedFile = 1; ; partedFile++)
				{
					string partedPath = Path.Combine(fdir, fname + $".angry{partedFile}");
					if (!File.Exists(partedPath))
						break;

					byte[] partedBytes = File.ReadAllBytes(partedPath);
					fs.Write(partedBytes, 0, partedBytes.Length);
				}
			}
		}
	}
}