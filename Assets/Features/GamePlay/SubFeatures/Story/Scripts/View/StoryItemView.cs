using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Features.GamePlay.SubFeatures.Story.View
{
    /// <summary>
    /// View for one story item entry.
    /// </summary>
    public class StoryItemView : MonoBehaviour 
    {
        [SerializeField]
        private TextMeshProUGUI _nameStoryText;
        [SerializeField]
        private TextMeshProUGUI _contentStoryText;
        [SerializeField]
        private Button _editButton;
        
        public int StoryId { get; private set; }

        /// <summary>
        /// Raised when edit is requested for this story.
        /// </summary>
        public event Action<StoryItemView> EditRequested;

        private void Awake()
        {
            if (_editButton != null)
            {
                _editButton.onClick.RemoveAllListeners();
                _editButton.onClick.AddListener(HandleEditClicked);
            }
        }

        private void OnDestroy()
        {
            if (_editButton != null)
            {
                _editButton.onClick.RemoveListener(HandleEditClicked);
            }
        }

        /// <summary>
        /// Binds story data to this item view.
        /// </summary>
        /// <param name="storyId">Story id.</param>
        /// <param name="name">Story name.</param>
        /// <param name="description">Story description.</param>
        /// <param name="currentProgress">Story current progress.</param>
        public void Bind(int storyId, string name, string description, string currentProgress)
        {
            StoryId = storyId;

            if (_nameStoryText != null)
            {
                _nameStoryText.text = name ?? string.Empty;
            }

            if (_contentStoryText != null)
            {
                var desc = description ?? string.Empty;
                var progress = currentProgress ?? string.Empty;
                _contentStoryText.text = desc + "\n----------------\n" + progress;
            }
        }

        private void HandleEditClicked()
        {
            EditRequested?.Invoke(this);
        }
    }    
}