using AngryCatalogEditor.GUI.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Processing;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;
using System.IO;

namespace AngryCatalogEditor.GUI.RudeLevelScripts.Essentials
{
	public class RudeBundleData
	{
		public string bundleName;
		public string author;
		public byte[]? levelIcon;

		public RudeBundleData(IMonoBehaviour obj, GameData gameData)
		{
			SerializableValue[] fields = (obj.Structure as SerializableStructure).Fields;

			bundleName = fields[0].AsString;
			author = fields[1].AsString;

			IPPtr iconPtr = fields[2].AsPPtr;
			if (iconPtr.PathID == 0)
				return;

			ISprite? iconSprite = null;
			foreach (var bundle in gameData.GameBundle.Bundles)
			{
				foreach (var collection in bundle.Collections)
				{
					var iconSpritePair = collection.Assets.Where(a => a.Key == iconPtr.PathID).FirstOrDefault();
					if (iconSpritePair.Key == iconPtr.PathID)
					{
						iconSprite = (ISprite) iconSpritePair.Value;
						break;
					}
				}
			}

			if (iconSprite == null)
				return;

			levelIcon = AssetBundleTextures.TryGetBytesFromSprite(iconSprite);
		}
	}
}
