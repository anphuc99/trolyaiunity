using System;
using System.Collections.Generic;
using UnityEngine;

namespace Share.Utils
{
	/// <summary>
	/// Shared audio URL resolution helpers used by multiple Views and Controllers.
	/// </summary>
	public static class AudioUrlUtils
	{
		/// <summary>
		/// Resolves an audio URL — prepends server base URL if relative.
		/// </summary>
		/// <param name="audioUrl">Raw audio URL from server.</param>
		/// <param name="normalizedServerBaseUrl">Server base URL already normalized (no trailing / or /api).</param>
		/// <returns>Fully resolved absolute URL, or null.</returns>
		public static string ResolveAudioUrl(string audioUrl, string normalizedServerBaseUrl)
		{
			if (string.IsNullOrWhiteSpace(audioUrl))
			{
				return null;
			}

			if (string.IsNullOrWhiteSpace(normalizedServerBaseUrl))
			{
				return null;
			}

			if (!audioUrl.StartsWith("/", StringComparison.Ordinal))
			{
				audioUrl = "/" + audioUrl;
			}

			return normalizedServerBaseUrl + audioUrl;
		}

		/// <summary>
		/// Determines the <see cref="AudioType"/> from a URL extension.
		/// </summary>
		/// <param name="audioUrl">Audio URL.</param>
		/// <returns>Matching AudioType.</returns>
		public static AudioType ResolveAudioType(string audioUrl)
		{
			if (string.IsNullOrWhiteSpace(audioUrl))
			{
				return AudioType.MPEG;
			}

			if (audioUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
			{
				return AudioType.WAV;
			}

			if (audioUrl.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
			{
				return AudioType.OGGVORBIS;
			}

			return AudioType.MPEG;
		}

		/// <summary>
		/// Normalizes the server base URL by trimming trailing slashes and /api suffix.
		/// </summary>
		/// <param name="baseUrl">Raw base URL.</param>
		/// <returns>Normalized base URL without trailing /api, or null.</returns>
		public static string NormalizeServerBaseUrl(string baseUrl)
		{
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return null;
			}

			var normalized = baseUrl.Trim().TrimEnd('/');
			if (normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
			{
				normalized = normalized.Substring(0, normalized.Length - 4);
			}

			return normalized;
		}

		/// <summary>
		/// Builds the TTS query parameter string portion (without base URL / endpoint).
		/// Callers attach this to the full endpoint path.
		/// </summary>
		/// <param name="text">Text to synthesize.</param>
		/// <param name="tone">Tone hint (defaults to neutral).</param>
		/// <param name="characterName">Character name (defaults to Mimi).</param>
		/// <param name="forceReload">Whether to force regeneration.</param>
		/// <returns>Query string starting with '?'.</returns>
		public static string BuildTextToSpeechQuery(string text, string tone, string characterName, bool forceReload = false)
		{
			var queryParts = new List<string>
			{
				"text=" + Uri.EscapeDataString(text ?? string.Empty),
				"tone=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(tone) ? "neutral" : tone),
				"characterName=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(characterName) ? "Mimi" : characterName),
			};

			if (forceReload)
			{
				queryParts.Add("force=true");
			}

			return "?" + string.Join("&", queryParts);
		}
	}
}
