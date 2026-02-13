using ImageMagick;

namespace AngryCatalogEditor.GUI
{
	public static class ImageUtils
	{
		public static void ResizeToMinimum(MagickImage img, int width, int height)
		{
			float targetAspect = (float)width / (float)height;
			float imgAspect = (float)img.Width / (float)img.Height;

			// Too wide
			if (imgAspect > targetAspect)
			{
				img.Crop((uint)(img.Height * targetAspect), img.Height, Gravity.Center);
			}
			// Too tall
			if (imgAspect < targetAspect)
			{
				img.Crop(img.Width, (uint)(img.Width * (1f / targetAspect)), Gravity.Center);
			}

			if (img.Width > width)
				img.Resize((uint)width, 0);
		}
	}
}
