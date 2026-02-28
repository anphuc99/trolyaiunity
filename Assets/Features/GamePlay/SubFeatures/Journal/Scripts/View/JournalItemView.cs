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
        /// Journal id associated with this item.
        /// </summary>
        public int JournalId { get; private set; }

        /// <summary>
        /// True when the selection toggle is on.
        /// </summary>
        public bool IsSelected => _toggleSelect != null && _toggleSelect.isOn;

        [SerializeField]
        private Toggle _toggleSelect;   

        /// <summary>
        /// Binds journal metadata and selection state to this item.
        /// </summary>
        /// <param name="journalId">Journal id.</param>
        /// <param name="isSelected">Initial selection state.</param>
        public void Bind(int journalId, bool isSelected)
        {
            JournalId = journalId;
            if (_toggleSelect != null)
            {
                _toggleSelect.SetIsOnWithoutNotify(isSelected);
            }
        }
    }    
}