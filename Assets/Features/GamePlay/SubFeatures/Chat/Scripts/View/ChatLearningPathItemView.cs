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

        private Action<ChatLearningPathPayload> _onSelected;
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
        public void Bind(ChatLearningPathPayload payload, Action<ChatLearningPathPayload> onSelected)
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

        private void HandleSelectClicked()
        {
            if (_selectedToggle != null)
            {
                _selectedToggle.isOn = true;
            }

            _onSelected?.Invoke(_payload);
        }
    }
}