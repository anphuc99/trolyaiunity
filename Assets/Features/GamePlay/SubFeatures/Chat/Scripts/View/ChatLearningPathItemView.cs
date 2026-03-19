using System;
using Features.GamePlay.SubFeatures.Chat.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Chat.View
{
    /// <summary>
    /// Renders one learning-path item inside chat popup.
    /// </summary>
    public sealed class ChatLearningPathItemView : MonoBehaviour
    {
        [SerializeField]
        private Button _selectButton;

        [SerializeField]
        private Toggle _selectedToggle;

        [SerializeField]
        private TMP_Text _contentText;

        private Action<ChatLearningPathItemView, ChatLearningPathPayload> _onSelected;
        private ChatLearningPathPayload _payload;

        private void Awake()
        {
            if (_selectButton != null)
            {
                _selectButton.onClick.RemoveListener(HandleSelectClicked);
                _selectButton.onClick.AddListener(HandleSelectClicked);
            }
        }

        private void OnDestroy()
        {
            if (_selectButton != null)
            {
                _selectButton.onClick.RemoveListener(HandleSelectClicked);
            }
        }

        /// <summary>
        /// Binds one learning path item.
        /// </summary>
        /// <param name="payload">Learning path payload.</param>
        /// <param name="onSelected">Selection callback.</param>
        public void Bind(ChatLearningPathPayload payload, Action<ChatLearningPathItemView, ChatLearningPathPayload> onSelected)
        {
            _payload = payload;
            _onSelected = onSelected;

            if (_contentText != null)
            {
                var context = payload?.Context ?? string.Empty;
                var vocabulary = payload?.Vocabulary ?? string.Empty;
                _contentText.text = context + "\n---------------\n" + vocabulary;
            }

            if (_selectedToggle != null)
            {
                _selectedToggle.SetIsOnWithoutNotify(false);
            }
        }

        /// <summary>
        /// Updates selected state without triggering toggle callbacks.
        /// </summary>
        /// <param name="isSelected">Whether this item is selected.</param>
        public void SetSelected(bool isSelected)
        {
            if (_selectedToggle == null)
            {
                return;
            }

            _selectedToggle.SetIsOnWithoutNotify(isSelected);
        }

        private void HandleSelectClicked()
        {
            _onSelected?.Invoke(this, _payload);
        }
    }
}