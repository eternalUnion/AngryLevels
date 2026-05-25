using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AngryCatalogEditor.GUI.IO
{
	public class AngryFile
	{
		// Placeholder textures

		public static byte[] noThumbnailImage;
		public static byte[] noThumbnailImageSquare;

		static AngryFile() {
			noThumbnailImage = File.ReadAllBytes("wwwroot/images/no_thumbnail.png");
			noThumbnailImageSquare = File.ReadAllBytes("wwwroot/images/no_thumbnail_square.png");
		}

		public readonly AngryBundleData angryBundleData;
		public readonly string path;
		public readonly long size;
		public readonly string md5;

		public byte[] bundleIcon;
		public Dictionary<string, byte[]> levelThumbnailMap = new();

		public class AngryFileStructureException : Exception
		{
			public AngryFileStructureException(string? cause) : base(cause) { }

			public AngryFileStructureException(string? cause, Exception? innerException) : base(cause, innerException) { }
		}

		private AngryFile(string path, AngryBundleData angryBundleData, long size, string md5)
		{
			this.angryBundleData = angryBundleData;
			this.path = path;
			this.size = size;
			this.md5 = md5;
		}

		private static readonly Regex assetBundleRegex = new Regex(@"\{AngryLevelLoader\.Plugin\.tempFolderPath\}\\+[a-f\d]{32}\\+(.+_assets_all\.bundle)");

		public static bool TryLoadFile(string filePath, [NotNullWhen(returnValue: true)] out AngryFile? angryFile, [NotNullWhen(returnValue: false)] out Exception? ex)
		{
			angryFile = null;

			long size = 0;
			string md5 = "";

			using (FileStream fs = File.OpenRead(filePath))
				size = fs.Length;

			using (FileStream fs = File.OpenRead(filePath))
				md5 = CryptologyUtils.GetMD5Hash(fs);

			using (ZipArchive archive = new ZipArchive(System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read), ZipArchiveMode.Read, false))
			{
				ZipArchiveEntry? dataEntry = archive.GetEntry("data.json");
				if (dataEntry == null)
				{
					ex = new AngryFileStructureException("Angry file has no data");
					return false;
				}

				AngryBundleData bundleData;
				using (Stream dataStream = dataEntry.Open())
				{
					StreamReader reader = new StreamReader(dataStream, Encoding.UTF8);
					string dataText = reader.ReadToEnd();
					bundleData = JsonConvert.DeserializeObject<AngryBundleData>(dataText);

					if (bundleData == null)
					{
						ex = new AngryFileStructureException("Angry file has no data");
						return false;
					}
				}

				if (bundleData.bundleVersion < 2 || bundleData.bundleVersion > 7)
				{
					ex = new AngryFileStructureException("Invalid angry file version");
					return false;
				}

				if (bundleData.bundleVersion < 7)
				{
					ex = new AngryFileStructureException("Outdated angry file version");
					return false;
				}

				ex = null;
				angryFile = new AngryFile(filePath, bundleData, size, md5);

				ZipArchiveEntry? bundleIconEntry = archive.GetEntry("icon.png");
				if (bundleIconEntry != null)
				{
					using Stream iconStream = bundleIconEntry.Open();
					using MemoryStream memoryStream = new MemoryStream();
					iconStream.CopyTo(memoryStream);

					angryFile.bundleIcon = memoryStream.ToArray();
				}
				else
				{
					angryFile.bundleIcon = noThumbnailImageSquare;
				}

				foreach (AngryLevelData levelData in bundleData.levels)
				{
					string levelHash = CryptologyUtils.GetMD5Hash(levelData.uniqueIdentifier);
					ZipArchiveEntry? thumbnailEntry = archive.GetEntry($"LevelThumbnails/{levelHash}.png");

					if (thumbnailEntry != null)
					{
						using Stream thumbnailStream = thumbnailEntry.Open();
						using MemoryStream memoryStream = new MemoryStream();
						thumbnailStream.CopyTo(memoryStream);

						angryFile.levelThumbnailMap[levelData.uniqueIdentifier] = memoryStream.ToArray();
					}
					else
					{
						angryFile.levelThumbnailMap[levelData.uniqueIdentifier] = noThumbnailImage;
					}
				}

				return true;
			}
		}
	
		public BundleInfo GetBundleInfo()
		{
			List<BundleInfo.LevelInfo> levelInfos = new List<BundleInfo.LevelInfo>();
			foreach (AngryLevelData levelData in angryBundleData.levels)
			{
				BundleInfo.LevelInfo levelInfo = new BundleInfo.LevelInfo()
				{
					LevelName = levelData.levelName,
					LevelId = levelData.uniqueIdentifier,
					isSecretLevel = levelData.isSecretLevel,
					requiredCompletedLevelIdsForUnlock = levelData.requiredCompletedLevelIdsForUnlock.ToList(),
					secretCount = levelData.secretCount,
					levelChallengeEnabled = levelData.levelChallengeEnabled,
					levelChallengeText = levelData.levelChallengeText,
					requiredDllNames = levelData.requiredDllNames.ToList()
				};

				levelInfos.Add(levelInfo);
			}

			BundleInfo bundleInfo = new BundleInfo()
			{
				Name = angryBundleData.bundleName,
				Author = angryBundleData.bundleAuthor,
				Guid = angryBundleData.bundleGuid,
				Hash = angryBundleData.buildHash,
				FileMD5 = md5,
				Size = (int) size,
				Levels = levelInfos,
			};

			return bundleInfo;
		}
	}
}
