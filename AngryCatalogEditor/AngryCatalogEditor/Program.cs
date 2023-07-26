using Newtonsoft.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Drawing;
using ImageMagick;
using System.Runtime.InteropServices;
using System;

namespace AngryCatalogEditor
{
	public class LevelInfo
	{
		public string Name { get; set; }
		public string Author { get; set; }
		public int Size { get; set; }
		public string Guid { get; set; }
		public string Hash { get; set; }
		public string ThumbnailHash { get; set; }
		public long LastUpdate { get; set; }
	}

	public class LevelCatalog
	{
		public List<LevelInfo> Levels;
	}

	public class ScriptInfo
	{
		public string FileName { get; set; }
		public string Hash { get; set; }
		public int Size { get; set; }
	}

	public class ScriptCatalog
	{
		public List<ScriptInfo> Scripts;
	}

	public class AngryBundleData
	{
		public List<string> levelDataPaths;
		public string bundleGuid { get; set; }
		public string buildHash { get; set; }
		public string bundleDataPath { get; set; }
	}

	internal class Program
	{
		static LevelCatalog catalog;
		static ScriptCatalog scriptCatalog;

		static void LoadCatalog()
		{
			string catalogPath = Path.Combine(projectRoot, "LevelCatalog.json");
			catalog = JsonConvert.DeserializeObject<LevelCatalog>(File.ReadAllText(catalogPath));
			string scriptCatalogPath = Path.Combine(projectRoot, "ScriptCatalog.json");
			scriptCatalog = JsonConvert.DeserializeObject<ScriptCatalog>(File.ReadAllText(scriptCatalogPath));
		}

		static void SaveCatalog()
		{
			string catalogPath = Path.Combine(projectRoot, "LevelCatalog.json");
			string catalogHashPath = Path.Combine(projectRoot, "LevelCatalogHash.txt");
			string catalogSerialized = JsonConvert.SerializeObject(catalog);

			MD5 md5 = MD5.Create();
			byte[] hashArr = md5.ComputeHash(Encoding.ASCII.GetBytes(catalogSerialized));
			string hash = Convert.ToHexString(hashArr).ToLower();

			File.WriteAllText(catalogPath, catalogSerialized);
			File.WriteAllText(catalogHashPath, hash);

			string scriptCatalogPath = Path.Combine(projectRoot, "ScriptCatalog.json");
			string scriptCatalogHashPath = Path.Combine(projectRoot, "ScriptCatalogHash.txt");
			string scriptCatalogSerialized = JsonConvert.SerializeObject(scriptCatalog);

			md5 = MD5.Create();
			hashArr = md5.ComputeHash(Encoding.ASCII.GetBytes(scriptCatalogSerialized));
			hash = Convert.ToHexString(hashArr).ToLower();

			File.WriteAllText(scriptCatalogPath, scriptCatalogSerialized);
			File.WriteAllText(scriptCatalogHashPath, hash);
		}

		static void ResizeToMinimum(MagickImage img, int width, int height)
		{
			float targetAspect = (float)width / (float)height;
			float imgAspect = (float)img.Width / (float)img.Height;

			// Too wide
			if (imgAspect > targetAspect)
			{
				img.Crop((int)(img.Height * targetAspect), img.Height, Gravity.Center);
			}
			// Too tall
			if (imgAspect < targetAspect)
			{
				img.Crop(img.Width, (int)(img.Width * (1f / targetAspect)), Gravity.Center);
			}

			if (img.Width > width)
				img.Resize(width, 0);
		}

		static string RandomGuid()
		{
			Random rnd = new Random(DateTime.Now.Millisecond);
			byte[] b = new byte[16];
			rnd.NextBytes(b);
			return Convert.ToHexString(b).ToLower();
		}

		static string ProcessPath(string path)
		{
			if (path.StartsWith('"') && path.EndsWith('"'))
				return path.Substring(1, path.Length - 2);
			return path;
		}

		struct BundleInfo
		{
			public string guid;
			public string buildHash;
		}

		static bool ProcessBundle(string bundlePath, string tempPath, out BundleInfo info)
		{
			info = new BundleInfo();
			
			using (ZipArchive angry = new ZipArchive(File.Open(bundlePath, FileMode.Open, FileAccess.ReadWrite), ZipArchiveMode.Update))
			{
				var dataEntry = angry.GetEntry("data.json");
				using (TextReader dataReader = new StreamReader(dataEntry.Open()))
				{
					AngryBundleData data = JsonConvert.DeserializeObject<AngryBundleData>(dataReader.ReadToEnd());

					info.guid = data.bundleGuid;
					info.buildHash = data.buildHash;
				}

				var iconEntry = angry.GetEntry("icon.png");
				MagickImage icon;
				using (Stream iconStream = iconEntry.Open())
				{
					icon = new MagickImage(iconStream, MagickFormat.Png);
				}
				ResizeToMinimum(icon, 256, 256);
				string tempIconLocation = Path.Combine(tempPath, "icon.png");
				icon.Write(tempIconLocation);
				var optimizer = new ImageOptimizer();
				optimizer.LosslessCompress(tempIconLocation);

				using (BinaryWriter iconWriter = new BinaryWriter(iconEntry.Open()))
				{
					iconWriter.BaseStream.Seek(0, SeekOrigin.Begin);
					iconWriter.BaseStream.SetLength(0);
					iconWriter.Write(File.ReadAllBytes(tempIconLocation));
				}

				File.Delete(tempIconLocation);
				return true;
			}
		}

		static void AddBundle()
		{
			Console.Write("Angry bundle path: ");
			string bundlePath = ProcessPath(Console.ReadLine());
			if (!File.Exists(bundlePath))
			{
				Console.WriteLine("Cancelled");
				return;
			}

			string tempPath = Path.Combine(projectRoot, "temp");
			if (Directory.Exists(tempPath))
				Directory.Delete(tempPath, true);
			Directory.CreateDirectory(tempPath);

			BundleInfo bundleInfo;
			if (!ProcessBundle(bundlePath, tempPath, out bundleInfo))
			{
				Console.WriteLine("Could not process bundle");
				return;
			}

			LevelInfo foundLevel = catalog.Levels.Where(level => level.Guid == bundleInfo.guid).FirstOrDefault();
			if (foundLevel != null)
			{
				Console.WriteLine("Level with the same guid already exists");
				return;
			}

			int size;
			using (FileStream fs = File.Open(bundlePath, FileMode.Open, FileAccess.Read))
				size = (int)fs.Length;

			Console.Write("Thumbnail path: ");
			string thumbnailPath = ProcessPath(Console.ReadLine());
			if (!File.Exists(thumbnailPath))
			{
				Console.WriteLine("Cancelled");
				return;
			}

			MagickImage thumbnail = new MagickImage(thumbnailPath, MagickFormat.Png);
			ResizeToMinimum(thumbnail, 800, 600);
			string tempThumbnailPath = Path.Combine(tempPath, "tempThumbnail.png");
			thumbnail.Write(tempThumbnailPath);
			var opt = new ImageOptimizer();
			opt.LosslessCompress(tempThumbnailPath);

			Console.Write("BundleName: ");
			string bundleName = Console.ReadLine();
			Console.Write("Author: ");
			string author = Console.ReadLine();

			LevelInfo newInfo = new LevelInfo();
			newInfo.Name = bundleName;
			newInfo.Author = author;
			newInfo.Guid = bundleInfo.guid;
			newInfo.Hash = bundleInfo.buildHash;
			newInfo.ThumbnailHash = RandomGuid();
			newInfo.Size = size;
			newInfo.LastUpdate = ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds();

			catalog.Levels.Add(newInfo);
			SaveCatalog();

			string bundleDir = Path.Combine(projectRoot, "Levels", bundleInfo.guid);
			Directory.CreateDirectory(bundleDir);
			File.Copy(bundlePath, Path.Combine(bundleDir, "level.angry"));
			File.Copy(tempThumbnailPath, Path.Combine(bundleDir, "thumbnail.png"));
			File.Delete(tempThumbnailPath);
		}

		static void UpdateBundle()
		{
			Console.Write("GUID: ");
			string guid = Console.ReadLine();
			LevelInfo bundle = catalog.Levels.Where(level => level.Guid == guid).FirstOrDefault();
			if (bundle == null)
			{
				Console.WriteLine("Not found");
				return;
			}

			Console.WriteLine("Leave blank to leave unchanged");
			bool changed = false;

			Console.Write($"Name ({bundle.Name}): ");
			string name = Console.ReadLine();
			if (!string.IsNullOrEmpty(name))
			{
				bundle.Name = name;
				changed = true;
			}

			Console.Write($"Author ({bundle.Author}): ");
			string author = Console.ReadLine();
			if (!string.IsNullOrEmpty(author))
			{
				bundle.Author = author;
				changed = true;
			}

			string tempPath = Path.Combine(projectRoot, "temp");
			if (!Directory.Exists(tempPath))
				Directory.CreateDirectory(tempPath);

			Console.Write($"New bundle path: ");
			string bundlePath = ProcessPath(Console.ReadLine());
			if (!string.IsNullOrEmpty(bundlePath))
			{
				BundleInfo info;
				if (!ProcessBundle(bundlePath, tempPath, out info))
				{
					Console.WriteLine("Could not process bundle, skipping");
				}
				else
				{
					if (info.guid != guid)
					{
						Console.WriteLine("Bundle guid does not match with the request");
					}
					else
					{
						File.Copy(bundlePath, Path.Combine(projectRoot, "Levels", guid, "level.angry"), true);
						bundle.Hash = info.buildHash;
						bundle.LastUpdate = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
						int size;
						using (FileStream fs = File.Open(bundlePath, FileMode.Open, FileAccess.Read))
							size = (int)fs.Length;
						bundle.Size = size;
						changed = true;
					}
				}
			}

			Console.Write($"New thumbnail path: ");
			string thumbnailPath = ProcessPath(Console.ReadLine());
			if (!string.IsNullOrEmpty(thumbnailPath))
			{
				MagickImage thumbnail = new MagickImage(thumbnailPath, MagickFormat.Png);
				ResizeToMinimum(thumbnail, 800, 600);
				string tempThumbnailPath = Path.Combine(tempPath, $"tempThumbnail_{DateTime.Now.Second}.png");
				thumbnail.Write(tempThumbnailPath);
				var opt = new ImageOptimizer();
				opt.LosslessCompress(tempThumbnailPath);

				File.Copy(tempThumbnailPath, Path.Combine(projectRoot, "Levels", guid, "thumbnail.png"), true);
				File.Delete(tempThumbnailPath);
				bundle.ThumbnailHash = RandomGuid();
				changed = true;
			}
		
			if (changed)
			{
				SaveCatalog();
			}
		}

		static void DeleteBundle()
		{
			Console.Write("Guid: ");
			string guid = Console.ReadLine().ToLower();

			LevelInfo info = catalog.Levels.Where(level => level.Guid == guid).FirstOrDefault();
			if (info != null)
			{
				string path = Path.Combine(projectRoot, "Levels", guid);
				Directory.Delete(path, true);

				catalog.Levels.Remove(info);
				SaveCatalog();
				Console.WriteLine("Removed");
			}
			else
			{
				Console.WriteLine("Could not find guid");
			}
		}

		static void ChangeAuthorName()
		{
			Console.Write("Author: ");
			string author = Console.ReadLine();

			if (string.IsNullOrEmpty(author))
			{
				Console.WriteLine("Aborted");
				return;
			}

			Console.Write("New Name: ");
			string newName = Console.ReadLine();

			if (string.IsNullOrEmpty(newName))
			{
				Console.WriteLine("Aborted");
				return;
			}

			if (catalog.Levels.Where(bundle => bundle.Author == newName).FirstOrDefault() != null)
			{
				Console.WriteLine("New name already exists, aborting");
				return;
			}

			catalog.Levels.ForEach(level =>
			{
				if (level.Author == author) level.Author = newName;
			});

			SaveCatalog();
		}

		static void AddOrUpdateScript()
		{
			Console.Write("Script path: ");
			string scriptPath = ProcessPath(Console.ReadLine());
			if (!File.Exists(scriptPath))
			{
				Console.WriteLine("Aborted");
				return;
			}

			string certificatePath = scriptPath + ".cert";
			if (!File.Exists(certificatePath))
			{
				Console.WriteLine("Certificate not found");
				return;
			}

			MD5 md5 = MD5.Create();
			byte[] hashArr = md5.ComputeHash(File.ReadAllBytes(scriptPath));
			string scriptHash = Convert.ToHexString(hashArr).ToLower();

			string scriptName = Path.GetFileName(scriptPath);
			File.Copy(scriptPath, Path.Combine(projectRoot, "Scripts", scriptName), true);
			File.Copy(scriptPath + ".cert", Path.Combine(projectRoot, "Scripts", scriptName + ".cert"));

			int size = 0;
			using (FileStream fs = File.Open(scriptPath, FileMode.Open, FileAccess.Read))
			{
				size = (int)fs.Length;
			}

			ScriptInfo info = scriptCatalog.Scripts.Where(script => script.FileName == scriptName).FirstOrDefault();
			if (info == null)
			{
				info = new ScriptInfo();
				scriptCatalog.Scripts.Add(info);
			}

			info.FileName = scriptName;
			info.Hash = scriptHash;
			info.Size = size;

			SaveCatalog();
		}

		static string projectRoot;
		static void Main(string[] args)
		{
			Console.Write("Project root: ");
			projectRoot = Console.ReadLine();

			Console.WriteLine("Loading catalog...");
			LoadCatalog();
			
			while (true)
			{
				Console.WriteLine("1 - Add bundle");
				Console.WriteLine("2 - Update bundle");
				Console.WriteLine("3 - Delete bundle");
				Console.WriteLine();
				Console.WriteLine("4 - Change author name");
				Console.WriteLine();
				Console.WriteLine("5 - Add or update script");
				Console.Write("> ");

				string str = Console.ReadLine();
				if (int.TryParse(str, out int choice))
				{
					if (choice == 1)
						AddBundle();
					else if (choice == 2)
						UpdateBundle();
					else if (choice == 3)
						DeleteBundle();
					else if (choice == 4)
						ChangeAuthorName();
					else if (choice == 5)
						AddOrUpdateScript();
				}
			}
		}
	}
}