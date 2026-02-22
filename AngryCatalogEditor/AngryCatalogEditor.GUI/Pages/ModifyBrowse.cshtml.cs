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
    public class ModifyBrowseModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
