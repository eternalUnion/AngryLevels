using AngryCatalogEditor.GUI.IO;
using AssetRipper.Addressables;
using AssetRipper.Import.Structure;
using AssetRipper.Processing;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NativeFileDialogs.Net;
using Newtonsoft.Json;
using SharpCompress.Compressors.Xz;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;

namespace AngryCatalogEditor.GUI.Pages
{
    public class ModifyLevelModel : PageModel
    {
        public void OnGet()
        {
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
				Console.WriteLine(ex.StackTrace);
				Console.ForegroundColor = ConsoleColor.White;
				return BadRequest("Internal exception, see console");
			}

			return StatusCode(200);
		}
    }
}
