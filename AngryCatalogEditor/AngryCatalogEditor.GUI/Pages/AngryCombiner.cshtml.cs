using AssetRipper.IO.Files;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace AngryCatalogEditor.GUI.Pages
{
    [IgnoreAntiforgeryToken]
    public class AngryCombinerModel : PageModel
    {
        private static Regex urlRegex = new Regex(@"(https?:\/\/)?raw\.githubusercontent\.com\/.*\.angry\d*");

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnGetDownload()
        {
            string? urlsString = Request.Query["urls"];
            if (urlsString == null)
                return BadRequest("No urls string");

            string[]? urls;
			try
            {
                urls = JsonConvert.DeserializeObject<string[]>(urlsString);
                if (urls == null || urls.Length == 0)
                    return BadRequest("Missing or empty urls query");
            }
            catch (Exception)
            {
                return BadRequest("Invalid urls query");
            }

            if (urls.Where(u => u == null || !urlRegex.IsMatch(u)).Any())
                    return BadRequest("At least one url does not point to an angry file");

            string fileName = urls[0];
            int lastPart = fileName.LastIndexOf('/');
            fileName = fileName.Substring(lastPart + 1);
            int extPart = fileName.LastIndexOf('.');
            fileName = fileName.Substring(0, extPart);

			Response.ContentType = "application/octet-stream";
			Response.Headers.ContentDisposition = $"attachment; filename={fileName}.angry";

            await Response.StartAsync();

            Console.WriteLine("Starting download...");
            Request.HttpContext.RequestAborted.ThrowIfCancellationRequested();

			try
            {
                foreach (string url in urls)
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
					    using (var stream = await httpClient.GetStreamAsync(url))
                        {
                            Console.WriteLine($"Downloading from '{url}'");
                            await stream.CopyToAsync(Response.Body, Request.HttpContext.RequestAborted);
                            await Response.Body.FlushAsync(Request.HttpContext.RequestAborted);
                            Console.WriteLine("Complete");
                        }
				    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Download cancelled by the client");
				return new EmptyResult();
			}

            await Response.CompleteAsync();
			return new EmptyResult();
		}
    }
}
