using AngryCatalogEditor.GUI.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
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

		public IActionResult OnPostUpdateScript()
		{
			var res = Dialog.OpenFile(out string? path);
			if (res == NativeFileDialogs.Net.NfdStatus.Cancelled)
				return StatusCode(StatusCodes.Status204NoContent);

			if (path == null || !path.EndsWith(".dll"))
				return BadRequest("Bad file path, must have .dll extension");

			string certificatePath = path + ".cert";
			if (!System.IO.File.Exists(certificatePath))
				return BadRequest($"Certificate not found. Make sure that {Path.GetFileName(certificatePath)} exists in the same directory as the script.");

			if (!CryptologyUtils.VerifyCertificate(path, certificatePath))
				return BadRequest("Certificate is invalid");

			if (!AngryCatalogHandler.TryGetScriptCatalog(out ScriptCatalog scriptCatalog))
				return BadRequest("Could not get script catalog");

			// Revert catalog to make sure that only proper changes are done
			GitHandler.HardResetCatalogs();

			MD5 md5 = MD5.Create();
			byte[] hashArr = md5.ComputeHash(System.IO.File.ReadAllBytes(path));
			string scriptHash = Convert.ToHexString(hashArr).ToLower();

			string scriptName = Path.GetFileName(path);
			bool alreadyExists = System.IO.File.Exists(Path.Combine(ProjectPaths.rootPath, "Scripts", scriptName));
			System.IO.File.Copy(path, Path.Combine(ProjectPaths.rootPath, "Scripts", scriptName), true);
			System.IO.File.Copy(certificatePath, Path.Combine(ProjectPaths.rootPath, "Scripts", scriptName + ".cert"), true);

			int size = 0;
			using (FileStream fs = System.IO.File.Open(path, FileMode.Open, FileAccess.Read))
			{
				size = (int)fs.Length;
			}

			ScriptInfo info = scriptCatalog.Scripts.Where(script => script.FileName == scriptName).FirstOrDefault();
			if (info == null)
			{
				info = new ScriptInfo();
				info.Updates = new List<string>();
				scriptCatalog.Scripts.Add(info);
			}

			info.FileName = scriptName;
			info.Hash = scriptHash;
			info.Size = size;
			if (info.Updates == null)
				info.Updates = new List<string>();
			info.Updates.Add(scriptHash);

			AngryCatalogHandler.SaveScriptCatalog();
			GitHandler.Commit($"Updated script {scriptName}");

			return Content(alreadyExists ? "Script successfully updated" : "Script successfully created");
		}
	}
}
