using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Share.Components
{
    /// <summary>
    /// Supported input languages for <see cref="MultiLanguageInputField"/>.
    /// </summary>
    public enum InputLanguage
    {
        English,
        Vietnamese,
        Korean
    }

    /// <summary>
    /// A <see cref="TMP_InputField"/> subclass that adds Telex Vietnamese input,
    /// Korean Hangul composition, and Right-Alt language switching.
    /// <para>
    /// - English mode: passthrough, no transformation.
    /// - Vietnamese mode: Telex rules (dd→đ, aa→â, tone marks s/f/r/x/j, etc.).
    /// - Korean mode: Latin keys mapped to Jamo, auto-composed into syllable blocks.
    /// </para>
    /// </summary>
    public class MultiLanguageInputField : TMP_InputField
    {
        [Header("Multi-Language Settings")]
        [SerializeField]
        private Notification _notification;

        [SerializeField]
        private InputLanguage _currentLanguage = InputLanguage.English;

        /// <summary>Current active input language.</summary>
        public InputLanguage CurrentLanguage => _currentLanguage;

        // ──────────────────────── Korean composition state ────────────────────────
        /// <summary>Tracks in-progress Hangul syllable composition.</summary>
        private enum HangulState { Empty, Choseong, Jungseong, Jongseong }

        private HangulState _hangulState = HangulState.Empty;
        private int _cho = -1;   // choseong index
        private int _jung = -1;  // jungseong index
        private int _jong = -1;  // jongseong index

        // ──────────────────────── Lifecycle ────────────────────────

        protected override void Awake()
        {
            base.Awake();
            onValidateInput += ValidateMultiLangInput;
        }

        protected override void OnDestroy()
        {
            onValidateInput -= ValidateMultiLangInput;
            base.OnDestroy();
        }

        private void Update()
        {
            // Right-Alt toggles language (AltGr / RightAlt)
            if (Input.GetKeyDown(KeyCode.RightAlt))
            {
                CycleLanguage();
            }
        }

        // ──────────────────────── Language cycling ────────────────────────

        /// <summary>
        /// Cycles through English → Vietnamese → Korean → English.
        /// Shows a notification with the new language label.
        /// </summary>
        private void CycleLanguage()
        {
            // Finalize any in-progress Korean composition before switching
            FinalizeHangul();

            switch (_currentLanguage)
            {
                case InputLanguage.English:
                    _currentLanguage = InputLanguage.Vietnamese;
                    break;
                case InputLanguage.Vietnamese:
                    _currentLanguage = InputLanguage.Korean;
                    break;
                case InputLanguage.Korean:
                    _currentLanguage = InputLanguage.English;
                    break;
            }

            var label = GetLanguageLabel(_currentLanguage);
            if (!TryResolveNotification())
            {
                Debug.LogWarning("[MultiLanguageInputField] Notification reference is missing.", this);
                return;
            }

            _notification.ShowNotification(label, 1f);
        }

        /// <summary>
        /// Resolves the Notification reference, including inactive scene objects.
        /// </summary>
        private bool TryResolveNotification()
        {
            if (_notification != null)
            {
                return true;
            }

            _notification = Object.FindFirstObjectByType<Notification>(FindObjectsInactive.Include);
            return _notification != null;
        }

        /// <summary>Returns the short label for a given language.</summary>
        private static string GetLanguageLabel(InputLanguage lang)
        {
            switch (lang)
            {
                case InputLanguage.Vietnamese: return "VI";
                case InputLanguage.Korean: return "KO";
                default: return "ENG";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  INPUT VALIDATION (per-character hook)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by TMP_InputField for each character typed.
        /// Returns '\0' to reject the character (we handle insertion manually).
        /// </summary>
        private char ValidateMultiLangInput(string currentText, int charIndex, char addedChar)
        {
            switch (_currentLanguage)
            {
                case InputLanguage.Vietnamese:
                    HandleVietnameseChar(addedChar);
                    return '\0'; // we set text manually
                case InputLanguage.Korean:
                    HandleKoreanChar(addedChar);
                    return '\0';
                default:
                    return addedChar; // English passthrough
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  VIETNAMESE TELEX
        // ══════════════════════════════════════════════════════════════════

        #region Vietnamese Telex

        // Telex double-key mappings (must be lowercase keys)
        private static readonly Dictionary<char, char> TelexDoubleMap = new Dictionary<char, char>
        {
            { 'a', '\u00e2' }, // aa → â
            { 'e', '\u00ea' }, // ee → ê
            { 'o', '\u00f4' }, // oo → ô
            { 'd', '\u0111' }, // dd → đ
        };

        // Telex w-mappings: vowel + w
        private static readonly Dictionary<char, char> TelexWMap = new Dictionary<char, char>
        {
            { 'a', '\u0103' }, // aw → ă
            { 'o', '\u01a1' }, // ow → ơ
            { 'u', '\u01b0' }, // uw → ư
        };

        // Tone marks applied to the main vowel
        // s = sắc, f = huyền, r = hỏi, x = ngã, j = nặng
        private static readonly Dictionary<char, int> TelexToneMap = new Dictionary<char, int>
        {
            { 's', 1 }, // sắc  (acute)
            { 'f', 2 }, // huyền (grave)
            { 'r', 3 }, // hỏi  (hook above)
            { 'x', 4 }, // ngã  (tilde)
            { 'j', 5 }, // nặng (dot below)
        };

        // Base vowels grouped by their tonal variants:
        // index 0 = no tone, 1 = sắc, 2 = huyền, 3 = hỏi, 4 = ngã, 5 = nặng
        private static readonly Dictionary<char, string> VowelToneTable = new Dictionary<char, string>
        {
            { 'a',  "a\u00e1\u00e0\u1ea3\u00e3\u1ea1" },
            { '\u00e2', "\u00e2\u1ea5\u1ea7\u1ea9\u1eab\u1ead" },  // â
            { '\u0103', "\u0103\u1eaf\u1eb1\u1eb3\u1eb5\u1eb7" },  // ă
            { 'e',  "e\u00e9\u00e8\u1ebb\u1ebd\u1eb9" },
            { '\u00ea', "\u00ea\u1ebf\u1ec1\u1ec3\u1ec5\u1ec7" },  // ê
            { 'i',  "i\u00ed\u00ec\u1ec9\u0129\u1ecb" },
            { 'o',  "o\u00f3\u00f2\u1ecf\u00f5\u1ecd" },
            { '\u00f4', "\u00f4\u1ed1\u1ed3\u1ed5\u1ed7\u1ed9" },  // ô
            { '\u01a1', "\u01a1\u1edb\u1edd\u1edf\u1ee1\u1ee3" },  // ơ
            { 'u',  "u\u00fa\u00f9\u1ee7\u0169\u1ee5" },
            { '\u01b0', "\u01b0\u1ee9\u1eeb\u1eed\u1eef\u1ef1" },  // ư
            { 'y',  "y\u00fd\u1ef3\u1ef7\u1ef9\u1ef5" },
        };

        /// <summary>
        /// Processes a single typed character under Vietnamese Telex rules.
        /// </summary>
        private void HandleVietnameseChar(char ch)
        {
            var lower = char.ToLowerInvariant(ch);
            var isUpper = char.IsUpper(ch);
            var currentText = text;
            var caret = caretPosition;

            // 1) Tone mark keys (s, f, r, x, j)
            if (TelexToneMap.TryGetValue(lower, out var toneIndex))
            {
                if (TryApplyTone(ref currentText, caret, toneIndex))
                {
                    SetTextAndCaret(currentText, caret);
                    return;
                }
                // If tone cannot be applied, insert literally
            }

            // 2) Double-key (aa→â, ee→ê, oo→ô, dd→đ) — scan backward in word
            if (TelexDoubleMap.ContainsKey(lower))
            {
                if (TryApplyDoubleKey(ref currentText, caret, lower, isUpper))
                {
                    SetTextAndCaret(currentText, caret);
                    return;
                }
            }

            // 3) w-key (aw→ă, ow→ơ, uw→ư, uo→ươ) — scan backward in word
            if (lower == 'w')
            {
                if (TryApplyWKey(ref currentText, caret))
                {
                    SetTextAndCaret(currentText, caret);
                    return;
                }
            }

            // 4) Default: insert the character as-is
            currentText = currentText.Insert(caret, ch.ToString());
            SetTextAndCaret(currentText, caret + 1);
        }

        /// <summary>
        /// Scans backward through the current word for a character matching
        /// the double-key target (a→â, e→ê, o→ô, d→đ) and transforms it.
        /// </summary>
        private static bool TryApplyDoubleKey(ref string text, int caret, char lower, bool isUpper)
        {
            var target = TelexDoubleMap[lower];
            for (var i = caret - 1; i >= 0; i--)
            {
                var c = text[i];
                var cLower = char.ToLowerInvariant(c);
                if (cLower == ' ' || cLower == '\n' || cLower == '\r') break;

                if (lower == 'd')
                {
                    if (cLower == 'd')
                    {
                        var replacement = target;
                        if (isUpper || char.IsUpper(c)) replacement = char.ToUpperInvariant(replacement);
                        text = text.Remove(i, 1).Insert(i, replacement.ToString());
                        return true;
                    }
                    // Already đ → undo: revert to 'd', return false so literal 'd' inserts → "dd"
                    if (cLower == '\u0111')
                    {
                        var reverted = char.IsUpper(c) ? 'D' : 'd';
                        text = text.Remove(i, 1).Insert(i, reverted.ToString());
                        return false;
                    }
                }
                else
                {
                    var cBase = GetVowelBase(cLower);
                    if (cBase == lower)
                    {
                        var replacement = target;
                        replacement = TransferTone(cLower, replacement);
                        if (isUpper || char.IsUpper(c)) replacement = char.ToUpperInvariant(replacement);
                        text = text.Remove(i, 1).Insert(i, replacement.ToString());
                        return true;
                    }
                    // Already transformed (â/ê/ô) → undo: revert to base, return false → "aa"/"ee"/"oo"
                    if (cBase == target)
                    {
                        var reverted = lower;
                        reverted = TransferTone(cLower, reverted);
                        if (char.IsUpper(c)) reverted = char.ToUpperInvariant(reverted);
                        text = text.Remove(i, 1).Insert(i, reverted.ToString());
                        return false;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Scans backward through the current word for a vowel that can be
        /// w-transformed (a→ă, o→ơ, u→ư) and applies the transformation.
        /// Also handles the uo→ươ cluster automatically.
        /// </summary>
        private static bool TryApplyWKey(ref string text, int caret)
        {
            for (var i = caret - 1; i >= 0; i--)
            {
                var c = text[i];
                var cLower = char.ToLowerInvariant(c);
                if (cLower == ' ' || cLower == '\n' || cLower == '\r') break;

                var cBase = GetVowelBase(cLower);

                // Already transformed (ă/ơ/ư) → undo: revert to base, return false → "aw"/"ow"/"uw"
                if (cBase == '\u0103' || cBase == '\u01a1' || cBase == '\u01b0')
                {
                    char plain;
                    if (cBase == '\u0103') plain = 'a';      // ă → a
                    else if (cBase == '\u01a1') plain = 'o';  // ơ → o
                    else plain = 'u';                         // ư → u

                    var reverted = plain;
                    reverted = TransferTone(cLower, reverted);
                    if (char.IsUpper(c)) reverted = char.ToUpperInvariant(reverted);
                    text = text.Remove(i, 1).Insert(i, reverted.ToString());

                    // Also undo ươ cluster: if we're reverting ơ→o, check prev for ư→u
                    if (cBase == '\u01a1' && i > 0)
                    {
                        var prev = text[i - 1];
                        var prevBase = GetVowelBase(char.ToLowerInvariant(prev));
                        if (prevBase == '\u01b0') // ư
                        {
                            var rep2 = 'u';
                            rep2 = TransferTone(char.ToLowerInvariant(prev), rep2);
                            if (char.IsUpper(prev)) rep2 = char.ToUpperInvariant(rep2);
                            text = text.Remove(i - 1, 1).Insert(i - 1, rep2.ToString());
                        }
                    }

                    return false;
                }

                if (!TelexWMap.ContainsKey(cBase)) continue;

                var replacement = TelexWMap[cBase];
                replacement = TransferTone(cLower, replacement);
                if (char.IsUpper(c)) replacement = char.ToUpperInvariant(replacement);
                text = text.Remove(i, 1).Insert(i, replacement.ToString());

                // Handle uo→ươ cluster: also transform preceding 'u'
                if (cBase == 'o' && i > 0)
                {
                    var prev = text[i - 1];
                    var prevLower = char.ToLowerInvariant(prev);
                    var prevBase = GetVowelBase(prevLower);
                    if (prevBase == 'u' && !IsSpecialVowel(prevBase))
                    {
                        var rep2 = TelexWMap['u'];
                        rep2 = TransferTone(prevLower, rep2);
                        if (char.IsUpper(prev)) rep2 = char.ToUpperInvariant(rep2);
                        text = text.Remove(i - 1, 1).Insert(i - 1, rep2.ToString());
                    }
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the nearest vowel before <paramref name="caret"/> and applies the tone.
        /// Returns true if a vowel was found and modified.
        /// </summary>
        private static bool TryApplyTone(ref string text, int caret, int toneIndex)
        {
            // Vietnamese orthography: words ending in stop consonants (t, c, ch, p)
            // only allow sắc (1) and nặng (5). Reject huyền (2), hỏi (3), ngã (4).
            if (toneIndex >= 2 && toneIndex <= 4 && HasStopConsonantEnding(text, caret))
            {
                return false;
            }

            // Search backwards for the tone-target vowel in the current word
            var vowelPos = FindToneTargetVowel(text, caret);
            if (vowelPos < 0) return false;

            var ch = text[vowelPos];
            var wasUpper = char.IsUpper(ch);
            var lowerCh = char.ToLowerInvariant(ch);
            var baseVowel = GetVowelBase(lowerCh);

            if (!VowelToneTable.TryGetValue(baseVowel, out var toneRow)) return false;

            // If the vowel already carries this exact tone, undo it and
            // return false so the tone key is inserted as a literal character.
            // Example: "té" + 's' → revert to "te", then 's' inserted → "tes"
            var currentToneIdx = toneRow.IndexOf(lowerCh);
            if (currentToneIdx == toneIndex)
            {
                var baseChar = toneRow[0];
                if (wasUpper) baseChar = char.ToUpperInvariant(baseChar);
                text = text.Remove(vowelPos, 1).Insert(vowelPos, baseChar.ToString());
                return false;
            }

            var toned = toneRow[toneIndex];
            if (wasUpper) toned = char.ToUpperInvariant(toned);

            text = text.Remove(vowelPos, 1).Insert(vowelPos, toned.ToString());
            return true;
        }

        /// <summary>
        /// Locates the vowel that should receive the tone mark, following
        /// simplified Vietnamese tone placement rules.
        /// </summary>
        private static int FindToneTargetVowel(string text, int caret)
        {
            // Scan backward to find vowels in the current word
            var vowelPositions = new List<int>();
            for (var i = caret - 1; i >= 0; i--)
            {
                var c = char.ToLowerInvariant(text[i]);
                if (c == ' ' || c == '\n' || c == '\r') break;
                if (IsVietnameseVowel(c))
                {
                    vowelPositions.Add(i);
                }
            }

            if (vowelPositions.Count == 0) return -1;
            if (vowelPositions.Count == 1) return vowelPositions[0];

            // Reverse so they're in left-to-right order
            vowelPositions.Reverse();

            // Priority 1: if there is a special/modified vowel (â, ă, ê, ô, ơ, ư),
            // the tone mark goes on that vowel.
            var specialPositions = new List<int>();
            foreach (var pos in vowelPositions)
            {
                var baseV = GetVowelBase(char.ToLowerInvariant(text[pos]));
                if (IsSpecialVowel(baseV))
                {
                    specialPositions.Add(pos);
                }
            }

            if (specialPositions.Count == 1)
            {
                return specialPositions[0];
            }

            if (specialPositions.Count > 1)
            {
                // Multiple special vowels (e.g. ươ): prefer the non-ư one (ơ gets tone)
                foreach (var pos in specialPositions)
                {
                    var baseV = GetVowelBase(char.ToLowerInvariant(text[pos]));
                    if (baseV != '\u01b0') // not ư
                        return pos;
                }
                return specialPositions[specialPositions.Count - 1];
            }

            // Priority 2: positional rules for plain vowels
            var lastVowelIdx = vowelPositions[vowelPositions.Count - 1];
            var hasTrailingConsonant = false;
            for (var i = lastVowelIdx + 1; i < caret; i++)
            {
                var c = char.ToLowerInvariant(text[i]);
                if (!IsVietnameseVowel(c) && char.IsLetter(c))
                {
                    hasTrailingConsonant = true;
                    break;
                }
            }

            // 3+ vowels → middle vowel
            if (vowelPositions.Count >= 3)
            {
                return vowelPositions[1];
            }

            // 2 vowels: trailing consonant → last vowel; otherwise → first vowel
            if (hasTrailingConsonant)
            {
                return vowelPositions[vowelPositions.Count - 1];
            }
            return vowelPositions[0];
        }

        /// <summary>Returns the base (unmarked) vowel for a Vietnamese character.</summary>
        private static char GetVowelBase(char c)
        {
            foreach (var kvp in VowelToneTable)
            {
                if (kvp.Value.IndexOf(c) >= 0) return kvp.Key;
            }
            return c;
        }

        /// <summary>
        /// If <paramref name="source"/> has a tone, transfers it to <paramref name="target"/>.
        /// </summary>
        private static char TransferTone(char source, char target)
        {
            var sourceLower = char.ToLowerInvariant(source);
            var sourceBase = GetVowelBase(sourceLower);
            if (!VowelToneTable.TryGetValue(sourceBase, out var srcRow)) return target;

            var toneIdx = srcRow.IndexOf(sourceLower);
            if (toneIdx <= 0) return target; // no tone or is base

            var targetLower = char.ToLowerInvariant(target);
            var targetBase = GetVowelBase(targetLower);
            if (!VowelToneTable.TryGetValue(targetBase, out var tgtRow)) return target;

            return tgtRow[toneIdx];
        }

        /// <summary>Checks if a lowercase character is a Vietnamese vowel (including diacritics).</summary>
        private static bool IsVietnameseVowel(char c)
        {
            foreach (var kvp in VowelToneTable)
            {
                if (kvp.Value.IndexOf(c) >= 0) return true;
            }
            return false;
        }

        /// <summary>Checks if a base vowel is a special/modified vowel (â, ă, ê, ô, ơ, ư).</summary>
        private static bool IsSpecialVowel(char baseVowel)
        {
            return baseVowel == '\u00e2' || // â
                   baseVowel == '\u0103' || // ă
                   baseVowel == '\u00ea' || // ê
                   baseVowel == '\u00f4' || // ô
                   baseVowel == '\u01a1' || // ơ
                   baseVowel == '\u01b0';   // ư
        }

        /// <summary>
        /// Checks whether the current word (up to <paramref name="caret"/>) ends with
        /// a Vietnamese stop consonant (t, c, ch, p). Words with these endings
        /// can only carry sắc or nặng tones.
        /// </summary>
        private static bool HasStopConsonantEnding(string text, int caret)
        {
            // Collect trailing consonant cluster of the current word
            var end = caret - 1;
            while (end >= 0)
            {
                var c = char.ToLowerInvariant(text[end]);
                if (c == ' ' || c == '\n' || c == '\r') return false;
                // Stop at first vowel — everything after it is the consonant ending
                if (IsVietnameseVowel(c)) break;
                if (!char.IsLetter(c)) return false;
                end--;
            }

            // Build the trailing consonant string after the last vowel
            var lastVowelIdx = end;
            if (lastVowelIdx < 0) return false; // no vowel found = no valid word

            var consonantLen = (caret - 1) - lastVowelIdx;
            if (consonantLen <= 0) return false; // word ends in vowel, no restriction

            var trailing = text.Substring(lastVowelIdx + 1, consonantLen).ToLowerInvariant();
            return trailing == "t" || trailing == "c" || trailing == "ch" || trailing == "p";
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════
        //  KOREAN HANGUL COMPOSITION
        // ══════════════════════════════════════════════════════════════════

        #region Korean Hangul

        // Choseong (initial consonants) — 19 entries
        private static readonly string[] Choseong =
        {
            "r", "R", "s", "e", "E",       // ㄱ ㄲ ㄴ ㄷ ㄸ
            "f", "a", "q", "Q", "t",       // ㄹ ㅁ ㅂ ㅃ ㅅ
            "T", "d", "w", "W", "c",       // ㅆ ㅇ ㅈ ㅉ ㅊ
            "z", "x", "v", "g"             // ㅋ ㅌ ㅍ ㅎ
        };

        // Jungseong (medial vowels) — 21 entries
        private static readonly string[] Jungseong =
        {
            "k",  "o",  "i",  "O",  "j",       // ㅏ ㅐ ㅑ ㅒ ㅓ
            "p",  "u",  "P",  "h",  "hk",      // ㅔ ㅕ ㅖ ㅗ ㅘ
            "ho", "hl", "y",  "n",  "nj",       // ㅙ ㅚ ㅛ ㅜ ㅝ
            "np", "nl", "b",  "m",  "ml",       // ㅞ ㅟ ㅠ ㅡ ㅢ
            "l"                                  // ㅣ
        };

        // Jongseong (final consonants) — index 0 = none, 1..27
        private static readonly string[] Jongseong =
        {
            "",    "r",  "R",  "rt", "s",  "sw", "sg",
            "e",   "f",  "fr", "fa", "fq", "ft", "fx",
            "fv",  "fg", "a",  "q",  "qt", "t",  "T",
            "d",   "w",  "c",  "z",  "x",  "v",  "g"
        };

        // Map: jongseong index → (choseong index, remaining jongseong index)
        // When a vowel follows a jongseong, some compound jongseong can split.
        private static readonly Dictionary<int, (int cho, int remainJong)> JongseongSplit =
            new Dictionary<int, (int, int)>
        {
            {  3, ( 9, 1) },  // rt → r + t  (ㄱ+ㅅ → ㄱ remains, ㅅ becomes cho)
            {  5, (12, 1) },  // sw → s + w
            {  6, (18, 1) },  // sg → s + g
            {  9, ( 1, 8) },  // fr → f + r  (ㄹㄱ → ㄹ remains, ㄱ)
            { 10, ( 6, 8) },  // fa → f + a
            { 11, ( 7, 8) },  // fq → f + q
            { 12, ( 9, 8) },  // ft → f + t
            { 13, (16, 8) },  // fx → f + x
            { 14, (17, 8) },  // fv → f + v
            { 15, (18, 8) },  // fg → f + g
            { 18, ( 9,17) },  // qt → q + t
        };

        /// <summary>
        /// Processes a single typed character under Korean Hangul rules.
        /// Uses the 2-set Korean keyboard layout (두벌식).
        /// </summary>
        private void HandleKoreanChar(char ch)
        {
            // Only handle ASCII letters; others inserted literally
            if (!IsLatinLetter(ch))
            {
                FinalizeHangul();
                var currentText = text.Insert(caretPosition, ch.ToString());
                SetTextAndCaret(currentText, caretPosition + 1);
                return;
            }

            var key = ch.ToString();

            // Try as jungseong (vowel) first — check compound then single
            var jungIdx = TryMatchJungseong(key);

            // Try as choseong (consonant)
            var choIdx = FindIndex(Choseong, key);

            // Try as jongseong (final consonant)
            var jongIdx = FindIndex(Jongseong, key);

            switch (_hangulState)
            {
                case HangulState.Empty:
                    if (jungIdx >= 0)
                    {
                        // Standalone vowel (no choseong)
                        _cho = 11; // ㅇ (silent ieung) as placeholder? No — in Korean, standalone vowels use jamo
                        _jung = jungIdx;
                        _hangulState = HangulState.Jungseong;
                        UpdateComposing();
                    }
                    else if (choIdx >= 0)
                    {
                        _cho = choIdx;
                        _hangulState = HangulState.Choseong;
                        UpdateComposing();
                    }
                    else
                    {
                        // Insert literally
                        var t = text.Insert(caretPosition, ch.ToString());
                        SetTextAndCaret(t, caretPosition + 1);
                    }
                    break;

                case HangulState.Choseong:
                    if (jungIdx >= 0)
                    {
                        // consonant + vowel → forming syllable
                        _jung = jungIdx;
                        _hangulState = HangulState.Jungseong;
                        UpdateComposing();
                    }
                    else if (choIdx >= 0)
                    {
                        // Another consonant — finalize previous, start new
                        FinalizeHangul();
                        _cho = choIdx;
                        _hangulState = HangulState.Choseong;
                        UpdateComposing();
                    }
                    else
                    {
                        FinalizeHangul();
                        var t = text.Insert(caretPosition, ch.ToString());
                        SetTextAndCaret(t, caretPosition + 1);
                    }
                    break;

                case HangulState.Jungseong:
                    // Try compound vowel
                    var compoundJung = TryCompoundJungseong(_jung, key);
                    if (compoundJung >= 0)
                    {
                        _jung = compoundJung;
                        UpdateComposing();
                        break;
                    }

                    if (jongIdx > 0) // jongIdx 0 = empty, skip
                    {
                        _jong = jongIdx;
                        _hangulState = HangulState.Jongseong;
                        UpdateComposing();
                    }
                    else if (jungIdx >= 0)
                    {
                        // New vowel — finalize and start fresh
                        FinalizeHangul();
                        _cho = 11; // ㅇ placeholder for standalone vowel
                        _jung = jungIdx;
                        _hangulState = HangulState.Jungseong;
                        UpdateComposing();
                    }
                    else if (choIdx >= 0)
                    {
                        // New consonant — finalize and start
                        FinalizeHangul();
                        _cho = choIdx;
                        _hangulState = HangulState.Choseong;
                        UpdateComposing();
                    }
                    else
                    {
                        FinalizeHangul();
                        var t = text.Insert(caretPosition, ch.ToString());
                        SetTextAndCaret(t, caretPosition + 1);
                    }
                    break;

                case HangulState.Jongseong:
                    // If vowel follows jongseong, split jongseong
                    if (jungIdx >= 0)
                    {
                        // Try to split compound jongseong
                        if (JongseongSplit.TryGetValue(_jong, out var split))
                        {
                            _jong = split.remainJong;
                            UpdateComposing(); // update current syllable without final
                            FinalizeHangul();
                            _cho = split.cho;
                            _jung = jungIdx;
                            _hangulState = HangulState.Jungseong;
                            UpdateComposing();
                        }
                        else
                        {
                            // Simple jongseong → decompose: jongseong becomes choseong of new syllable
                            var newCho = JongseongToChoseong(_jong);
                            _jong = 0;
                            UpdateComposing();
                            FinalizeHangul();
                            _cho = newCho;
                            _jung = jungIdx;
                            _hangulState = HangulState.Jungseong;
                            UpdateComposing();
                        }
                        break;
                    }

                    // Try compound jongseong
                    var compoundJong = TryCompoundJongseong(_jong, key);
                    if (compoundJong > 0)
                    {
                        _jong = compoundJong;
                        UpdateComposing();
                        break;
                    }

                    if (choIdx >= 0)
                    {
                        FinalizeHangul();
                        _cho = choIdx;
                        _hangulState = HangulState.Choseong;
                        UpdateComposing();
                    }
                    else
                    {
                        FinalizeHangul();
                        var t = text.Insert(caretPosition, ch.ToString());
                        SetTextAndCaret(t, caretPosition + 1);
                    }
                    break;
            }
        }

        /// <summary>Composes the current Hangul syllable Unicode character.</summary>
        private char ComposeHangul()
        {
            // Unicode formula: 0xAC00 + (cho * 21 + jung) * 28 + jong
            var jong = _jong >= 0 ? _jong : 0;
            var code = 0xAC00 + (_cho * 21 + _jung) * 28 + jong;
            return (char)code;
        }

        /// <summary>Updates the composing (in-progress) character at the caret.</summary>
        private void UpdateComposing()
        {
            var currentText = text;
            var caret = caretPosition;

            if (_hangulState == HangulState.Choseong)
            {
                // Show the standalone jamo character (compatibility jamo: 0x3131 + offset)
                var jamo = GetChoseongJamo(_cho);
                if (caret > 0 && IsComposingPosition(caret))
                {
                    currentText = currentText.Remove(caret - 1, 1).Insert(caret - 1, jamo.ToString());
                    SetTextAndCaret(currentText, caret);
                }
                else
                {
                    currentText = currentText.Insert(caret, jamo.ToString());
                    SetTextAndCaret(currentText, caret + 1);
                }
            }
            else if (_jung >= 0)
            {
                var composed = ComposeHangul();
                if (caret > 0 && IsComposingPosition(caret))
                {
                    currentText = currentText.Remove(caret - 1, 1).Insert(caret - 1, composed.ToString());
                    SetTextAndCaret(currentText, caret);
                }
                else
                {
                    currentText = currentText.Insert(caret, composed.ToString());
                    SetTextAndCaret(currentText, caret + 1);
                }
            }
        }

        /// <summary>Whether the character before caret is our composing character.</summary>
        private bool _isComposing;

        private bool IsComposingPosition(int caret)
        {
            return _isComposing && caret > 0;
        }

        /// <summary>Finalizes the current Hangul composition, resetting state.</summary>
        private void FinalizeHangul()
        {
            _hangulState = HangulState.Empty;
            _cho = -1;
            _jung = -1;
            _jong = -1;
            _isComposing = false;
        }

        // ──────────────── Jamo lookup helpers ────────────────

        // Compatibility Jamo for choseong display
        private static readonly char[] ChoseongJamo =
        {
            '\u3131', '\u3132', '\u3134', '\u3137', '\u3138', // ㄱ ㄲ ㄴ ㄷ ㄸ
            '\u3139', '\u3141', '\u3142', '\u3143', '\u3145', // ㄹ ㅁ ㅂ ㅃ ㅅ
            '\u3146', '\u3147', '\u3148', '\u3149', '\u314A', // ㅆ ㅇ ㅈ ㅉ ㅊ
            '\u314B', '\u314C', '\u314D', '\u314E'            // ㅋ ㅌ ㅍ ㅎ
        };

        private static char GetChoseongJamo(int index)
        {
            if (index >= 0 && index < ChoseongJamo.Length) return ChoseongJamo[index];
            return '?';
        }

        /// <summary>Converts a jongseong index back to the corresponding choseong index.</summary>
        private static int JongseongToChoseong(int jongIdx)
        {
            // Mapping from jongseong index → choseong index (for single jongseong only)
            // jong: 1=ㄱ→cho:0, 2=ㄲ→1, 4=ㄴ→2, 7=ㄷ→3, 8=ㄹ→5,
            //      16=ㅁ→6, 17=ㅂ→7, 19=ㅅ→9, 20=ㅆ→10, 21=ㅇ→11,
            //      22=ㅈ→12, 23=ㅊ→14, 24=ㅋ→15, 25=ㅌ→16, 26=ㅍ→17, 27=ㅎ→18
            switch (jongIdx)
            {
                case 1: return 0;
                case 2: return 1;
                case 4: return 2;
                case 7: return 3;
                case 8: return 5;
                case 16: return 6;
                case 17: return 7;
                case 19: return 9;
                case 20: return 10;
                case 21: return 11;
                case 22: return 12;
                case 23: return 14;
                case 24: return 15;
                case 25: return 16;
                case 26: return 17;
                case 27: return 18;
                default: return 0;
            }
        }

        /// <summary>Tries to match a key as a jungseong (vowel). Returns index or -1.</summary>
        private int TryMatchJungseong(string key)
        {
            // First check compound with previous jungseong state (handled separately)
            // Here just find single-char match
            return FindIndex(Jungseong, key);
        }

        /// <summary>
        /// Tries to form a compound jungseong from the current jungseong + new key.
        /// Returns the compound index or -1.
        /// </summary>
        private static int TryCompoundJungseong(int currentJung, string key)
        {
            // Compound vowels:
            // ㅗ(8) + k(ㅏ,0) = ㅘ(9)
            // ㅗ(8) + o(ㅐ,1) = ㅙ(10)
            // ㅗ(8) + l(ㅣ,20) = ㅚ(11)
            // ㅜ(13) + j(ㅓ,4) = ㅝ(14)
            // ㅜ(13) + p(ㅔ,5) = ㅞ(15)
            // ㅜ(13) + l(ㅣ,20) = ㅟ(16)
            // ㅡ(18) + l(ㅣ,20) = ㅢ(19)
            if (currentJung == 8 && key == "k") return 9;
            if (currentJung == 8 && key == "o") return 10;
            if (currentJung == 8 && key == "l") return 11;
            if (currentJung == 13 && key == "j") return 14;
            if (currentJung == 13 && key == "p") return 15;
            if (currentJung == 13 && key == "l") return 16;
            if (currentJung == 18 && key == "l") return 19;
            return -1;
        }

        /// <summary>
        /// Tries to form a compound jongseong from the current jongseong + new key.
        /// Returns the compound index or -1.
        /// </summary>
        private static int TryCompoundJongseong(int currentJong, string key)
        {
            // Compound jongseong from Jongseong table:
            // ㄱ(1) + t(ㅅ) = ㄳ(3)
            // ㄴ(4) + w(ㅈ) = ㄵ(5),  ㄴ(4) + g(ㅎ) = ㄶ(6)
            // ㄹ(8) + r(ㄱ) = ㄺ(9),  ㄹ(8) + a(ㅁ) = ㄻ(10), ㄹ(8) + q(ㅂ) = ㄼ(11)
            // ㄹ(8) + t(ㅅ) = ㄽ(12), ㄹ(8) + x(ㅌ) = ㄾ(13), ㄹ(8) + v(ㅍ) = ㄿ(14)
            // ㄹ(8) + g(ㅎ) = ㅀ(15)
            // ㅂ(17) + t(ㅅ) = ㅄ(18)
            if (currentJong == 1 && key == "t") return 3;
            if (currentJong == 4 && key == "w") return 5;
            if (currentJong == 4 && key == "g") return 6;
            if (currentJong == 8 && key == "r") return 9;
            if (currentJong == 8 && key == "a") return 10;
            if (currentJong == 8 && key == "q") return 11;
            if (currentJong == 8 && key == "t") return 12;
            if (currentJong == 8 && key == "x") return 13;
            if (currentJong == 8 && key == "v") return 14;
            if (currentJong == 8 && key == "g") return 15;
            if (currentJong == 17 && key == "t") return 18;
            return -1;
        }

        private static int FindIndex(string[] array, string key)
        {
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] == key) return i;
            }
            return -1;
        }

        private static bool IsLatinLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════
        //  SHARED HELPERS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the input field text and restores the caret position.
        /// </summary>
        private void SetTextAndCaret(string newText, int newCaret)
        {
            // Mark composing state for Korean
            if (_currentLanguage == InputLanguage.Korean && _hangulState != HangulState.Empty)
            {
                _isComposing = true;
            }

            text = newText;

            // Defer caret update to next frame to avoid TMP layout race
            caretPosition = Mathf.Clamp(newCaret, 0, newText.Length);
            selectionAnchorPosition = caretPosition;
            selectionFocusPosition = caretPosition;
        }
    }
}
