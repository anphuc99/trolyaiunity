using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Features.GamePlay.SubFeatures.Journal.View
{
    /// <summary>
    /// View component for a single journal list item.
    /// </summary>
    public class JournalItemView : MonoBehaviour 
    {
        /// <summary>
        /// Raised when the detail button is clicked.
        /// </summary>
        public event Action<JournalItemView> Clicked;

        /// <summary>
        /// Raised when the selection toggle changes.
        /// </summary>
        public event Action<JournalItemView, bool> SelectionChanged;

        /// <summary>
        /// Journal id associated with this item.
        /// </summary>
        public int JournalId { get; private set; }

        /// <summary>
        /// True when the selection toggle is on.
        /// </summary>
        public bool IsSelected => _toggleSelect != null && _toggleSelect.isOn;

        [SerializeField]
        private TMP_Text _summaryText;

        [SerializeField]
        private Button _detailButton;

        [SerializeField]
        private Toggle _toggleSelect;   

        private void Awake()
        {
            if (_detailButton != null)
            {
                _detailButton.onClick.RemoveAllListeners();
                _detailButton.onClick.AddListener(HandleClicked);
            }

            if (_toggleSelect != null)
            {
                _toggleSelect.onValueChanged.RemoveAllListeners();
                _toggleSelect.onValueChanged.AddListener(HandleSelectionChanged);
            }
        }

        /// <summary>
        /// Binds journal metadata and selection state to this item.
        /// </summary>
        /// <param name="journalId">Journal id.</param>
        /// <param name="label">Summary label text.</param>
        /// <param name="isSelected">Initial selection state.</param>
        public void Bind(int journalId, string label, bool isSelected)
        {
            JournalId = journalId;
            if (_summaryText != null)
            {
                _summaryText.text = label ?? string.Empty;
            }

            if (_toggleSelect != null)
            {
                _toggleSelect.SetIsOnWithoutNotify(isSelected);
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke(this);
        }

        private void HandleSelectionChanged(bool isOn)
        {
            SelectionChanged?.Invoke(this, isOn);
        }
    }    
}