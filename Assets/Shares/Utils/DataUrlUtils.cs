using System;
using System.IO;

namespace Share.Utils
{
	/// <summary>
	/// Shared data-URL building helpers.
	/// </summary>
	public static class DataUrlUtils
	{
		/// <summary>
		/// Builds a base64-encoded data URL from image file bytes.
		/// </summary>
		/// <param name="filePath">Original file path (used to determine MIME type from extension).</param>
		/// <param name="bytes">Raw image bytes.</param>
		/// <returns>Data URL string, or null if MIME type is unsupported.</returns>
		public static string BuildImageDataUrl(string filePath, byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0 || string.IsNullOrWhiteSpace(filePath))
			{
				return null;
			}

			var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
			var mime = extension == ".png"
				? "image/png"
				: extension == ".webp"
					? "image/webp"
					: extension == ".jpg" || extension == ".jpeg"
						? "image/jpeg"
						: null;

			if (string.IsNullOrWhiteSpace(mime))
			{
				return null;
			}

			return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
		}
	}
}
