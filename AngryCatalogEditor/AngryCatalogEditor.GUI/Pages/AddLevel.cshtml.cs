using AngryCatalogEditor.GUI.IO;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NativeFileDialogs.Net;
using Newtonsoft.Json;

namespace AngryCatalogEditor.GUI.Pages
{
	[IgnoreAntiforgeryToken]
	public class AddLevelModel : PageModel
    {
		private static MemoryStream? thumbnailStream = null;
		private static AngryFile? angryFile = null;

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

			AddLevelModel.angryFile = angryFile;
			return Content(JsonConvert.SerializeObject(angryFile.GetBundleInfo()), "application/json");
		}
	}
}
