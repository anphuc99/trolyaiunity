using System.Collections.Generic;
using System;
using Features.GamePlay.SubFeatures.Journal.Model;
using Share.Components;
using UnityEngine;
namespace Features.GamePlay.SubFeatures.Journal.View
{
    /// <summary>
    /// Presenter for journal chat history using virtualized chat container.
    /// </summary>
    public sealed class JournalHistoryChat : MonoBehaviour
    {
        [SerializeField]
        private VirtualizedChatMessageContainer messageContainer;

        /// <summary>
        /// Invoked when the back button is pressed.
        /// </summary>
        public Action callback;

        /// <summary>
        /// Fired when the speaker button is clicked.
        /// </summary>
        public Action<MessageBubbleData> OnSpeakerClicked { get; set; }

        /// <summary>
        /// Fired when the speaker button is long pressed.
        /// </summary>
        public Action<MessageBubbleData> OnSpeakerLongPressed { get; set; }

        private void OnEnable()
        {
            BindMessageHandlers();
        }

        /// <summary>
        /// Replaces the current chat history messages.
        /// </summary>
        /// <param name="historys">Chat history list.</param>
        public void SetChatHistory(List<MessageBubbleData> historys)
        {
            if (messageContainer == null)
            {
                return;
            }

            messageContainer.SetMessages(historys);
            BindMessageHandlers();
        }

        /// <summary>
        /// Sets TTS reloading state for a specific message index.
        /// </summary>
        /// <param name="messageIndex">Target message index.</param>
        /// <param name="isReloading">Reloading state.</param>
        public void SetMessageTtsReloading(int messageIndex, bool isReloading)
        {
            if (messageContainer == null)
            {
                return;
            }

            messageContainer.SetMessageTtsReloading(messageIndex, isReloading);
        }

        /// <summary>
        /// Scrolls chat viewport to ensure the target message is visible.
        /// </summary>
        /// <param name="messageIndex">Target message index.</param>
        public void ScrollToMessage(int messageIndex)
        {
            if (messageContainer == null)
            {
                return;
            }

            messageContainer.ScrollToMessage(messageIndex);
        }

        /// <summary>
        /// Clears all pooled objects and invokes the back callback.
        /// </summary>
        public void Callback()
        {
            if (messageContainer != null)
            {
                messageContainer.ClearAllDataAndPools();
            }

            callback?.Invoke();
        }

        private void BindMessageHandlers()
        {
            if (messageContainer == null)
            {
                return;
            }

            messageContainer.UsePinyinRubyOnTranslate = false;
            messageContainer.OnMessageSpeakerClicked = HandleSpeakerClicked;
            messageContainer.OnMessageSpeakerLongPressed = HandleSpeakerLongPressed;
            messageContainer.OnMessageTranslateClicked = HandleTranslateClicked;
        }

        private void HandleSpeakerClicked(MessageBubbleData messageData)
        {
            OnSpeakerClicked?.Invoke(messageData);
        }

        private void HandleSpeakerLongPressed(MessageBubbleData messageData)
        {
            OnSpeakerLongPressed?.Invoke(messageData);
        }

        private void HandleTranslateClicked(MessageBubbleData messageData)
        {
            if (messageContainer == null || messageData == null)
            {
                return;
            }

            if (messageData.MessageIndex < 0 && string.IsNullOrWhiteSpace(messageData.Translation))
            {
                return;
            }

            messageContainer.ToggleMessageTranslation(messageData);
        }
    }
}