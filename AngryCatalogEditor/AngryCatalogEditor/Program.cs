using Newtonsoft.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Drawing;
using ImageMagick;
using System.Runtime.InteropServices;
using System;
using System.Globalization;
using Octokit;
using FileMode = System.IO.FileMode;
using System.ComponentModel;

namespace AngryCatalogEditor
{
	public class BundleInfo
	{
		public class UpdateInfo
		{
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

		public string ExternalLink { get; set; }
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

	public class AngryBundleData
	{
		public string bundleName { get; set; }
		public string bundleAuthor { get; set; }
		[DefaultValue(2)]
		public int bundleVersion { get; set; }
		public string bundleGuid { get; set; }
		public string buildHash { get; set; }
		public string bundleDataPath { get; set; }
		public List<string> levelDataPaths;
	}

	internal class Program
	{
		static LevelCatalog catalog;
		static ScriptCatalog scriptCatalog;

		static string GetMD5Hash(string data)
		{
            MD5 md5 = MD5.Create();
            byte[] hashArr = md5.ComputeHash(Encoding.ASCII.GetBytes(data));
            return Convert.ToHexString(hashArr).ToLower();
        }

		static string GetMD5Hash(byte[] data)
		{
            MD5 md5 = MD5.Create();
            byte[] hashArr = md5.ComputeHash(data);
            return Convert.ToHexString(hashArr).ToLower();
        }

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
			string catalogSerialized = JsonConvert.SerializeObject(catalog, Formatting.Indented);
			catalogSerialized = catalogSerialized.Replace("\r", "");

			string hash = GetMD5Hash(catalogSerialized);

			File.WriteAllText(catalogPath, catalogSerialized);
			File.WriteAllText(catalogHashPath, hash);

			string scriptCatalogPath = Path.Combine(projectRoot, "ScriptCatalog.json");
			string scriptCatalogHashPath = Path.Combine(projectRoot, "ScriptCatalogHash.txt");
			string scriptCatalogSerialized = JsonConvert.SerializeObject(scriptCatalog, Formatting.Indented);
			scriptCatalogSerialized = scriptCatalogSerialized.Replace("\r", "");

			hash = GetMD5Hash(scriptCatalogSerialized);

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

		static AngryBundleData DataFromBundle(string filePath)
		{
			using (ZipArchive zip = new ZipArchive(File.Open(filePath, FileMode.Open, FileAccess.Read)))
			{
				var entry = zip.GetEntry("data.json");

				using (TextReader reader = new StreamReader(entry.Open()))
				{
					AngryBundleData data = JsonConvert.DeserializeObject<AngryBundleData>(reader.ReadToEnd());
					return data;
				}
			}
		}

		static string GuidFromBundle(string path)
		{
			return DataFromBundle(path).bundleGuid;
		}

		static bool IsGUID(string guid)
		{
			if (guid == null || guid.Length != 32)
				return false;

			foreach (char c in guid)
			{
				if (c >= '0' && c <= '9')
					continue;
				if (c >= 'a' && c <= 'f')
					continue;
				if (c >= 'A' && c <= 'F')
					continue;

				return false;
			}

			return true;
		}

		static string ProcessPath(string path)
		{
			if (path.StartsWith('"') && path.EndsWith('"'))
				return path.Substring(1, path.Length - 2);
			return path;
		}

		static void WriteWarning(string message)
		{
			ConsoleColor fg = Console.ForegroundColor;
			ConsoleColor bg = Console.BackgroundColor;

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.BackgroundColor = ConsoleColor.Black;

			Console.Write(message);

			Console.ForegroundColor = fg;
			Console.BackgroundColor = bg;
		}

		static void WriteLineWarning(string message)
		{
			WriteWarning(message);
			Console.WriteLine();
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
					if (string.IsNullOrEmpty(data.bundleName))
						WriteLineWarning("No bundle name");
					if (string.IsNullOrEmpty(data.bundleAuthor))
						WriteLineWarning("No bundle author");
					if (data.bundleVersion != 5)
						WriteLineWarning($"Old bundle version: {data.bundleVersion}");
				}

				/*var iconEntry = angry.GetEntry("icon.png");
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

				File.Delete(tempIconLocation);*/
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

			AngryCatalogEditor.BundleInfo foundLevel = catalog.Levels.Where(level => level.Guid == bundleInfo.guid).FirstOrDefault();
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

			MagickImage thumbnail = new MagickImage(thumbnailPath);
			ResizeToMinimum(thumbnail, 800, 600);
			string tempThumbnailPath = Path.Combine(tempPath, "tempThumbnail.png");
			thumbnail.Write(tempThumbnailPath);
			var opt = new ImageOptimizer();
			opt.LosslessCompress(tempThumbnailPath);

			Console.Write("BundleName: ");
			string bundleName = Console.ReadLine();
			Console.Write("Author: ");
			string author = Console.ReadLine();

			AngryCatalogEditor.BundleInfo newInfo = new AngryCatalogEditor.BundleInfo();
			newInfo.Name = bundleName;
			newInfo.Author = author;
			newInfo.Guid = bundleInfo.guid;
			newInfo.Hash = bundleInfo.buildHash;
			newInfo.ThumbnailHash = GetMD5Hash(File.ReadAllBytes(tempThumbnailPath));
			newInfo.Size = size;
			newInfo.LastUpdate = ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds();
			newInfo.Updates = new List<AngryCatalogEditor.BundleInfo.UpdateInfo>() { new AngryCatalogEditor.BundleInfo.UpdateInfo() { Hash = bundleInfo.buildHash, Message = "Initial upload" } };
			newInfo.Parts = new List<string>();

			Console.Write("External link (leave empty to upload to github): ");
			string externalLink = Console.ReadLine();
			string bundleDir = Path.Combine(projectRoot, "Levels", bundleInfo.guid);
			if (string.IsNullOrEmpty(externalLink))
			{
                externalLink = $"https://raw.githubusercontent.com/eternalUnion/AngryLevels/release/Levels/{bundleInfo.guid}/level.angry";
				newInfo.Parts.Add(externalLink);
				newInfo.ExternalLink = externalLink;
				File.Copy(bundlePath, Path.Combine(bundleDir, "level.angry"));
            }
			else
			{
                newInfo.Parts.Add(externalLink);
				for (int i = 2; ; i++)
				{
                    Console.Write($"External link (part {i}): ");
					string part = Console.ReadLine();
					if (string.IsNullOrEmpty(part))
						break;

					newInfo.Parts.Add(part);
                }

                newInfo.ExternalLink = externalLink;
                if (newInfo.Parts.Count != 1)
				{
                    newInfo.ExternalLink = "";
                }
            }

			catalog.Levels.Add(newInfo);
			SaveCatalog();

			Directory.CreateDirectory(bundleDir);
			File.Copy(tempThumbnailPath, Path.Combine(bundleDir, "thumbnail.png"));
			File.Delete(tempThumbnailPath);
		}

		static void UpdateBundle()
		{
			Console.Write("GUID or path: ");
			string guid = Console.ReadLine();
			
			if (!IsGUID(guid))
			{
				string path = ProcessPath(guid);
				if (File.Exists(path))
				{
					guid = GuidFromBundle(path);
				}
				else
				{
					Console.WriteLine("Invalid GUID");
					return;
				}
			}

			AngryCatalogEditor.BundleInfo bundle = catalog.Levels.Where(level => level.Guid == guid).FirstOrDefault();
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
					Console.WriteLine("Could not process bundle");
					return;
				}
				else
				{
					if (info.guid != guid)
					{
						Console.WriteLine("Bundle guid does not match with the request");
						return;
					}
					else if (bundle.Updates.Where(update => update.Hash == info.buildHash).Any())
					{
                        Console.WriteLine("Bundle with the same build hash is already in the updates list");
                        return;
                    }
					else
					{
						Console.Write("Update message: ");
						string updateMsg = Console.ReadLine().Replace("\\n", "\n");

						Console.WriteLine($"Old link: {bundle.ExternalLink}");
                        Console.Write("New external link (leave empty to upload to github): ");
                        string externalLink = Console.ReadLine();
						if (string.IsNullOrEmpty(externalLink))
						{
							bundle.ExternalLink = $"https://raw.githubusercontent.com/eternalUnion/AngryLevels/release/Levels/{bundle.Guid}/level.angry";
							File.Copy(bundlePath, Path.Combine(projectRoot, "Levels", guid, "level.angry"), true);
							bundle.Parts = new List<string>() { bundle.ExternalLink };
						}
						else
						{
                            string angryFile = Path.Combine(projectRoot, "Levels", bundle.Guid, "level.angry");
                            if (File.Exists(angryFile))
                                File.Delete(angryFile);

                            bundle.ExternalLink = externalLink;
                            bundle.Parts.Clear();
                            bundle.Parts.Add(externalLink);

                            for (int i = 2; ; i++)
                            {
                                Console.Write($"External link (part {i}): ");
                                string part = Console.ReadLine();
                                if (string.IsNullOrEmpty(part))
                                    break;

                                bundle.Parts.Add(part);
                                bundle.ExternalLink = "";
                            }
                        }

						bundle.Hash = info.buildHash;
						bundle.LastUpdate = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
						int size;
						using (FileStream fs = File.Open(bundlePath, FileMode.Open, FileAccess.Read))
							size = (int)fs.Length;
						bundle.Size = size;
						if (bundle.Updates == null)
							bundle.Updates = new List<AngryCatalogEditor.BundleInfo.UpdateInfo>();
						bundle.Updates.Add(new AngryCatalogEditor.BundleInfo.UpdateInfo() { Hash = info.buildHash, Message = updateMsg });
						
						changed = true;
					}
				}
			}
			else
			{
				Console.Write("New external link: ");
				string link = Console.ReadLine();

				if (!string.IsNullOrEmpty(link))
				{
					string angryFile = Path.Combine(projectRoot, "Levels", bundle.Guid, "level.angry");
					if (File.Exists(angryFile))
						File.Delete(angryFile);

					bundle.ExternalLink = link;
					bundle.Parts.Clear();
					bundle.Parts.Add(link);

                    for (int i = 2; ; i++)
                    {
                        Console.Write($"External link (part {i}): ");
                        string part = Console.ReadLine();
                        if (string.IsNullOrEmpty(part))
                            break;

                        bundle.Parts.Add(part);
						bundle.ExternalLink = "";
                    }

                    changed = true;
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

				bundle.ThumbnailHash = GetMD5Hash(File.ReadAllBytes(tempThumbnailPath));
				File.Copy(tempThumbnailPath, Path.Combine(projectRoot, "Levels", guid, "thumbnail.png"), true);
				File.Delete(tempThumbnailPath);
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

			AngryCatalogEditor.BundleInfo info = catalog.Levels.Where(level => level.Guid == guid).FirstOrDefault();
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

		static void ListAuthorNames()
		{
			foreach (string author in catalog.Levels.Select(level => level.Author).Distinct())
				Console.WriteLine($"- '{author}'");
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
			bool alreadyExists = File.Exists(Path.Combine(projectRoot, "Scripts", scriptName));
			File.Copy(scriptPath, Path.Combine(projectRoot, "Scripts", scriptName), true);
			File.Copy(scriptPath + ".cert", Path.Combine(projectRoot, "Scripts", scriptName + ".cert"), true);

			int size = 0;
			using (FileStream fs = File.Open(scriptPath, FileMode.Open, FileAccess.Read))
			{
				size = (int)fs.Length;
			}

			ScriptInfo info = scriptCatalog.Scripts.Where(script => script.FileName == scriptName).FirstOrDefault();
			if (info == null)
			{
				info = new ScriptInfo();
				info.Updates = new List<string>();
				scriptCatalog.Scripts.Add(info);
			}

			info.FileName = scriptName;
			info.Hash = scriptHash;
			info.Size = size;
			if (info.Updates == null)
				info.Updates = new List<string>();
			info.Updates.Add(scriptHash);

			if (alreadyExists)
				Console.WriteLine("Script overwritten");
			else
				Console.WriteLine("Script created");

			SaveCatalog();
		}

		static void SearchBundle()
		{
			Console.Write("Bundle name: ");
			string name = Console.ReadLine();

			if (string.IsNullOrEmpty(name))
				return;

			string[] query = name.Split(' ').Select(s => s.ToLower()).ToArray();

			bool found = false;
			foreach (var bundle in catalog.Levels)
			{
				bool skip = false;

				string currentName = bundle.Name.ToLower();
				foreach (string queryString in query)
					if (!currentName.Contains(queryString))
					{
						skip = true;
						break;
					}

				if (skip)
					continue;

				found = true;
				Console.WriteLine($"{bundle.Guid}: {bundle.Name}");
			}

			if (!found)
				Console.WriteLine("Not found");
		}

		static string projectRoot;
		static void Main(string[] args)
		{
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			projectRoot = "";

			string currentDir = Directory.GetCurrentDirectory();
			while (Directory.Exists(currentDir))
			{
				if (Directory.GetFiles(currentDir).Select(file => Path.GetFileName(file)).Contains("LevelCatalog.json"))
				{
					projectRoot = currentDir;
					break;
				}
				else
				{
					currentDir = Path.GetDirectoryName(currentDir);
				}
			}

			if (projectRoot == "")
			{
				Console.Write("Project root: ");
				projectRoot = Console.ReadLine();
			}

			Console.WriteLine("Loading catalog...");
			LoadCatalog();

			// DEBUG
			foreach (var level in catalog.Levels)
			{
				string thumbnailPath = Path.Combine(currentDir, "Levels", level.Guid, "thumbnail.png");
				string newHash = GetMD5Hash(File.ReadAllBytes(thumbnailPath));
				level.ThumbnailHash = newHash;
			}

			SaveCatalog();

			while (true)
			{
				Console.WriteLine("1 - Add bundle");
				Console.WriteLine("2 - Update bundle");
                Console.WriteLine("3 - Delete bundle");
				Console.WriteLine();
				Console.WriteLine("4 - List all authors");
				Console.WriteLine("5 - Change author name");
				Console.WriteLine();
				Console.WriteLine("6 - Add or update script");
				Console.WriteLine();
				Console.WriteLine("7 - Force save catalog");
				Console.WriteLine("8 - Get repo info");
				Console.WriteLine();
				Console.WriteLine("9 - Search bundle");
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
						ListAuthorNames();
					else if (choice == 5)
						ChangeAuthorName();
					else if (choice == 6)
						AddOrUpdateScript();
					else if (choice == 7)
						SaveCatalog();
					else if (choice == 8)
						GetRepo().Wait();
					else if (choice == 9)
						SearchBundle();
				}
			}
		}

		static async Task GetRepo()
		{
			var ownerName = "eternalUnion";
			var repositoryName = "AngryLevels";
			var defaultBranchName = "heads/dev";

			var client = new GitHubClient(new ProductHeaderValue("eternalUnion"))
			{

			};

			var repo = await client.Repository.Get(ownerName, repositoryName);
			var defaultBranch = await client.Git.Reference.Get(ownerName, repositoryName, defaultBranchName);

			Console.WriteLine(repo.GitUrl);
			Console.WriteLine(repo.FullName);
			Console.WriteLine(repo.Description);
			Console.WriteLine(defaultBranch.Url);

			var featureBranch = await client.Git.Reference.Create(ownerName,
				repositoryName,
				new NewReference("refs/heads/feature", defaultBranch.Object.Sha));

			
		}
	}
}