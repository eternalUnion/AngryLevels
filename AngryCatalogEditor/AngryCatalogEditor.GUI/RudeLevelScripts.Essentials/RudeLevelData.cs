using AngryCatalogEditor.GUI.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_213;

namespace AngryCatalogEditor.GUI.RudeLevelScripts.Essentials
{
	public class RudeLevelData
	{
		public string[] requiredDllNames;
		public string scenePath = "";
		public string uniqueIdentifier = "";
		public string levelName = "";
		public bool isSecretLevel = false;
		public int prefferedLevelOrder = 0;
		public byte[]? levelPreviewImage = null;
		public bool hideIfNotPlayed = false;
		public string[] requiredCompletedLevelIdsForUnlock;
		public bool levelChallengeEnabled = false;
		public string levelChallengeText = "";
		public int secretCount = 0;

		public RudeLevelData(IMonoBehaviour obj, GameData gameData)
		{
			SerializableValue[] fields = (obj.Structure as SerializableStructure).Fields;

			requiredDllNames = fields[1].AsStringArray;
			scenePath = fields[2].AsString;
			uniqueIdentifier = fields[3].AsString;
			levelName = fields[4].AsString;
			isSecretLevel = fields[5].AsBoolean;
			prefferedLevelOrder = fields[6].AsInt32;
			hideIfNotPlayed = fields[8].AsBoolean;
			requiredCompletedLevelIdsForUnlock = fields[9].AsStringArray;
			levelChallengeEnabled = fields[10].AsBoolean;
			levelChallengeText = fields[11].AsString;
			secretCount = fields[12].AsInt32;

			IPPtr levelIconPtr = fields[7].AsPPtr;
			if (levelIconPtr.PathID == 0)
				return;

			ISprite? iconSprite = null;
			foreach (var bundle in gameData.GameBundle.Bundles)
			{
				foreach (var collection in bundle.Collections)
				{
					var iconSpritePair = collection.Assets.Where(a => a.Key == levelIconPtr.PathID).FirstOrDefault();
					if (iconSpritePair.Key == levelIconPtr.PathID)
					{
						iconSprite = (ISprite)iconSpritePair.Value;
						break;
					}
				}
			}

			if (iconSprite == null)
				return;

			levelPreviewImage = AssetBundleTextures.TryGetBytesFromSprite(iconSprite);
		}
	}
}
