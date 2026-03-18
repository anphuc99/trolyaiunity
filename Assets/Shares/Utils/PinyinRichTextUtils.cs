using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Share.Utils
{
	/// <summary>
	/// Builds TMP rich text that places pinyin above each Han character.
	/// </summary>
	public static class PinyinRichTextUtils
	{
		private const string Prefix = "<voffset=1em><size=50%>";
		private const string Suffix = "</size></voffset>";

		/// <summary>
		/// Converts plain Han text + pinyin into inline ruby-like TMP text.
		/// Example: nǐ hǎo + 你好 -> &lt;voffset=1em&gt;&lt;size=50%&gt;nǐ&lt;/size&gt;&lt;/voffset&gt;你 ...
		/// </summary>
		/// <param name="hanText">Base Han text.</param>
		/// <param name="pinyin">Pinyin text; punctuation/separators are tolerated.</param>
		/// <returns>Formatted TMP rich text. Falls back to hanText when pinyin is missing.</returns>
		public static string BuildInlineRuby(string hanText, string pinyin)
		{
			if (string.IsNullOrWhiteSpace(hanText))
			{
				return string.Empty;
			}

			if (string.IsNullOrWhiteSpace(pinyin))
			{
				return hanText;
			}

			var syllables = ExtractPinyinSyllables(pinyin);
			if (syllables.Length == 0)
			{
				return hanText;
			}

			var builder = new StringBuilder(hanText.Length * 8);
			var syllableIndex = 0;

			for (var i = 0; i < hanText.Length; i++)
			{
				var ch = hanText[i];
				if (IsCjkIdeograph(ch) && syllableIndex < syllables.Length)
				{
					builder.Append(Prefix);
					builder.Append(EscapeTmpText(syllables[syllableIndex]));
					builder.Append(Suffix);
					builder.Append(ch);
					syllableIndex++;
					continue;
				}

				builder.Append(ch);
			}

			return builder.ToString();
		}

		private static string[] ExtractPinyinSyllables(string pinyin)
		{
			var result = new List<string>();
			var current = new StringBuilder();

			for (var i = 0; i < pinyin.Length; i++)
			{
				var ch = pinyin[i];
				if (IsPinyinSyllableChar(ch))
				{
					current.Append(ch);
					continue;
				}

				if (current.Length > 0)
				{
					result.Add(current.ToString());
					current.Clear();
				}

				if (IsPinyinSeparator(ch))
				{
					continue;
				}
			}

			if (current.Length > 0)
			{
				result.Add(current.ToString());
			}

			return result.ToArray();
		}

		private static bool IsPinyinSyllableChar(char ch)
		{
			if (char.IsDigit(ch))
			{
				return true;
			}

			if (IsLatinLetter(ch))
			{
				return true;
			}

			var category = CharUnicodeInfo.GetUnicodeCategory(ch);
			return category == UnicodeCategory.NonSpacingMark
				|| category == UnicodeCategory.SpacingCombiningMark
				|| category == UnicodeCategory.EnclosingMark;
		}

		private static bool IsLatinLetter(char ch)
		{
			if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
			{
				return true;
			}

			return (ch >= 0x00C0 && ch <= 0x024F)
				|| (ch >= 0x1E00 && ch <= 0x1EFF);
		}

		private static bool IsPinyinSeparator(char ch)
		{
			return ch == '\'' || ch == '’' || ch == '-' || ch == '·';
		}

		private static bool IsCjkIdeograph(char ch)
		{
			return (ch >= 0x3400 && ch <= 0x4DBF)
				|| (ch >= 0x4E00 && ch <= 0x9FFF)
				|| (ch >= 0xF900 && ch <= 0xFAFF);
		}

		private static string EscapeTmpText(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			return value
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;");
		}
	}
}