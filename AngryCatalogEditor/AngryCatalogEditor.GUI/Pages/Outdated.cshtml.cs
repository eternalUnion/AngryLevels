using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AngryCatalogEditor.GUI.Pages
{
    public class OutdatedModel : PageModel
    {
        public void OnGet()
        {
        }

        public void OnPost()
        {
            GitHandler.ForceSync();
        }
    }
}
