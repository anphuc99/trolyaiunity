using System;
using UnityEngine;

namespace Share.Utils
{
	/// <summary>
	/// Helpers to compress image bytes.
	/// </summary>
	public static class ImageCompressionUtils
	{
		/// <summary>
		/// Compresses an image to be under the specified size in bytes.
		/// Converts the image to JPEG if compression is needed.
		/// </summary>
		public static byte[] CompressImageUnderSize(byte[] originalBytes, int maxBytes, out bool changedToJpeg)
		{
			changedToJpeg = false;

			if (originalBytes == null || originalBytes.Length == 0)
			{
				return originalBytes;
			}

			if (originalBytes.Length <= maxBytes)
			{
				return originalBytes;
			}

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!texture.LoadImage(originalBytes))
			{
				UnityEngine.Object.Destroy(texture);
				return originalBytes; // Cannot parse, return original
			}

			changedToJpeg = true;
			int quality = 90;
			byte[] compressedBytes = texture.EncodeToJPG(quality);

			while (compressedBytes.Length > maxBytes && quality > 10)
			{
				quality -= 10;

				if (quality <= 30)
				{
					// Time to resize
					int newWidth = Mathf.Max(1, texture.width / 2);
					int newHeight = Mathf.Max(1, texture.height / 2);
					var resized = ResizeTexture(texture, newWidth, newHeight);
					UnityEngine.Object.Destroy(texture);
					texture = resized;
					quality = 80; // Reset quality for the new size
				}

				compressedBytes = texture.EncodeToJPG(quality);
			}

			UnityEngine.Object.Destroy(texture);
			return compressedBytes;
		}

		private static Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
		{
			var rt = RenderTexture.GetTemporary(newWidth, newHeight);
			RenderTexture.active = rt;
			Graphics.Blit(source, rt);
			var result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
			result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
			result.Apply();
			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(rt);
			return result;
		}
	}
}
