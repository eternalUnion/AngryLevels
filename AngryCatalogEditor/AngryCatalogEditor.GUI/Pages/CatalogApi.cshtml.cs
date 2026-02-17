using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;

namespace AngryCatalogEditor.GUI.Pages
{
    public class CatalogApiModel : PageModel
    {
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



		public IActionResult OnGet()
        {
            return BadRequest("No handler specified.");
        }

        public IActionResult OnGetCatalog()
        {
            if (!AngryCatalogHandler.TryGetCatalog(out LevelCatalog catalog))
                return BadRequest("Catalog not found.");

            return Content(JsonConvert.SerializeObject(catalog), "application/json");
		}

        public IActionResult OnGetThumbnail()
        {
            string? guid = Request.Query["guid"];
            if (string.IsNullOrEmpty(guid))
                return BadRequest("Missing GUID.");

			if (!IsGUID(guid))
				return BadRequest("Invalid GUID.");

			string? rootDir = ProjectPaths.rootPath;
			if (rootDir == null)
				return BadRequest("Could not locate project root.");

			return File(System.IO.File.Open(Path.Combine(rootDir, "Levels", guid, "thumbnail.png"), FileMode.Open, FileAccess.Read), "image/png");
        }
    }
}
