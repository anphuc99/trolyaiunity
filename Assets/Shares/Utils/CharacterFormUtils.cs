using System;

namespace Share.Utils
{
	/// <summary>
	/// Shared character form helpers used by Create/Edit character views.
	/// </summary>
	public static class CharacterFormUtils
	{
		/// <summary>
		/// Normalizes a gender string to "male" or "female".
		/// </summary>
		/// <param name="raw">Raw input string.</param>
		/// <returns>"male", "female", or null if unrecognized.</returns>
		public static string NormalizeGender(string raw)
		{
			var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();

			if (normalized.Contains("female") || normalized.Contains("nữ") || normalized == "nu")
			{
				return "female";
			}

			if (normalized.Contains("male") || normalized.Contains("nam"))
			{
				return "male";
			}

			return null;
		}

		/// <summary>
		/// Trims and returns a string, or null if it is empty/whitespace.
		/// </summary>
		/// <param name="value">Input string.</param>
		/// <returns>Trimmed non-empty string, or null.</returns>
		public static string ToNullableString(string value)
		{
			var trimmed = (value ?? string.Empty).Trim();
			return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
		}
	}
}
