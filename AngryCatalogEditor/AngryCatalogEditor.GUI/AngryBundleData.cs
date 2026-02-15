using System.ComponentModel;

namespace AngryCatalogEditor.GUI
{
	public class AngryBundleData
	{
		public string bundleName { get; set; }
		public string bundleAuthor { get; set; }
		[DefaultValue(2)]
		public int bundleVersion { get; set; }
		public string bundleGuid { get; set; }
		public string buildHash { get; set; }
		public string bundleDataPath { get; set; }
		public List<string> levelDataPaths;
	}
}
