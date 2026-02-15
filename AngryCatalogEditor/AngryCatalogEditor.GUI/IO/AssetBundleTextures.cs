using AssetRipper.Export.Modules.Textures;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;

namespace AngryCatalogEditor.GUI.IO
{
	public static class AssetBundleTextures
	{
		public static byte[]? TryGetBytesFromTexture2D(ITexture2D texture)
		{
			if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
			{
				using MemoryStream stream = new MemoryStream();
				bitmap.Save(stream, ImageExportFormat.Png);
				return stream.ToArray();
			}

			return null;
		}

		public static byte[]? TryGetBytesFromSprite(ISprite sprite)
		{
			SpriteInformationObject infoObj = (SpriteInformationObject)sprite.MainAsset;
			if (infoObj == null)
				return null;

			ITexture2D iconTexture = infoObj.Texture;
			if (iconTexture == null)
				return null;

			if (!iconTexture.CheckAssetIntegrity())
				return null;

			return TryGetBytesFromTexture2D(iconTexture);
		}
	}
}
