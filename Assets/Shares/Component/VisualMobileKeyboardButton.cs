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
        private bool _isPointerInside;
        private Color _baseImageColor = Color.white;

        void Awake()
        {
            if (_imageTarget == null)
            {
                _imageTarget = GetComponent<Image>();
            }

            if (_imageTarget != null)
            {
                _baseImageColor = _imageTarget.color;
            }

            ApplyStateColor(_interactable ? _normalColor : _disabledColor, true);
        }

        private void OnEnable()
        {
            ApplyStateColor(_interactable ? _normalColor : _disabledColor, true);
        }

        private void OnDisable()
        {
            _isPointerDown = false;
            _isPointerInside = false;
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
            ApplyStateColor(_isPointerInside ? _highlightedColor : _normalColor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactable)
            {
                return;
            }

            _isPointerInside = true;
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

            _isPointerInside = false;
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

        private void ApplyStateColor(Color targetColor, bool instant = false)
        {
            if (_imageTarget == null)
            {
                return;
            }

            var tintedColor = MultiplyColor(_baseImageColor, targetColor);

            if (instant || !isActiveAndEnabled || _fadeDuration <= 0f)
            {
                _imageTarget.color = tintedColor;
                return;
            }

            _imageTarget.CrossFadeColor(tintedColor, _fadeDuration, true, true);
        }

        private static Color MultiplyColor(Color baseColor, Color tint)
        {
            return new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a * tint.a);
        }
    }
}
