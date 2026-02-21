using AngryCatalogEditor.GUI.RudeLevelScripts.Essentials;
using AssetRipper.Addressables;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Structure;
using AssetRipper.Processing;
using AssetRipper.Processing.Editor;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Extensions;
using Newtonsoft.Json;
using SharpCompress.Compressors.Xz;
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
		public readonly RudeBundleData rudeBundleData;
		public readonly List<RudeLevelData> rudeLevelData = new();
		public readonly string path;
		public readonly long size;

		public class AngryFileStructureException : Exception
		{
			public AngryFileStructureException(string? cause) : base(cause) { }

			public AngryFileStructureException(string? cause, Exception? innerException) : base(cause, innerException) { }
		}

		private AngryFile(string path, AngryBundleData angryBundleData, RudeBundleData rudeBundleData, IReadOnlyCollection<RudeLevelData> rudeLevelData)
		{
			this.angryBundleData = angryBundleData;
			this.rudeBundleData = rudeBundleData;
			this.rudeLevelData.AddRange(rudeLevelData);
			this.path = path;

			using (FileStream fs = File.OpenRead(path))
				size = fs.Length;
		}

		private static readonly Regex assetBundleRegex = new Regex(@"\{AngryLevelLoader\.Plugin\.tempFolderPath\}\\+[a-f\d]{32}\\+(.+_assets_all\.bundle)");

		public static bool TryLoadFile(string filePath, [NotNullWhen(returnValue: true)] out AngryFile? angryFile, [NotNullWhen(returnValue: false)] out Exception? ex)
		{
			angryFile = null;

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

				if (bundleData.bundleVersion < 6)
				{
					ex = new AngryFileStructureException("Outdated angry file version");
					return false;
				}

				if (bundleData.bundleVersion > 6)
				{
					ex = new AngryFileStructureException("Invalid angry file version");
					return false;
				}

				ZipArchiveEntry catalogEntry = archive.GetEntry("catalog.json");
				if (catalogEntry == null)
				{
					ex = new AngryFileStructureException("Angry file has no content catalog");
					return false;
				}

				Catalog catalog;
				using (Stream catalogStream = catalogEntry.Open())
				{
					using TempFile tempCatalogFile = new TempFile(catalogStream, "catalog.json");
					catalog = Catalog.FromJsonFile(tempCatalogFile.tempFilePath);

					if (catalog == null)
					{
						ex = new AngryFileStructureException("Angry file has no content catalog");
						return false;
					}
				}

				string? bundleId = catalog.InternalIds
					.Where(id => id.EndsWith(".bundle"))
					.Where(id => assetBundleRegex.IsMatch(id))
					.FirstOrDefault();

				if (bundleId == null)
				{
					ex = new AngryFileStructureException("Could not locate the asset bundle file");
					return false;
				}

				Match match = assetBundleRegex.Match(bundleId);
				string assetBundleName = match.Groups[1].Value;

				ZipArchiveEntry assetBundleEntry = archive.GetEntry(assetBundleName);
				if (assetBundleEntry == null)
				{
					ex = new AngryFileStructureException("Could not locate the asset bundle file in the zip archive");
					return false;
				}

				Stream assetBundleStream = assetBundleEntry.Open();
				using TempFile assetBundleFile = new TempFile(assetBundleStream, assetBundleName);
				assetBundleStream.Close();
				using GameStructure gameStructure = GameStructure.Load([assetBundleFile.tempFilePath], new AssetRipper.Import.Configuration.CoreConfiguration());
				GameData gameData = GameData.FromGameStructure(gameStructure);

				// Force dispose files
				GC.Collect();
				GC.WaitForPendingFinalizers();

				new MainAssetProcessor().Process(gameData);
				new EditorFormatProcessor(AssetRipper.Processing.Configuration.BundledAssetsExportMode.DirectExport).Process(gameData);
				new SpriteProcessor().Process(gameData);

				Bundle assetBundle = gameData.GameBundle.Bundles.Where(b => b.Name == assetBundleName.ToLower()).First();
				AssetCollection assetCollection = assetBundle.Collections[0];

				IMonoBehaviour bundleDataObj = (IMonoBehaviour) assetCollection.Assets.Where(a => a.Value.OriginalName == bundleData.bundleDataPath).First().Value;
					
				List<IMonoBehaviour> levelDataObjects = new();
				foreach (string levelPath in bundleData.levelDataPaths)
				{
					levelDataObjects.Add((IMonoBehaviour)assetCollection.Assets.Where(a => a.Value.OriginalName == levelPath).First().Value);
				}

				RudeBundleData rudeBundleData = new RudeBundleData(bundleDataObj, gameData);
				if (rudeBundleData.levelIcon == null)
				{
					Console.WriteLine("null bundle icon");
					rudeBundleData.levelIcon = noThumbnailImageSquare;
				}

				List<RudeLevelData> rudeLevelData = new();
				foreach (IMonoBehaviour levelDataObj in levelDataObjects)
				{
					RudeLevelData levelData = new RudeLevelData(levelDataObj, gameData);
					if (levelData.levelPreviewImage == null)
					{
						levelData.levelPreviewImage = noThumbnailImage;
					}

					rudeLevelData.Add(levelData);
				}

				archive.Dispose();

				ex = null;
				angryFile = new AngryFile(filePath, bundleData, rudeBundleData, rudeLevelData);
				return true;
			}
		}
	
		public BundleInfo GetBundleInfo()
		{
			List<BundleInfo.LevelInfo> levelInfos = new List<BundleInfo.LevelInfo>();
			foreach (RudeLevelData levelData in rudeLevelData)
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
				Name = rudeBundleData.bundleName,
				Author = rudeBundleData.author,
				Guid = angryBundleData.bundleGuid,
				Hash = angryBundleData.buildHash,
				Size = (int) size,
				Levels = levelInfos,
			};

			return bundleInfo;
		}
	}
}
