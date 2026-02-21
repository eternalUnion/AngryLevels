using AngryCatalogEditor.GUI.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NativeFileDialogs.Net;
using Newtonsoft.Json;
using System.Text;

namespace AngryCatalogEditor.GUI.Pages
{
	[IgnoreAntiforgeryToken]
    public class ModifyBundleModel : PageModel
    {
		private static MemoryStream? thumbnailStream = null;
		private static AngryFile? angryFile = null;

		public IActionResult OnGet()
		{
			string? guid = Request.Query["guid"];
			if (guid == null)
				return BadRequest("No guid provided");

			if (!AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog))
				return BadRequest("Could not obtain catalog");

			var bundle = catalog.Levels.Where(l => l.Guid == guid).FirstOrDefault();
			if (bundle == null)
				return BadRequest("No such bundle exists");

			return Page();
		}

		public IActionResult OnGetBundleThumbnail()
		{
			if (angryFile == null)
				return BadRequest("Angry file not loaded");

			if (angryFile.rudeBundleData.levelIcon == null)
				return new NoContentResult();

			return File(angryFile.rudeBundleData.levelIcon, "image/png");
		}

		public IActionResult OnGetLevelThumbnail()
		{
			string? levelId = Request.Query["id"];
			if (levelId == null)
				return BadRequest("No level id provided");

			if (angryFile == null)
				return BadRequest("Angry file not loaded");

			var level = angryFile.rudeLevelData.Where(l => l.uniqueIdentifier == levelId).FirstOrDefault();
			if (level == null)
				return BadRequest("Level not found");

			if (level.levelPreviewImage == null)
				return new NoContentResult();

			return File(level.levelPreviewImage, "image/png");
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
			string? guid = Request.Query["guid"];
			if (guid == null)
				return BadRequest("No guid provided");

			if (!AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog))
				return BadRequest("Could not load catalog");

			var bundle = catalog.Levels.Where(l => l.Guid == guid).FirstOrDefault();
			if (bundle == null)
				return BadRequest("No such bundle exists in catalog");

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

			if (bundle.Guid != angryFile.angryBundleData.bundleGuid)
				return BadRequest("GUID of the online bundle and the provided angry file does not match");

			if (bundle.Updates.Where(u => u.Hash == angryFile.angryBundleData.buildHash).Any())
				return BadRequest("This level was uploaded to the online catalog before");

			ModifyBundleModel.angryFile = angryFile;
			return Content(JsonConvert.SerializeObject(angryFile.GetBundleInfo()), "application/json");
		}

		public class ModifyLevelBody
		{
			public string guid { get; set; }

			public bool updateThumbnail { get; set; }
			public string? newBundleName { get; set; }
			public string? newBundleAuthor { get; set; }
			public bool? newLocked { get; set; }

			public bool updateExternalURLs { get; set; }
			public string[]? newExternalURLs { get; set; }

			public bool updateAngryFile { get; set; }
			public string? changelog { get; set; }

			public bool[] updateChangelog { get; set; }
			public string?[] newChangelogText { get; set; }
		}

		public IActionResult OnPostModifyBundle([FromBody] ModifyLevelBody info)
		{
			if (info == null)
				return BadRequest("Bad body");

			if (info.guid == null)
				return BadRequest("No guid provided");

			if (!AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog))
				return BadRequest("Failed to load catalog");

			var bundle = catalog.Levels.Where(l => l.Guid == info.guid).FirstOrDefault();
			if (bundle == null)
				return BadRequest("Bundle with given guid does not exist");

			if (info.updateAngryFile && (angryFile == null || angryFile.angryBundleData.bundleGuid != info.guid || bundle.Updates.Where(u => angryFile.angryBundleData.buildHash == u.Hash).Any()))
				return BadRequest("Invalid angry file provided");

			if (info.updateAngryFile && !info.updateExternalURLs)
				return BadRequest("If angry file is updated, external URLs must also be updated");

			if (info.updateThumbnail && thumbnailStream == null)
				return BadRequest("No thumbnail provided");

			// Apply changes

			List<string> commitMessage = new();

			string bundlePath = Path.Combine(ProjectPaths.rootPath, "Levels", info.guid);
			if (!Directory.Exists(bundlePath))
				Directory.CreateDirectory(bundlePath);

			if (info.updateThumbnail)
			{
				thumbnailStream.Position = 0;
				bundle.ThumbnailHash = CryptologyUtils.GetMD5Hash(thumbnailStream);
				thumbnailStream.Position = 0;
				using (FileStream fs = System.IO.File.Open(Path.Combine(bundlePath, "thumbnail.png"), FileMode.OpenOrCreate, FileAccess.Write))
				{
					thumbnailStream.CopyTo(fs);
				}

				commitMessage.Add("- Updated bundle thumbnail");
			}

			if (info.newBundleName != null)
			{
				bundle.Name = info.newBundleName;
				commitMessage.Add("- Updated bundle name");
			}

			if (info.newBundleAuthor != null)
			{
				bundle.Author = info.newBundleAuthor;
				commitMessage.Add("- Updated bundle author");
			}

			if (info.newLocked != null)
			{
				bundle.Locked = (bool) info.newLocked;
				commitMessage.Add((bool) info.newLocked ? "- Locked the bundle" : "- Unlocked the bundle");
			}

			if (info.updateExternalURLs && info.newExternalURLs != null)
			{
				bundle.Parts = info.newExternalURLs.ToList();
				commitMessage.Add("- Updated external URLs");
			}

			if (info.updateAngryFile)
			{
				bundle.Hash = angryFile.angryBundleData.buildHash;
				bundle.LastUpdate = ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds();
				bundle.Size = (int) angryFile.size;
				bundle.Updates.Add(new BundleInfo.UpdateInfo()
				{
					Date = ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds(),
					Hash = angryFile.angryBundleData.buildHash,
					Message = info.changelog
				});

				bundle.Levels = new List<BundleInfo.LevelInfo>();
				foreach (var level in angryFile.rudeLevelData)
				{
					bundle.Levels.Add(new BundleInfo.LevelInfo()
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

				if (!Directory.Exists(Path.Combine(bundlePath, "LevelThumbnails")))
					Directory.CreateDirectory(Path.Combine(bundlePath, "LevelThumbnails"));

				foreach (var level in angryFile.rudeLevelData)
				{
					if (level.levelPreviewImage == null)
						continue;

					using (FileStream fs = System.IO.File.Open(Path.Combine(bundlePath, "LevelThumbnails", $"{CryptologyUtils.GetMD5Hash(level.uniqueIdentifier)}.png"), FileMode.OpenOrCreate, FileAccess.Write))
					{
						fs.Write(level.levelPreviewImage, 0, level.levelPreviewImage.Length);
					}
				}

				commitMessage.Add("- Updated angry file");
			}

			for (int i = 0; i < Math.Min(bundle.Updates.Count, info.updateChangelog.Length); i++)
			{
				if (!info.updateChangelog[i]) continue;

				bundle.Updates[i].Message = info.newChangelogText[i];
				commitMessage.Add($"- Modified changelog of update {i + 1}");
			}

			if (commitMessage.Count == 0)
				return BadRequest("There are no changes to apply");

			AngryCatalogHandler.SaveLevelCatalog();

			GitHandler.Commit($"Modified {bundle.Name}\n" + string.Join("\n", commitMessage));
			return StatusCode(200);
		}
	
		public IActionResult OnPostDeleteBundle()
		{
			string? guid = Request.Query["guid"];
			if (guid == null)
				return BadRequest("No guid provided");

			if (!AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog))
				return BadRequest("Could not get catalog");

			var bundle = catalog.Levels.Where(l => l.Guid == guid).FirstOrDefault();
			if (bundle == null)
				return BadRequest("Bundle with the given guid not found");

			int index = catalog.Levels.IndexOf(bundle);
			if (index == -1)
				return BadRequest("Bundle with the given guid not found");

			catalog.Levels.RemoveAt(index);
			AngryCatalogHandler.SaveLevelCatalog();

			GitHandler.Commit($"Removed {bundle.Name}");
			return StatusCode(200);
		}
	}
}
