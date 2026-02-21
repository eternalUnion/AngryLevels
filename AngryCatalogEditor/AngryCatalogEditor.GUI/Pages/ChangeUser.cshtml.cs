using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AngryCatalogEditor.GUI.Pages
{
    public class ChangeUserModel : PageModel
    {
        public IActionResult OnGet()
        {
            GitHandler.username = null;
            GitHandler.email = null;
            Response.Cookies.Delete("username");
			Response.Cookies.Delete("email");
			return Redirect("/");
        }
    }
}
