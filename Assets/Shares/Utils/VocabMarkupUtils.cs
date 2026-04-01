using System.Text.RegularExpressions;

namespace Share.Utils
{
	/// <summary>
	/// Shared utilities for converting **word** vocabulary markup in chat messages
	/// to TMP Pro rich text, and for stripping the markup for TTS/plain-text use.
	/// </summary>
	public static class VocabMarkupUtils
	{
		/// <summary>
		/// Regex to match **word** vocabulary markup.
		/// </summary>
		private static readonly Regex VocabMarkupRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

		/// <summary>
		/// Converts **word** markup to TMP rich text with underline and link tags.
		/// Example: 我**爱**你 → 我&lt;u&gt;&lt;link="vocab:爱"&gt;爱&lt;/link&gt;&lt;/u&gt;你
		/// </summary>
		/// <param name="text">Raw text with ** markup.</param>
		/// <returns>TMP-compatible rich text string.</returns>
		public static string ConvertToRichText(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			return VocabMarkupRegex.Replace(text, match =>
			{
				var word = match.Groups[1].Value;
				return "<u><link=\"vocab:" + word + "\">" + word + "</link></u>";
			});
		}

		/// <summary>
		/// Strips **word** markup from text, keeping only the word itself.
		/// Used for TTS to ensure clean pronunciation.
		/// </summary>
		/// <param name="text">Raw text with ** markup.</param>
		/// <returns>Clean text with ** markers removed.</returns>
		public static string StripMarkup(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			return VocabMarkupRegex.Replace(text, "$1");
		}

		/// <summary>
		/// Returns true when the text contains at least one **word** markup pattern.
		/// </summary>
		/// <param name="text">Text to check.</param>
		/// <returns>True if at least one vocab markup is found.</returns>
		public static bool HasVocabMarkup(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			return VocabMarkupRegex.IsMatch(text);
		}
	}
}
