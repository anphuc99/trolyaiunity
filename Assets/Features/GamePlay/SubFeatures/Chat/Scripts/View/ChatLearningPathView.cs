using System;
using System.Collections.Generic;
using Features.GamePlay.SubFeatures.Chat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Chat.View
{
    /// <summary>
    /// Popup view for selecting a learning path and applying it to chat context.
    /// </summary>
    public sealed class ChatLearningPathView : MonoBehaviour
    {
        [SerializeField]
        private ChatLearningPathItemView _learningPathItemPrefab;

        [SerializeField]
        private Transform _learningPathListContainer;

        [SerializeField]
        private Button _submitButton;

        [SerializeField]
        private Button _closeButton;

        private readonly List<ChatLearningPathItemView> _spawnedItems = new List<ChatLearningPathItemView>();
        private ChatLearningPathPayload _selectedLearningPath;
        private ChatLearningPathItemView _selectedItemView;

        public Action<ChatLearningPathPayload> OnLearningPathSelected;

        private void Awake()
        {
            if (_submitButton != null)
            {
                _submitButton.onClick.RemoveListener(HandleSubmitClicked);
                _submitButton.onClick.AddListener(HandleSubmitClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
                _closeButton.onClick.AddListener(Hide);
            }
        }

        private void OnDestroy()
        {
            if (_submitButton != null)
            {
                _submitButton.onClick.RemoveListener(HandleSubmitClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
            }
        }

        /// <summary>
        /// Shows popup and renders learning path list.
        /// </summary>
        /// <param name="items">Learning path items.</param>
        public void Show(List<ChatLearningPathPayload> items)
        {
            _selectedLearningPath = null;
            RenderItems(items);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Hides popup.
        /// </summary>
        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Hides popup immediately.
        /// </summary>
        public void HideImmediate()
        {
            Hide();
        }

        private void RenderItems(List<ChatLearningPathPayload> items)
        {
            ClearSpawnedItems();

            if (_learningPathItemPrefab == null || _learningPathListContainer == null || items == null || items.Count == 0)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                var instance = Instantiate(_learningPathItemPrefab, _learningPathListContainer);
                instance.gameObject.SetActive(true);
                instance.Bind(item, HandleItemSelected);
                _spawnedItems.Add(instance);
            }
        }

        private void HandleItemSelected(ChatLearningPathItemView itemView, ChatLearningPathPayload payload)
        {
            _selectedLearningPath = payload;
            _selectedItemView = itemView;

            for (var i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] == null)
                {
                    continue;
                }

                var isSelected = ReferenceEquals(_spawnedItems[i], _selectedItemView);
                _spawnedItems[i].SetSelected(isSelected);
            }
        }

        private void HandleSubmitClicked()
        {
            if (_selectedLearningPath == null)
            {
                return;
            }

            OnLearningPathSelected?.Invoke(_selectedLearningPath);
            Hide();
        }

        private void ClearSpawnedItems()
        {
            _selectedLearningPath = null;
            _selectedItemView = null;

            for (var i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                {
                    Destroy(_spawnedItems[i].gameObject);
                }
            }

            _spawnedItems.Clear();
        }
    }
}