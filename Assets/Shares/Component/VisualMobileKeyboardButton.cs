using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Share.Components
{
    public enum VisualMobileKeyType
    {
        Letter,
        Space,
        Enter,
        Backspace,
        Uppercase,
        Lowercase,
        Symbol1,
        Symbol2,
        BackSymbol,
        Language,
        Number,
        Emoji
    }

    /// <summary>
    /// Individual keyboard button. Implements <see cref="IPointerClickHandler"/>
    /// directly instead of relying on <see cref="Button.onClick"/> so that
    /// clicking a key does NOT change the EventSystem's selected object
    /// (i.e. the input field keeps focus).
    /// The serialized Button component is disabled at runtime to remove
    /// it from the Selectable pool.
    /// </summary>
    public class VisualMobileKeyboardButton : MonoBehaviour,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        public VisualMobileKeyType type;
        public string DisplayText;
        public string InputText;
        [SerializeField] private TextMeshProUGUI textMeshProUGUI;
        [SerializeField] private Image _imageTarget;
        [Header("Color Transition")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        [SerializeField] private Color _pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        [SerializeField] private Color _disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
        [SerializeField] private float _fadeDuration = 0.1f;
        [SerializeField] private bool _interactable = true;

        private Action<VisualMobileKeyboardButton> _onClick;
        private bool _isPointerDown;
        private Color _originalImageColor = Color.white;
        private bool _hasOriginalImageColor;

        void Awake()
        {
            ResolveImageTarget();

            CacheOriginalColorIfNeeded();

            ApplyStateColor(_interactable ? _normalColor : _disabledColor, true);
        }

        private void OnEnable()
        {
            CacheOriginalColorIfNeeded();
            ApplyStateColor(_interactable ? _normalColor : _disabledColor, true);
        }

        private void OnDisable()
        {
            _isPointerDown = false;
        }

        /// <summary>
        /// Handles pointer click via IPointerClickHandler (no selection change).
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _onClick?.Invoke(this);
            ApplyStateColor(_normalColor);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isPointerDown = true;
            ApplyStateColor(_pressedColor);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_interactable || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isPointerDown = false;
            // Keyboard keys should return to the base visual state after release.
            ApplyStateColor(_normalColor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable)
            {
                return;
            }

            if (!_isPointerDown)
            {
                ApplyStateColor(_highlightedColor);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_interactable)
            {
                return;
            }

            if (!_isPointerDown)
            {
                ApplyStateColor(_normalColor);
            }
        }

        public void Install(string displayText, string inputText, Action<VisualMobileKeyboardButton> onClick)
        {
            DisplayText = displayText;
            InputText = inputText;
            _onClick = onClick;
            textMeshProUGUI.text = displayText;

            // Ensure we capture the final runtime key color as the true base color.
            CacheOriginalColorFromCurrent(force: true);
            ApplyStateColor(_interactable ? _normalColor : _disabledColor, true);
        }

        public void SetDisplayText(string displayText)
        {
            DisplayText = displayText;
            textMeshProUGUI.text = displayText;
        }

        /// <summary>
        /// Sets interactable state and updates the visual color accordingly.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            _isPointerDown = false;
            ApplyStateColor(_interactable ? _normalColor : _disabledColor);
        }

        private void ApplyStateColor(Color stateMultiplier, bool instant = false)
        {
            if (_imageTarget == null)
            {
                return;
            }

            CacheOriginalColorIfNeeded();

            // Always: finalColor = originalImageColor × stateMultiplier.
            // Image.color is locked to white (set in CacheOriginalColorFromCurrent)
            // so CanvasRenderer.color is the only active multiplier.
            var finalColor = MultiplyColor(_originalImageColor, stateMultiplier);

            if (instant || !isActiveAndEnabled || _fadeDuration <= 0f)
            {
                // Use CanvasRenderer directly to stay consistent with CrossFadeColor path.
                _imageTarget.canvasRenderer.SetColor(finalColor);
                return;
            }

            _imageTarget.CrossFadeColor(finalColor, _fadeDuration, true, true);
        }

        private static Color MultiplyColor(Color baseColor, Color tint)
        {
            return new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a * tint.a);
        }

        private void CacheOriginalColorIfNeeded()
        {
            if (_hasOriginalImageColor)
            {
                return;
            }

            CacheOriginalColorFromCurrent(force: false);
        }

        private void CacheOriginalColorFromCurrent(bool force)
        {
            ResolveImageTarget();

            if (_imageTarget == null)
            {
                return;
            }

            if (!force && _hasOriginalImageColor)
            {
                return;
            }

            // Capture the sprite's intended color from Image.color.
            _originalImageColor = _imageTarget.color;

            // Lock Image.color to white so that CanvasRenderer.color becomes the
            // sole color multiplier. Without this, CrossFadeColor applies ON TOP
            // of Image.color causing double-multiplication and black artifacts:
            //   displayed = Image.color × CanvasRenderer.color
            // With Image.color = white: displayed = CanvasRenderer.color only.
            _imageTarget.color = Color.white;
            _hasOriginalImageColor = true;
        }

        private void ResolveImageTarget()
        {
            if (_imageTarget != null)
            {
                return;
            }

            // Prefer a visible child image (e.g. "Fill") over the root image,
            // since the root may be transparent or stylistic-only.
            var childImages = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                var img = childImages[i];
                if (img == null || img.gameObject == gameObject)
                {
                    continue;
                }

                if (img.color.a > 0.01f)
                {
                    _imageTarget = img;
                    return;
                }
            }

            _imageTarget = GetComponent<Image>();
        }
    }
}
