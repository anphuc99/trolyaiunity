using System.Collections.Generic;
using UnityEngine;
using Features.GamePlay.SubFeatures.Journal.Model;
using Share.Components;
using System;
namespace Features.GamePlay.SubFeatures.Journal.View
{
    public class JournalHistoryChat : MonoBehaviour
    {
        [SerializeField]
        private VirtualizedChatMessageContainer messageContainer;

        public Action callback;

        public void SetChatHistory(List<MessageBubbleData> historys)
        {
            messageContainer.SetMessages(historys);
        }

        public void Callback()
        {
            messageContainer.ClearAllDataAndPools();
            callback?.Invoke();
        }
    }
}