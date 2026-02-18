using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Encodings.Web;

namespace AngryCatalogEditor.GUI.Pages
{
	public class IndexModel : PageModel
	{
		private readonly ILogger<IndexModel> _logger;

		public IndexModel(ILogger<IndexModel> logger)
		{
			_logger = logger;
		}

		public IActionResult OnGet()
		{
			if (GitHandler.username == null || GitHandler.email == null)
			{
				string? username = Request.Cookies["username"];
				string? email = Request.Cookies["email"];

				if (username == null || email == null)
					return Redirect(QueryHelpers.AddQueryString("/Authorize", "redirect", "/"));

				GitHandler.username = username;
				GitHandler.email = email;
			}

			if (!GitHandler.Synced())
			{
				return Redirect("/Outdated");
			}

			return Page();
		}
	}
}
