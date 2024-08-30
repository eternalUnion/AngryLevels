namespace AngryV1Splitter
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

			string dir = Path.GetDirectoryName(path);

			using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
			using (BinaryReader br = new BinaryReader(fs))
			{
				int bundleCount = br.ReadInt32();
				int currentOffset = 0;

				for (int i = 0; i < bundleCount; i++)
				{
					fs.Seek(4 + i * 4, SeekOrigin.Begin);
					int bundleLen = br.ReadInt32();

					byte[] bundleData = new byte[bundleLen];
					fs.Seek(4 + bundleCount * 4 + currentOffset, SeekOrigin.Begin);
					fs.Read(bundleData, 0, bundleLen);
					File.WriteAllBytes(Path.Combine(dir, i.ToString()), bundleData);

					currentOffset += bundleLen;
				}
			}
		}
	}
}
