using NativeFileDialogs.Net;

namespace AngryCatalogEditor.GUI
{
	public static class Dialog
	{
		private static object _lock = new object();

		public static NfdStatus OpenFile(out string? path)
		{
			lock (_lock)
			{
				return Nfd.OpenDialog(out path);
			}
		}
	}
}
