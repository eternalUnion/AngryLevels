using AngryCatalogEditor.GUI.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NativeFileDialogs.Net;
using Newtonsoft.Json;

namespace AngryCatalogEditor.GUI.Pages
{
	public class AddBundleModel : PageModel
    {
		private static MemoryStream? thumbnailStream = null;
		private static AngryFile? angryFile = null;

		public IActionResult OnGetBundleThumbnail()
		{
			if (angryFile == null)
				return BadRequest("Angry file not loaded");

			return File(angryFile.bundleIcon, "image/png");
		}

		public IActionResult OnGetLevelThumbnail()
		{
			string? levelId = Request.Query["id"];
			if (levelId == null)
				return BadRequest("No level id provided");

			if (angryFile == null)
				return BadRequest("Angry file not loaded");

			if (!angryFile.levelThumbnailMap.TryGetValue(levelId, out byte[]? levelThumbnail))
				return BadRequest("Level not found");

			return File(levelThumbnail, "image/png");
		}

		public IActionResult OnPostOpenThumbnail()
        {
			MemoryStream newThumbnailStream = new MemoryStream();

			try
			{
				NfdStatus status = Dialog.OpenFile(out string? path);
				if (status == NfdStatus.Cancelled || string.IsNullOrEmpty(path))
					return StatusCode(StatusCodes.Status204NoContent);


				var thumbnail = new MagickImage(path);
				ImageUtils.ResizeToMinimum(thumbnail, 800, 600);

				var opt = new ImageOptimizer();
				thumbnail.Write(newThumbnailStream);
				newThumbnailStream.Position = 0;
				opt.LosslessCompress(newThumbnailStream);
				newThumbnailStream.Position = 0;

				if (thumbnailStream != null)
					thumbnailStream.Dispose();
				thumbnailStream = newThumbnailStream;

				thumbnailStream.Position = 0;
				MemoryStream cloneThumbnailStream = new MemoryStream();
				thumbnailStream.CopyTo(cloneThumbnailStream);
				cloneThumbnailStream.Position = 0;

				return File(cloneThumbnailStream, "image/png");
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(ex.StackTrace);
				Console.ForegroundColor = ConsoleColor.White;

				newThumbnailStream.Dispose();
				return BadRequest("Internal exception:\n\n" + ex.Message);
			}
		}
		
		public IActionResult OnPostOpenAngryFile()
		{
			NfdStatus status = Dialog.OpenFile(out string? path);
			if (status == NfdStatus.Cancelled || string.IsNullOrEmpty(path))
				return StatusCode(StatusCodes.Status204NoContent);

			AngryFile angryFile;
			try
			{
				if (!AngryFile.TryLoadFile(path, out angryFile, out Exception ex))
				{
					return BadRequest(ex.Message);
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
				Console.ForegroundColor = ConsoleColor.White;
				return BadRequest("Internal exception, see console");
			}

			BundleInfo? existingLevel;
			if (AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog) && (existingLevel = catalog.Levels.Where(l => l.Guid == angryFile.angryBundleData.bundleGuid).FirstOrDefault()) != null)
			{
				return BadRequest($"Another bundle with name {existingLevel.Name} exists in the catalog with the same global ID!");
			}

			AddBundleModel.angryFile = angryFile;
			return Content(JsonConvert.SerializeObject(angryFile.GetBundleInfo()), "application/json");
		}
	
		public class AddLevelBody
		{
			public string? Name { get; set; }
			public string? Author { get; set; }
			public bool? EpilepsyWarning { get; set; }
			public string? UpdateMessage { get; set; }
			public string[]? ExternalURLs { get; set; }
		}

		public IActionResult OnPostAddBundle([FromBody] AddLevelBody info)
		{
			if (info == null)
				return BadRequest("Bad body");

			if (info.Name == null)
				return BadRequest("No bundle name provided");

			if (info.Author == null)
				return BadRequest("No author provided");

			if (info.EpilepsyWarning == null)
				return BadRequest("No epilepsy warning toggle provided");

			if (info.ExternalURLs == null || info.ExternalURLs.Length == 0)
				return BadRequest("No external URL provided");

			if (thumbnailStream == null)
				return BadRequest("No thumbnail provided");

			if (angryFile == null)
				return BadRequest("No angry file provided");

			if (!AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog))
				return BadRequest("Failed to get the level catalog");

			if (catalog.Levels.Where(l => l.Guid == angryFile.angryBundleData.bundleGuid).Any())
				return BadRequest("Another level with the same GUID already exists");

			// Revert catalog to make sure that only proper changes are done
			GitHandler.HardResetCatalogs();

			BundleInfo bundleInfo = new BundleInfo()
			{
				Name = info.Name,
				Author = info.Author,
				Size = (int)angryFile.size,
				Guid = angryFile.angryBundleData.bundleGuid,
				Hash = angryFile.angryBundleData.buildHash,
				FileMD5 = angryFile.md5,
				ThumbnailHash = CryptologyUtils.GetMD5Hash(thumbnailStream),
				Locked = false,
				EpilepsyWarning = (bool)info.EpilepsyWarning,
				Parts = new List<string>(info.ExternalURLs),
				LastUpdate = ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds(),
				Updates = new List<BundleInfo.UpdateInfo>() { new BundleInfo.UpdateInfo() {
					Date = ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds(),
					Hash = angryFile.angryBundleData.buildHash,
					Message = info.UpdateMessage ?? "Initial upload."
				} },
				Levels = new List<BundleInfo.LevelInfo>()
			};

			foreach (var level in angryFile.angryBundleData.levels)
			{
				bundleInfo.Levels.Add(new BundleInfo.LevelInfo()
				{
					LevelName = level.levelName,
					LevelId = level.uniqueIdentifier,
					isSecretLevel = level.isSecretLevel,
					requiredCompletedLevelIdsForUnlock = new List<string>(level.requiredCompletedLevelIdsForUnlock),
					secretCount = level.secretCount,
					levelChallengeEnabled = level.levelChallengeEnabled,
					levelChallengeText = level.levelChallengeText,
					requiredDllNames = new List<string>(level.requiredDllNames)
				});
			}

			string bundlePath = Path.Combine(ProjectPaths.rootPath, "Levels", angryFile.angryBundleData.bundleGuid);
			if (!Directory.Exists(bundlePath))
				Directory.CreateDirectory(bundlePath);

			thumbnailStream.Position = 0;
			using (FileStream fs = System.IO.File.Open(Path.Combine(bundlePath, "thumbnail.png"), FileMode.OpenOrCreate, FileAccess.Write))
			{
				thumbnailStream.CopyTo(fs);
			}
			bundleInfo.ThumbnailHash = CryptologyUtils.GetMD5Hash(System.IO.File.ReadAllBytes(Path.Combine(bundlePath, "thumbnail.png")));

			catalog.Levels.Add(bundleInfo);
			AngryCatalogHandler.SaveLevelCatalog();

			if (!Directory.Exists(Path.Combine(bundlePath, "LevelThumbnails")))
				Directory.CreateDirectory(Path.Combine(bundlePath, "LevelThumbnails"));

			foreach (var level in angryFile.angryBundleData.levels)
			{
				if (!angryFile.levelThumbnailMap.TryGetValue(level.uniqueIdentifier, out byte[]? levelThumbnail))
					continue;

				using (FileStream fs = System.IO.File.Open(Path.Combine(bundlePath, "LevelThumbnails", $"{CryptologyUtils.GetMD5Hash(level.uniqueIdentifier)}.png"), FileMode.OpenOrCreate, FileAccess.Write))
				{
					fs.Write(levelThumbnail, 0, levelThumbnail.Length);
				}
			}

			GitHandler.Commit($"Added {angryFile.angryBundleData.bundleName}\nAuthor: {angryFile.angryBundleData.bundleAuthor}");

			return Redirect("/");
		}
	}
}
