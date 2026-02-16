namespace AngryCatalogEditor.GUI.IO
{
	public static class IOUtils
	{
		private static readonly string[] prefixes
			= new string[] { "B", "KB", "MB", "GB", "TB", "PB" };

		public static string SizeToText(long bytes)
		{
			int idx = 0;

			long whole = bytes;
			long remainder = 0;
			long prefixSize = 1;

			while (whole > 1024 && idx < prefixes.Length - 1)
			{
				remainder += (whole % 1024) * prefixSize;
				whole /= 1024;
				prefixSize *= 1024;
				idx++;
			}

			return (remainder == 0) ? $"{whole} {prefixes[idx]}" : $"{(whole + (double)remainder / prefixSize):0.00} {prefixes[idx]}";
		}
	}
}
