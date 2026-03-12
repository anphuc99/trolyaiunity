using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Share.Components
{
    /// <summary>
    /// Visual on-screen mobile keyboard that works with <see cref="MultiLanguageInputField"/>.
    /// Supports alphabet (lowercase/uppercase), Hangul (lowercase/uppercase), and two symbol panels.
    /// Slides in from the bottom when shown, slides out when hidden.
    /// </summary>
    public class VisualMobileKeyboard : MonoBehaviour
    {
        [SerializeField] private GameObject _UIPanel;
        [SerializeField] private VisualMobileKeyboardPanel _alphabetPanel;
        [SerializeField] private VisualMobileKeyboardPanel _alphabetPanelUppercase;
        [SerializeField] private VisualMobileKeyboardPanel _hangulPanel;
        [SerializeField] private VisualMobileKeyboardPanel _hangulPanelUppercase;
        [SerializeField] private VisualMobileKeyboardPanel _symbolPanel1;
        [SerializeField] private VisualMobileKeyboardPanel _symbolPanel2;

        [Header("Animation")]
        [SerializeField] private float _animDuration = 0.3f;

        /// <summary>Invoked when the language button is pressed. Payload is the new label (ENG/VI/KO).</summary>
        public Action<string> _languageChangeAction;

        /// <summary>Invoked for every character/action input from the keyboard.</summary>
        public Action<string> _inputAction;

        private RectTransform _rectTransform;
        private float _hiddenY;
        private float _shownY;
        private Tween _slideTween;
        private bool _isShown;
        private bool _isUppercase;
        private InputLanguage _currentLanguage = InputLanguage.English;
        private MultiLanguageInputField _focusedInput;

        // ──────────────────────── Keyboard layout data ────────────────────────

        // Alphabet lowercase: standard QWERTY
        private static readonly string[] AlphabetLowerDisplay =
        {
            "q","w","e","r","t","y","u","i","o","p",
            "a","s","d","f","g","h","j","k","l",
            "z","x","c","v","b","n","m"
        };
        private static readonly string[] AlphabetLowerInput =
        {
            "q","w","e","r","t","y","u","i","o","p",
            "a","s","d","f","g","h","j","k","l",
            "z","x","c","v","b","n","m"
        };

        // Alphabet uppercase
        private static readonly string[] AlphabetUpperDisplay =
        {
            "Q","W","E","R","T","Y","U","I","O","P",
            "A","S","D","F","G","H","J","K","L",
            "Z","X","C","V","B","N","M"
        };
        private static readonly string[] AlphabetUpperInput =
        {
            "Q","W","E","R","T","Y","U","I","O","P",
            "A","S","D","F","G","H","J","K","L",
            "Z","X","C","V","B","N","M"
        };

        // Hangul lowercase (두벌식): display = Hangul jamo, input = Latin key for MultiLanguageInputField
        private static readonly string[] HangulLowerDisplay =
        {
            "ㅂ","ㅈ","ㄷ","ㄱ","ㅅ","ㅛ","ㅕ","ㅑ","ㅐ","ㅔ",
            "ㅁ","ㄴ","ㅇ","ㄹ","ㅎ","ㅗ","ㅓ","ㅏ","ㅣ",
            "ㅋ","ㅌ","ㅊ","ㅍ","ㅠ","ㅜ","ㅡ"
        };
        private static readonly string[] HangulLowerInput =
        {
            "q","w","e","r","t","y","u","i","o","p",
            "a","s","d","f","g","h","j","k","l",
            "z","x","c","v","b","n","m"
        };

        // Hangul uppercase: only some keys have uppercase variants (ㅃ ㅉ ㄸ ㄲ ㅆ)
        // The rest stay lowercase input
        private static readonly string[] HangulUpperDisplay =
        {
            "ㅃ","ㅉ","ㄸ","ㄲ","ㅆ","ㅛ","ㅕ","ㅑ","ㅐ","ㅔ",
            "ㅁ","ㄴ","ㅇ","ㄹ","ㅎ","ㅗ","ㅓ","ㅏ","ㅣ",
            "ㅋ","ㅌ","ㅊ","ㅍ","ㅠ","ㅜ","ㅡ"
        };
        private static readonly string[] HangulUpperInput =
        {
            "Q","W","E","R","T","y","u","i","o","p",
            "a","s","d","f","g","h","j","k","l",
            "z","x","c","v","b","n","m"
        };

        // Symbol panel 1
        private static readonly string[] Symbol1Display =
        {
            "1","2","3","4","5","6","7","8","9","0",
            "@","#","$","_","&","-","+","(",")","/",
            "*","\"","'",":",";","!","?"
        };
        private static readonly string[] Symbol1Input =
        {
            "1","2","3","4","5","6","7","8","9","0",
            "@","#","$","_","&","-","+","(",")","/",
            "*","\"","'",":",";","!","?"
        };

        // Symbol panel 2
        private static readonly string[] Symbol2Display =
        {
            "~","`","|","·","√","π","÷","×","¶","∆",
            "£","¢","€","¥","^","°","=","{","}","\\",
            "%","©","®","™","✓","[","]"
        };
        private static readonly string[] Symbol2Input =
        {
            "~","`","|","·","√","π","÷","×","¶","∆",
            "£","¢","€","¥","^","°","=","{","}","\\",
            "%","©","®","™","✓","[","]"
        };

        // ──────────────────────── Lifecycle ────────────────────────

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Lazily initializes RectTransform and panels (safe to call multiple times).</summary>
        private void EnsureInitialized()
        {
            if (_rectTransform != null) return;

            _rectTransform = GetComponent<RectTransform>();
            _shownY = _rectTransform.anchoredPosition.y;
            _hiddenY = _shownY - _rectTransform.rect.height;

            InstallAllPanels();
            HideAllPanels();
            gameObject.SetActive(false);
            _isShown = false;
        }

        // ──────────────────────── Public API ────────────────────────

        /// <summary>
        /// Shows the keyboard for the given input field, syncing language.
        /// </summary>
        public void Show(MultiLanguageInputField inputField)
        {
            EnsureInitialized();
            _focusedInput = inputField;
            _currentLanguage = inputField.CurrentLanguage;
            _isUppercase = false;
            UpdateLanguageButtonDisplay();
            ShowActivePanel();

            gameObject.SetActive(true);
            AnimateIn();
            _isShown = true;
        }

        /// <summary>
        /// Hides the keyboard with a slide-out animation.
        /// </summary>
        public void Hide()
        {
            if (!_isShown) return;
            _isShown = false;
            _focusedInput = null;
            AnimateOut(() =>
            {
                gameObject.SetActive(false);
                HideAllPanels();
            });
        }

        /// <summary>Whether the keyboard is currently visible.</summary>
        public bool IsShown => _isShown;

        /// <summary>Height of the keyboard panel in pixels (for UI push-up calculation).</summary>
        public float KeyboardHeight => _rectTransform != null ? _rectTransform.rect.height : 0f;

        /// <summary>The RectTransform of the keyboard UI panel.</summary>
        public RectTransform KeyboardRect => _rectTransform;

        /// <summary>The anchoredPosition.y when fully shown (for world-space calculation).</summary>
        public float ShownY => _shownY;

        // ──────────────────────── Panel Installation ────────────────────────

        /// <summary>Installs display/input text on all panels.</summary>
        private void InstallAllPanels()
        {
            _alphabetPanel.Install(AlphabetLowerDisplay, AlphabetLowerInput, OnButtonClicked);
            _alphabetPanelUppercase.Install(AlphabetUpperDisplay, AlphabetUpperInput, OnButtonClicked);
            _hangulPanel.Install(HangulLowerDisplay, HangulLowerInput, OnButtonClicked);
            _hangulPanelUppercase.Install(HangulUpperDisplay, HangulUpperInput, OnButtonClicked);
            _symbolPanel1.Install(Symbol1Display, Symbol1Input, OnButtonClicked);
            _symbolPanel2.Install(Symbol2Display, Symbol2Input, OnButtonClicked);
        }

        // ──────────────────────── Panel Visibility ────────────────────────

        /// <summary>Hides all keyboard panels.</summary>
        private void HideAllPanels()
        {
            _alphabetPanel.gameObject.SetActive(false);
            _alphabetPanelUppercase.gameObject.SetActive(false);
            _hangulPanel.gameObject.SetActive(false);
            _hangulPanelUppercase.gameObject.SetActive(false);
            _symbolPanel1.gameObject.SetActive(false);
            _symbolPanel2.gameObject.SetActive(false);
        }

        /// <summary>Shows the correct panel based on current language and uppercase state.</summary>
        private void ShowActivePanel()
        {
            HideAllPanels();

            switch (_currentLanguage)
            {
                case InputLanguage.English:
                case InputLanguage.Vietnamese:
                    if (_isUppercase)
                        _alphabetPanelUppercase.gameObject.SetActive(true);
                    else
                        _alphabetPanel.gameObject.SetActive(true);
                    break;
                case InputLanguage.Korean:
                    if (_isUppercase)
                        _hangulPanelUppercase.gameObject.SetActive(true);
                    else
                        _hangulPanel.gameObject.SetActive(true);
                    break;
            }
        }

        // ──────────────────────── Button Click Handler ────────────────────────

        /// <summary>Central click handler for all keyboard buttons.</summary>
        private void OnButtonClicked(VisualMobileKeyboardButton button)
        {
            switch (button.type)
            {
                case VisualMobileKeyType.Letter:
                    _inputAction?.Invoke(button.InputText);
                    // Auto-revert uppercase after one letter (like mobile keyboards)
                    if (_isUppercase)
                    {
                        _isUppercase = false;
                        ShowActivePanel();
                    }
                    break;

                case VisualMobileKeyType.Space:
                    _inputAction?.Invoke(" ");
                    break;

                case VisualMobileKeyType.Enter:
                    _inputAction?.Invoke("\n");
                    break;

                case VisualMobileKeyType.Backspace:
                    _inputAction?.Invoke("\b");
                    break;

                case VisualMobileKeyType.Uppercase:
                    _isUppercase = true;
                    ShowActivePanel();
                    break;

                case VisualMobileKeyType.Lowercase:
                    _isUppercase = false;
                    ShowActivePanel();
                    break;

                case VisualMobileKeyType.Symbol1:
                    HideAllPanels();
                    _symbolPanel1.gameObject.SetActive(true);
                    break;

                case VisualMobileKeyType.Symbol2:
                    HideAllPanels();
                    _symbolPanel2.gameObject.SetActive(true);
                    break;

                case VisualMobileKeyType.BackSymbol:
                    ShowActivePanel();
                    break;

                case VisualMobileKeyType.Language:
                    CycleLanguage();
                    break;

                case VisualMobileKeyType.Number:
                    // Not implemented yet
                    break;

                case VisualMobileKeyType.Emoji:
                    // Not implemented yet
                    break;
            }

            // Re-focus the input field so the keyboard stays open
            RefocusInput();
        }

        /// <summary>Re-selects the focused input field to prevent keyboard from closing.</summary>
        private void RefocusInput()
        {
            if (_focusedInput == null) return;
            EventSystem.current.SetSelectedGameObject(_focusedInput.gameObject);
            _focusedInput.ActivateInputField();
        }

        // ──────────────────────── Language Cycling ────────────────────────

        /// <summary>Cycles through ENG → VI → KO and updates the focused input field.</summary>
        private void CycleLanguage()
        {
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

            _isUppercase = false;

            // Notify the focused input field about the language change
            if (_focusedInput != null)
            {
                _focusedInput.SetLanguage(_currentLanguage);
            }

            UpdateLanguageButtonDisplay();
            ShowActivePanel();

            var label = GetLanguageLabel(_currentLanguage);
            _languageChangeAction?.Invoke(label);
        }

        /// <summary>Updates the display text on all Language-type buttons across all panels.</summary>
        private void UpdateLanguageButtonDisplay()
        {
            var label = GetLanguageLabel(_currentLanguage);
            UpdateLanguageButtonsOnPanel(_alphabetPanel, label);
            UpdateLanguageButtonsOnPanel(_alphabetPanelUppercase, label);
            UpdateLanguageButtonsOnPanel(_hangulPanel, label);
            UpdateLanguageButtonsOnPanel(_hangulPanelUppercase, label);
            UpdateLanguageButtonsOnPanel(_symbolPanel1, label);
            UpdateLanguageButtonsOnPanel(_symbolPanel2, label);
        }

        /// <summary>Sets display text on Language-type buttons in a single panel.</summary>
        private static void UpdateLanguageButtonsOnPanel(VisualMobileKeyboardPanel panel, string label)
        {
            if (panel == null || panel.Buttons == null) return;
            foreach (var btn in panel.Buttons)
            {
                if (btn.type == VisualMobileKeyType.Language)
                {
                    btn.SetDisplayText(label);
                }
            }
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

        // ──────────────────────── Animation ────────────────────────

        /// <summary>Slides the keyboard up from below the screen.</summary>
        private void AnimateIn()
        {
            _slideTween?.Kill();
            _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _hiddenY);
            _slideTween = DOTween.To(
                    () => _rectTransform.anchoredPosition,
                    v => _rectTransform.anchoredPosition = v,
                    new Vector2(_rectTransform.anchoredPosition.x, _shownY),
                    _animDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        /// <summary>Slides the keyboard down and then invokes <paramref name="onComplete"/>.</summary>
        private void AnimateOut(Action onComplete)
        {
            _slideTween?.Kill();
            _slideTween = DOTween.To(
                    () => _rectTransform.anchoredPosition,
                    v => _rectTransform.anchoredPosition = v,
                    new Vector2(_rectTransform.anchoredPosition.x, _hiddenY),
                    _animDuration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}