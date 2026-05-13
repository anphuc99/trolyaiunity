using System;
using System.Collections.Generic;
using UnityEngine;

public class ChatMissionView : MonoBehaviour
{
    [SerializeField] private ChatItemMissionView chatItemMissionViewPrefab;
    [SerializeField] private Transform chatContent;

    private readonly List<ChatItemMissionView> _spawnedItems = new List<ChatItemMissionView>();

    /// <summary>
    /// Clears all existing mission items and populates with new vocabulary entries.
    /// </summary>
    /// <param name="entries">List of (hanzi, pinyin, vietnamese) tuples.</param>
    public void SetMissionItems(List<MissionVocabEntry> entries)
    {
        ClearAll();

        if (entries == null || entries.Count == 0)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Hanzi))
            {
                continue;
            }

            AddChatItem(entry.Hanzi, entry.Pinyin, entry.Vietnamese);
        }
    }

    /// <summary>
    /// Adds a single mission item.
    /// </summary>
    public void AddChatItem(string hanzi, string pinyin, string vietnamese)
    {
        if (chatItemMissionViewPrefab == null || chatContent == null)
        {
            return;
        }

        var chatItem = Instantiate(chatItemMissionViewPrefab, chatContent);
        chatItem.SetText(hanzi, pinyin, vietnamese);
        _spawnedItems.Add(chatItem);
    }

    /// <summary>
    /// Checks user text against all uncompleted mission words.
    /// Returns true if at least one word was newly completed.
    /// Completed items get strikethrough and are moved to the bottom.
    /// </summary>
    /// <param name="userText">User message text to check.</param>
    /// <returns>List of newly completed hanzi words.</returns>
    public List<string> CheckAndMarkUsedWords(string userText)
    {
        var newlyCompleted = new List<string>();
        if (string.IsNullOrWhiteSpace(userText) || _spawnedItems.Count == 0)
        {
            return newlyCompleted;
        }

        for (var i = 0; i < _spawnedItems.Count; i++)
        {
            var item = _spawnedItems[i];
            if (item == null || item.IsCompleted)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Hanzi))
            {
                continue;
            }

            if (userText.IndexOf(item.Hanzi, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                item.MarkCompleted();
                newlyCompleted.Add(item.Hanzi);
            }
        }

        if (newlyCompleted.Count > 0)
        {
            ReorderCompletedToBottom();
        }

        return newlyCompleted;
    }

    /// <summary>
    /// Clears all spawned mission items.
    /// </summary>
    public void ClearAll()
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

    /// <summary>
    /// Reorders items so completed ones are at the bottom, preserving relative order.
    /// </summary>
    private void ReorderCompletedToBottom()
    {
        if (chatContent == null || _spawnedItems.Count <= 1)
        {
            return;
        }

        // Stable partition: uncompleted first, completed last.
        _spawnedItems.Sort((a, b) =>
        {
            if (a.IsCompleted == b.IsCompleted) return 0;
            return a.IsCompleted ? 1 : -1;
        });

        for (var i = 0; i < _spawnedItems.Count; i++)
        {
            if (_spawnedItems[i] != null)
            {
                _spawnedItems[i].transform.SetSiblingIndex(i);
            }
        }
    }
}

/// <summary>
/// Simple data class for mission vocabulary entries.
/// </summary>
public class MissionVocabEntry
{
    public string Hanzi { get; set; }
    public string Pinyin { get; set; }
    public string Vietnamese { get; set; }
}