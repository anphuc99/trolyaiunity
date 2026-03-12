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
    public class VisualMobileKeyboardButton : MonoBehaviour, IPointerClickHandler
    {
        public VisualMobileKeyType type;
        public string DisplayText;
        public string InputText;
        [SerializeField] private TextMeshProUGUI textMeshProUGUI;
        [SerializeField] private Button _button;

        private Action<VisualMobileKeyboardButton> _onClick;

        void Awake()
        {
            // Disable the Button component so it is no longer a Selectable.
            // This prevents the EventSystem from selecting the button on click,
            // which would deselect the input field and hide the keyboard.
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
            if (_button != null)
            {
                _button.enabled = false;
            }
        }

        /// <summary>
        /// Handles pointer click via IPointerClickHandler (no selection change).
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke(this);
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
    }
}
