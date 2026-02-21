using AssetRipper.IO.Files;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.IO;

namespace AngryCatalogEditor.GUI.Pages
{
    [IgnoreAntiforgeryToken]
    public class AngryCombinerModel : PageModel
    {
        public void OnGet()
        {
        }

        public async Task<IActionResult> OnGetDownload()
        {
            string? urlsString = Request.Query["urls"];
            if (urlsString == null)
                return BadRequest("No urls string");

            string[]? urls = JsonConvert.DeserializeObject<string[]>(urlsString);
            if (urls == null || urls.Length == 0)
                return BadRequest("Invalid urls query");

            string fileName = urls[0];
            int lastPart = fileName.LastIndexOf('/');
            fileName = fileName.Substring(lastPart + 1);
            int extPart = fileName.LastIndexOf('.');
            fileName = fileName.Substring(0, extPart);

			Response.ContentType = "application/octet-stream";
			Response.Headers.ContentDisposition = $"attachment; filename={fileName}.angry";

            await Response.StartAsync();

            Console.WriteLine("Starting...");

            try
            {
                foreach (string url in urls)
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
					    using (var stream = await httpClient.GetStreamAsync(url))
                        {
                            Console.WriteLine($"Downloading from '{url}'");
                            await stream.CopyToAsync(Response.Body);
                            await Response.Body.FlushAsync();
                            Console.WriteLine("Complete");
                        }
				    }
                }
            }
            catch (Exception _)
            {
                Response.HttpContext.Abort();
                return new EmptyResult();
            }

            await Response.CompleteAsync();
			return new EmptyResult();
		}
    }
}
