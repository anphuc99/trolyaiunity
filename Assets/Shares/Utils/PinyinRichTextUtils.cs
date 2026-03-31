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
		private const float DefaultColumnStepEm = 0.8f;
		private const int DefaultPinyinSize = 15;
		private const int DefaultHanSize = 50;

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

		/// <summary>
		/// Builds a two-line ruby block where pinyin is displayed above Han text and wraps every N Han characters.
		/// </summary>
		/// <param name="hanText">Base Han text.</param>
		/// <param name="pinyin">Pinyin text.</param>
		/// <param name="hanWrapCount">Maximum Han characters per row before wrapping.</param>
		/// <returns>TMP rich text with pinyin and Han rows.</returns>
		public static string BuildWrappedInlineRuby(string hanText, string pinyin, int hanWrapCount)
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

			var safeWrapCount = Math.Max(1, hanWrapCount);
			var blockBuilder = new StringBuilder(hanText.Length * 16);
			var pinyinLineBuilder = new StringBuilder();
			var hanLineBuilder = new StringBuilder();
			var rowColumnCount = 0;
			var rowHanCount = 0;
			var syllableIndex = 0;

			for (var i = 0; i < hanText.Length; i++)
			{
				var ch = hanText[i];
				if (ch == '\r')
				{
					continue;
				}

				if (ch == '\n')
				{
					FlushRubyRow(blockBuilder, pinyinLineBuilder, hanLineBuilder);
					rowColumnCount = 0;
					rowHanCount = 0;
					continue;
				}

				var isHan = IsCjkIdeograph(ch);
				if (isHan && rowHanCount >= safeWrapCount)
				{
					FlushRubyRow(blockBuilder, pinyinLineBuilder, hanLineBuilder);
					rowColumnCount = 0;
					rowHanCount = 0;
				}

				if (rowColumnCount > 0)
				{
					var pos = (rowColumnCount * DefaultColumnStepEm).ToString("0.0", CultureInfo.InvariantCulture);
					pinyinLineBuilder.Append("<pos=").Append(pos).Append("em>");
					hanLineBuilder.Append("<pos=").Append(pos).Append("em>");
				}

				var ruby = string.Empty;
				if (isHan && syllableIndex < syllables.Length)
				{
					ruby = EscapeTmpText(syllables[syllableIndex]);
					syllableIndex++;
				}

				pinyinLineBuilder.Append("<size=").Append(DefaultPinyinSize).Append('>').Append(ruby).Append("</size>");
				hanLineBuilder.Append("<size=").Append(DefaultHanSize).Append('>').Append(EscapeTmpText(ch.ToString())).Append("</size>");

				rowColumnCount++;
				if (isHan)
				{
					rowHanCount++;
				}
			}

			FlushRubyRow(blockBuilder, pinyinLineBuilder, hanLineBuilder);
			return blockBuilder.ToString();
		}

		private static void FlushRubyRow(StringBuilder output, StringBuilder pinyinLine, StringBuilder hanLine)
		{
			if (pinyinLine.Length == 0 && hanLine.Length == 0)
			{
				return;
			}

			if (output.Length > 0)
			{
				output.Append('\n');
			}

			output.Append(pinyinLine).Append('\n').Append(hanLine);
			pinyinLine.Clear();
			hanLine.Clear();
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