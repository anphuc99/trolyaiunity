using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.GamePlay.View
{
    public class GamePlayMenuButton : MonoBehaviour
    {
        public GamePlayMenuItem MenuItemPrefab;

        private readonly Dictionary<string, GamePlayMenuItem> _menuItemsById = new Dictionary<string, GamePlayMenuItem>(StringComparer.Ordinal);

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

            item.OnClick = onClick;
            item.id = id;
            _menuItemsById[id] = item;
            return item.id;
        }

        public void RemoveMenuItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!_menuItemsById.TryGetValue(id, out var item))
            {
                return;
            }

            _menuItemsById.Remove(id);
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
    }
}
