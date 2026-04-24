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
		private const float DefaultLatinColumnStepEm = 0.45f;
		private const int DefaultPinyinSize = 25;
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

				// Group consecutive non-CJK characters and skip matching pinyin syllables.
				var groupStart = i;
				while (i + 1 < hanText.Length && !IsCjkIdeograph(hanText[i + 1]))
				{
					i++;
				}

				var group = hanText.Substring(groupStart, i - groupStart + 1);
				syllableIndex = SkipLatinPinyinSyllables(syllables, syllableIndex, group);
				builder.Append(group);
			}

			return builder.ToString();
		}

		/// <summary>
		/// Builds a two-line ruby block where pinyin is displayed above Han text and wraps every N Han characters.
		/// Supports **word** vocab markers: characters inside markers are rendered with vocab link tags.
		/// </summary>
		/// <param name="hanText">Base Han text, optionally containing **vocab** markers.</param>
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

			var cleanText = PreprocessVocabMarkers(hanText, out var charVocabMap);

			var safeWrapCount = Math.Max(1, hanWrapCount);
			var blockBuilder = new StringBuilder(cleanText.Length * 16);
			var pinyinLineBuilder = new StringBuilder();
			var hanLineBuilder = new StringBuilder();
			var currentPositionEm = 0f;
			var rowHanCount = 0;
			var syllableIndex = 0;

			for (var i = 0; i < cleanText.Length; i++)
			{
				var ch = cleanText[i];
				if (ch == '\r')
				{
					continue;
				}

				if (ch == '\n')
				{
					FlushRubyRow(blockBuilder, pinyinLineBuilder, hanLineBuilder);
					currentPositionEm = 0f;
					rowHanCount = 0;
					continue;
				}

				if (IsCjkIdeograph(ch))
				{
					// Wrap before this Han character if the row is full.
					if (rowHanCount >= safeWrapCount)
					{
						FlushRubyRow(blockBuilder, pinyinLineBuilder, hanLineBuilder);
						currentPositionEm = 0f;
						rowHanCount = 0;
					}

					if (currentPositionEm > 0f)
					{
						var pos = currentPositionEm.ToString("0.0", CultureInfo.InvariantCulture);
						pinyinLineBuilder.Append("<pos=").Append(pos).Append("em>");
						hanLineBuilder.Append("<pos=").Append(pos).Append("em>");
					}

					var ruby = string.Empty;
					if (syllableIndex < syllables.Length)
					{
						ruby = EscapeTmpText(syllables[syllableIndex]);
						syllableIndex++;
					}

					pinyinLineBuilder.Append("<size=").Append(DefaultPinyinSize).Append('>').Append(ruby).Append("</size>");

					string vocabWord = null;
					if (charVocabMap != null && i < charVocabMap.Length)
					{
						vocabWord = charVocabMap[i];
					}

					if (!string.IsNullOrEmpty(vocabWord))
					{
						hanLineBuilder.Append("<size=").Append(DefaultHanSize).Append("><u><link=\"vocab:")
							.Append(EscapeTmpText(vocabWord)).Append("\">")
							.Append(EscapeTmpText(ch.ToString()))
							.Append("</link></u></size>");
					}
					else
					{
						hanLineBuilder.Append("<size=").Append(DefaultHanSize).Append('>').Append(EscapeTmpText(ch.ToString())).Append("</size>");
					}

					currentPositionEm += DefaultColumnStepEm;
					rowHanCount++;
				}
				else
				{
					// Group consecutive non-CJK, non-newline characters (Latin, digits, punctuation).
					var groupStart = i;
					while (i + 1 < cleanText.Length && !IsCjkIdeograph(cleanText[i + 1]) && cleanText[i + 1] != '\n' && cleanText[i + 1] != '\r')
					{
						i++;
					}

					var groupText = cleanText.Substring(groupStart, i - groupStart + 1);

					// Skip pinyin syllables that correspond to Latin text in this group.
					syllableIndex = SkipLatinPinyinSyllables(syllables, syllableIndex, groupText);

					if (currentPositionEm > 0f)
					{
						var pos = currentPositionEm.ToString("0.0", CultureInfo.InvariantCulture);
						pinyinLineBuilder.Append("<pos=").Append(pos).Append("em>");
						hanLineBuilder.Append("<pos=").Append(pos).Append("em>");
					}

					// No pinyin above non-CJK text.
					pinyinLineBuilder.Append("<size=").Append(DefaultPinyinSize).Append("></size>");

					// Check if the group carries a vocab marker.
					string groupVocabWord = null;
					if (charVocabMap != null)
					{
						for (var j = groupStart; j <= i && j < charVocabMap.Length; j++)
						{
							if (!string.IsNullOrEmpty(charVocabMap[j]))
							{
								groupVocabWord = charVocabMap[j];
								break;
							}
						}
					}

					if (!string.IsNullOrEmpty(groupVocabWord))
					{
						hanLineBuilder.Append("<size=").Append(DefaultHanSize).Append("><u><link=\"vocab:")
							.Append(EscapeTmpText(groupVocabWord)).Append("\">")
							.Append(EscapeTmpText(groupText))
							.Append("</link></u></size>");
					}
					else
					{
						hanLineBuilder.Append("<size=").Append(DefaultHanSize).Append('>').Append(EscapeTmpText(groupText)).Append("</size>");
					}

					currentPositionEm += EstimateNonHanGroupWidth(groupText);
				}
			}

			FlushRubyRow(blockBuilder, pinyinLineBuilder, hanLineBuilder);
			return blockBuilder.ToString();
		}

		/// <summary>
		/// Strips **vocab** markers from input text and builds a parallel map of vocab words per character.
		/// </summary>
		/// <param name="text">Input text possibly containing **word** markers.</param>
		/// <param name="charVocabMap">Output array parallel to the cleaned text. Each element is the full vocab word or null.</param>
		/// <returns>Cleaned text without ** markers.</returns>
		private static string PreprocessVocabMarkers(string text, out string[] charVocabMap)
		{
			charVocabMap = null;
			if (string.IsNullOrEmpty(text) || !text.Contains("**"))
			{
				return text;
			}

			var clean = new StringBuilder(text.Length);
			var vocabMap = new List<string>();
			var inVocab = false;
			var currentVocabChars = new StringBuilder();

			for (var i = 0; i < text.Length; i++)
			{
				if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
				{
					if (inVocab)
					{
						var word = currentVocabChars.ToString();
						var startIdx = vocabMap.Count - word.Length;
						for (var j = startIdx; j < vocabMap.Count; j++)
						{
							vocabMap[j] = word;
						}

						currentVocabChars.Clear();
					}

					inVocab = !inVocab;
					i++;
					continue;
				}

				clean.Append(text[i]);
				vocabMap.Add(null);

				if (inVocab)
				{
					currentVocabChars.Append(text[i]);
				}
			}

			charVocabMap = vocabMap.ToArray();
			return clean.ToString();
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

		/// <summary>
		/// Estimates the column width of a group of non-CJK characters.
		/// Full-width punctuation is counted at Han-character width; narrow characters at a smaller step.
		/// </summary>
		private static float EstimateNonHanGroupWidth(string text)
		{
			var width = 0f;
			for (var i = 0; i < text.Length; i++)
			{
				width += IsFullWidthNonHanChar(text[i]) ? DefaultColumnStepEm : DefaultLatinColumnStepEm;
			}

			return width;
		}

		/// <summary>
		/// Returns true for full-width non-ideograph characters such as CJK punctuation.
		/// </summary>
		private static bool IsFullWidthNonHanChar(char ch)
		{
			// CJK Symbols and Punctuation
			if (ch >= 0x3000 && ch <= 0x303F)
			{
				return true;
			}

			// Fullwidth ASCII variants
			if (ch >= 0xFF01 && ch <= 0xFF60)
			{
				return true;
			}

			// CJK Compatibility Forms
			if (ch >= 0xFE30 && ch <= 0xFE4F)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Advances syllableIndex past pinyin syllables that correspond to Latin letters
		/// in a non-CJK text group (e.g., a name like "Hugh" echoed in the pinyin stream).
		/// </summary>
		private static int SkipLatinPinyinSyllables(string[] syllables, int syllableIndex, string nonCjkGroup)
		{
			// Extract only Latin letters from the group.
			var latinBuilder = new StringBuilder();
			for (var i = 0; i < nonCjkGroup.Length; i++)
			{
				if (IsLatinLetter(nonCjkGroup[i]))
				{
					latinBuilder.Append(nonCjkGroup[i]);
				}
			}

			if (latinBuilder.Length == 0)
			{
				return syllableIndex;
			}

			var latinText = latinBuilder.ToString();

			// Try to match consecutive syllables against the Latin text.
			var matched = new StringBuilder();
			var tempIndex = syllableIndex;
			while (tempIndex < syllables.Length)
			{
				matched.Append(syllables[tempIndex]);
				tempIndex++;

				if (string.Equals(matched.ToString(), latinText, StringComparison.OrdinalIgnoreCase))
				{
					return tempIndex;
				}

				if (matched.Length >= latinText.Length)
				{
					break;
				}
			}

			return syllableIndex;
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