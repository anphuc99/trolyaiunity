using System;
using UnityEngine;

namespace Share.Components
{
    /// <summary>
    /// A panel containing multiple <see cref="VisualMobileKeyboardButton"/>s.
    /// Provides bulk installation of display/input text and click callbacks.
    /// </summary>
    public class VisualMobileKeyboardPanel : MonoBehaviour
    {
        [SerializeField] private VisualMobileKeyboardButton[] buttons;

        /// <summary>All buttons on this panel.</summary>
        public VisualMobileKeyboardButton[] Buttons => buttons;

        void OnValidate()
        {
            buttons = GetComponentsInChildren<VisualMobileKeyboardButton>();
        }

        /// <summary>
        /// Installs display/input text on all Letter-type buttons using parallel arrays,
        /// and wires every button's click to <paramref name="onClick"/>.
        /// </summary>
        /// <param name="displayTexts">Display texts for Letter buttons (in order).</param>
        /// <param name="inputTexts">Input texts for Letter buttons (in order).</param>
        /// <param name="onClick">Click handler for every button on the panel.</param>
        public void Install(string[] displayTexts, string[] inputTexts, Action<VisualMobileKeyboardButton> onClick)
        {
            var letterIndex = 0;
            for (var i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn.type == VisualMobileKeyType.Letter)
                {
                    if (letterIndex < displayTexts.Length && letterIndex < inputTexts.Length)
                    {
                        btn.Install(displayTexts[letterIndex], inputTexts[letterIndex], onClick);
                    }
                    letterIndex++;
                }
                else
                {
                    // Non-letter buttons keep their existing display, wire click only
                    btn.Install(btn.DisplayText, btn.InputText, onClick);
                }
            }
        }
    }
}