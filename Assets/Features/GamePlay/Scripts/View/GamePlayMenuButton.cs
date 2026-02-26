using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.GamePlay.View
{
    public class GamePlayMenuButton : MonoBehaviour
    {
        public GamePlayMenuItem MenuItemPrefab;

        public GameObject MenuContainer;

        private readonly Dictionary<string, GamePlayMenuItem> _menuItemsById = new Dictionary<string, GamePlayMenuItem>(StringComparer.Ordinal);

        private void Awake()
        {
            UpdateButtonVisibility();
        }

        public string AddMenuItem(string id, string text, Action onClick)
        {
            if (MenuItemPrefab == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            RemoveMenuItem(id);

            var item = Instantiate(MenuItemPrefab, transform);
            if (item.Text != null)
            {
                item.Text.text = text ?? string.Empty;
            }

            item.OnClick = () => {
                onClick?.Invoke();
                ShowHideMenu();
            };
            item.id = id;
            _menuItemsById[id] = item;
            UpdateButtonVisibility();
            return item.id;
        }

        public void RemoveMenuItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                UpdateButtonVisibility();
                return;
            }

            if (!_menuItemsById.TryGetValue(id, out var item))
            {
                UpdateButtonVisibility();
                return;
            }

            _menuItemsById.Remove(id);
            if (item != null)
            {
                Destroy(item.gameObject);
            }

            UpdateButtonVisibility();
        }

        public void ShowHideMenu()
        {
            if (MenuContainer != null)
            {
                MenuContainer.SetActive(!MenuContainer.activeSelf);
            }
        }

        private void UpdateButtonVisibility()
        {
            var hasMenuItems = _menuItemsById.Count > 0;

            if (!hasMenuItems && MenuContainer != null && MenuContainer.activeSelf)
            {
                MenuContainer.SetActive(false);
            }

            if (gameObject.activeSelf != hasMenuItems)
            {
                gameObject.SetActive(hasMenuItems);
            }
        }
    }
}
