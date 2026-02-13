using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NativeFileDialogs.Net;

namespace AngryCatalogEditor.GUI.Pages
{
	[IgnoreAntiforgeryToken]
	public class AddLevelModel : PageModel
    {
		private static MemoryStream? thumbnailStream = null;

		public void OnGet()
        {
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
    }
}
