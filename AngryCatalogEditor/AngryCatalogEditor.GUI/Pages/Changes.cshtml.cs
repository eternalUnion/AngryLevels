using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AngryCatalogEditor.GUI.Pages
{
    [IgnoreAntiforgeryToken]
    public class ChangesModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (!GitHandler.Synced())
                return Redirect("/Outdated");

            return Page();
        }

        public IActionResult OnPostPush()
        {
            if (!GitHandler.Synced())
                return BadRequest("Remote repository is ahead of the local repository");

            if (AuthorizeModel.Token == null)
                return BadRequest("User is not logged in");

            if (GitHandler.NumberOfChanges <= 0)
                return BadRequest("No pending changes");

            if (GitHandler.Push())
                return BadRequest("Failed to push");
            else
                return StatusCode(200);
        }
    }
}
