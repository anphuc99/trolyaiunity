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
        private Button _closeButton;

        private readonly List<ChatLearningPathItemView> _spawnedItems = new List<ChatLearningPathItemView>();

        public Action<ChatLearningPathPayload> OnLearningPathSelected;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
                _closeButton.onClick.AddListener(Hide);
            }
        }

        private void OnDestroy()
        {
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

        private void HandleItemSelected(ChatLearningPathPayload payload)
        {
            OnLearningPathSelected?.Invoke(payload);
            Hide();
        }

        private void ClearSpawnedItems()
        {
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