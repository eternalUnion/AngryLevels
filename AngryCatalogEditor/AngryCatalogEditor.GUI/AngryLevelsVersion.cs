using Newtonsoft.Json;

namespace AngryCatalogEditor.GUI
{
	public class AngryLevelsVersion
	{
		public string Version { get; set; }

		[JsonIgnore]
		public Version VersionObj => new Version(Version);

		public string Name { get; set; }
	}
}
