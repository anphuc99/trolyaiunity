using System.Collections.Generic;
using System;
using Share.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Share.Components
{
	/// <summary>
	/// Virtualized message container that supports drag and mouse-wheel scrolling without using Unity ScrollView.
	/// Only a small pool of message items is rendered and recycled while scrolling.
	/// </summary>
	public sealed class VirtualizedChatMessageContainer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
	{
		public Action<MessageBubbleData> OnMessageSpeakerClicked { get; set; }
		public Action<MessageBubbleData> OnMessageSpeakerLongPressed { get; set; }
		public Action<MessageBubbleData> OnMessageTranslateClicked { get; set; }
		public Action<string> OnVocabWordClicked { get; set; }

		private const string TranslationSeparator = "---------------------";
		private const string PinyinLabel = "Pinyin: ";
		private const int RubyWrapHanCountPerLine = 7;
		private const int TranslationTextSize = 30;

		[SerializeField]
		private RectTransform _viewport;

		[SerializeField]
		private RectTransform _itemsRoot;

		[SerializeField]
		private MessageBubble _characterMessagePrefab;

		[SerializeField]
		private MessageBubble _userMessagePrefab;

		[SerializeField]
		[Min(0)]
		private int _extraBufferedItems = 2;

		[SerializeField]
		[Min(0.01f)]
		private float _dragSensitivity = 1f;

		[SerializeField]
		[Min(1f)]
		private float _mouseWheelStep = 48f;

		[SerializeField]
		[Min(0f)]
		private float _contentPaddingTop = 0f;

		[SerializeField]
		[Min(0f)]
		private float _contentPaddingBottom = 0f;

		[SerializeField]
		[Min(1)]
		private int _maxSpawnedObjects = 24;

		[SerializeField]
		private List<MessageBubbleData> _messages = new();

		[SerializeField]
		private bool _usePinyinRubyOnTranslate = true;

		/// <summary>
		/// Enables ruby-style pinyin rendering when translation is toggled.
		/// </summary>
		public bool UsePinyinRubyOnTranslate
		{
			get => _usePinyinRubyOnTranslate;
			set => _usePinyinRubyOnTranslate = value;
		}

		private readonly List<MessageBubble> _characterPool = new();
		private readonly List<MessageBubble> _userPool = new();
		private readonly List<float> _messageHeights = new();
		private readonly List<float> _prefixHeights = new();

		private MessageBubble _characterMeasureBubble;
		private MessageBubble _userMeasureBubble;
		private float _scrollOffset;
		private bool _isDragging;
		private float _totalHeight;
		private bool _metricsStale;

		/// <summary>
		/// Returns current total message count.
		/// </summary>
		public int MessageCount => _messages.Count;

		/// <summary>
		/// Maximum pooled object count that can be spawned per bubble type.
		/// </summary>
		public int MaxSpawnedObjects => _maxSpawnedObjects;

		private void Awake()
		{
			EnsureReferences();
			EnsureMeasureBubbles();
			RebuildMetrics();
			RefreshVisible();
		}

		private void OnEnable()
		{
			if (_metricsStale)
			{
				_metricsStale = false;
				RebuildMetrics();
			}

			RefreshVisible();
		}

		/// <summary>
		/// Replaces all messages in container.
		/// </summary>
		/// <param name="messages">New message collection.</param>
		public void SetMessages(IReadOnlyList<MessageBubbleData> messages)
		{
			_messages.Clear();
			if (messages != null)
			{
				for (var i = 0; i < messages.Count; i++)
				{
					var mapped = messages[i] ?? new MessageBubbleData();
					ApplyDefaultMessageDisplay(mapped);
					_messages.Add(mapped);
				}
			}

			_scrollOffset = 0f;
			SyncMessageIndices();
			EnsureMeasureBubbles();
			RebuildMetrics();
			RefreshVisible();
		}

		/// <summary>
		/// Appends one message at the end (newer message).
		/// </summary>
		/// <param name="message">Message text.</param>
		public void AddNewMessage(MessageBubbleData message)
		{
			var mapped = message ?? new MessageBubbleData();
			ApplyDefaultMessageDisplay(mapped);
			_messages.Add(mapped);
			SyncMessageIndices();
			RebuildMetrics();
			_scrollOffset = GetMaxScrollOffset();
			RefreshVisible();
		}

		/// <summary>
		/// Clears all messages and destroys pooled objects to release memory.
		/// </summary>
		public void ClearAllDataAndPools()
		{
			_messages.Clear();
			_messageHeights.Clear();
			_prefixHeights.Clear();
			_scrollOffset = 0f;
			_totalHeight = 0f;

			DestroyBubbles(_characterPool);
			DestroyBubbles(_userPool);
			_characterPool.Clear();
			_userPool.Clear();

			DestroyBubble(_characterMeasureBubble);
			DestroyBubble(_userMeasureBubble);
			_characterMeasureBubble = null;
			_userMeasureBubble = null;

			RefreshVisible();
		}

		/// <summary>
		/// Updates avatar sprite for all character messages with matching sender name.
		/// </summary>
		/// <param name="characterName">Character display name.</param>
		/// <param name="avatar">Avatar sprite.</param>
		public void UpdateCharacterAvatar(string characterName, Sprite avatar)
		{
			if (string.IsNullOrWhiteSpace(characterName) || avatar == null)
			{
				return;
			}

			var updated = false;
			for (var i = 0; i < _messages.Count; i++)
			{
				var message = _messages[i];
				if (message == null || message.Type != MessageBubbleType.Character)
				{
					continue;
				}

				if (!string.Equals(message.SenderName, characterName, System.StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (message.Avatar == avatar)
				{
					continue;
				}

				message.Avatar = avatar;
				updated = true;
			}

			if (updated)
			{
				RefreshVisible();
			}
		}

		/// <summary>
		/// Toggles translation display for a specific message instance.
		/// </summary>
		/// <param name="messageData">Message instance from bubble callback.</param>
		public void ToggleMessageTranslation(MessageBubbleData messageData)
		{
			if (messageData == null)
			{
				return;
			}

			ToggleMessageTranslationInternal(messageData, messageData.MessageIndex, messageData.Translation);
		}

		private void ToggleMessageTranslationInternal(MessageBubbleData targetMessage, int messageIndex, string translation)
		{
			if (targetMessage == null && messageIndex < 0)
			{
				return;
			}

			var previousMaxScrollOffset = GetMaxScrollOffset();
			var wasPinnedToBottom = Mathf.Abs(_scrollOffset - previousMaxScrollOffset) <= 2f;

			var updated = false;
			for (var i = 0; i < _messages.Count; i++)
			{
				var message = _messages[i];
				if (message == null)
				{
					continue;
				}

				if (!ReferenceEquals(message, targetMessage) && message.MessageIndex != messageIndex)
				{
					continue;
				}

				var originalText = string.IsNullOrWhiteSpace(message.OriginalMessage)
					? (message.Message ?? string.Empty)
					: message.OriginalMessage;
				var displayBaseText = BuildDisplayBaseText(message, originalText);
				var resolvedTranslation = string.IsNullOrWhiteSpace(translation) ? message.Translation : translation;
				var translationDisplayText = BuildTranslationDisplayText(resolvedTranslation);

				if (_usePinyinRubyOnTranslate)
				{
					if (string.IsNullOrWhiteSpace(resolvedTranslation))
					{
						break;
					}

					if (message.IsTranslationExpanded)
					{
						message.Message = BuildDefaultMessageText(message, displayBaseText);
						message.IsTranslationExpanded = false;
					}
					else
					{
						message.OriginalMessage = originalText;
						message.Translation = resolvedTranslation;
						var rubyText = BuildDefaultMessageText(message, displayBaseText);
						message.Message = rubyText + "\n" + TranslationSeparator + "\n" + translationDisplayText;
						message.IsTranslationExpanded = true;
					}

					updated = true;
					break;
				}

				if (string.IsNullOrWhiteSpace(resolvedTranslation))
				{
					break;
				}

				var pinyinLine = BuildPinyinLine(message.Pinyin);

				if (message.IsTranslationExpanded)
				{
					message.Message = displayBaseText;
					message.IsTranslationExpanded = false;
				}
				else
				{
					message.OriginalMessage = originalText;
					message.Translation = resolvedTranslation;
					if (string.IsNullOrWhiteSpace(pinyinLine))
					{
						message.Message = displayBaseText + "\n" + TranslationSeparator + "\n" + translationDisplayText;
					}
					else
					{
						message.Message = displayBaseText + "\n" + pinyinLine + "\n" + TranslationSeparator + "\n" + translationDisplayText;
					}
					message.IsTranslationExpanded = true;
				}

				updated = true;
				break;
			}

			if (updated)
			{
				RebuildMetrics();
				if (wasPinnedToBottom)
				{
					_scrollOffset = GetMaxScrollOffset();
				}
				RefreshVisible();
			}
		}

		private static string BuildDisplayBaseText(MessageBubbleData message, string originalText)
		{
			return originalText ?? string.Empty;
		}

		private static string BuildPinyinLine(string pinyin)
		{
			if (string.IsNullOrWhiteSpace(pinyin))
			{
				return string.Empty;
			}

			return PinyinLabel + pinyin.Trim();
		}

		private static string BuildTranslationDisplayText(string translation)
		{
			if (string.IsNullOrWhiteSpace(translation))
			{
				return string.Empty;
			}

			return "<size=" + TranslationTextSize + ">" + translation.Trim() + "</size>";
		}

		private void ApplyDefaultMessageDisplay(MessageBubbleData message)
		{
			if (message == null || message.IsTranslationExpanded)
			{
				return;
			}

			var originalText = string.IsNullOrWhiteSpace(message.OriginalMessage)
				? (message.Message ?? string.Empty)
				: message.OriginalMessage;
			message.OriginalMessage = originalText;
			message.Message = BuildDefaultMessageText(message, originalText);
		}

		private string BuildDefaultMessageText(MessageBubbleData message, string originalText)
		{
			if (!_usePinyinRubyOnTranslate)
			{
				return originalText ?? string.Empty;
			}

			if (message == null || string.IsNullOrWhiteSpace(message.Pinyin))
			{
				return originalText ?? string.Empty;
			}

			var textForRuby = string.IsNullOrWhiteSpace(message.RawVocabText)
				? (originalText ?? string.Empty)
				: message.RawVocabText;
			return PinyinRichTextUtils.BuildWrappedInlineRuby(
				textForRuby,
				message.Pinyin,
				RubyWrapHanCountPerLine);
		}

		/// <summary>
		/// Sets TTS reloading state for a message and refreshes visible bubbles.
		/// </summary>
		/// <param name="messageIndex">Target message index.</param>
		/// <param name="isReloading">Reloading state.</param>
		public void SetMessageTtsReloading(int messageIndex, bool isReloading)
		{
			if (messageIndex < 0)
			{
				return;
			}

			var updated = false;
			for (var i = 0; i < _messages.Count; i++)
			{
				var message = _messages[i];
				if (message == null)
				{
					continue;
				}

				if (message.MessageIndex != messageIndex)
				{
					continue;
				}

				if (message.IsTtsReloading == isReloading)
				{
					break;
				}

				message.IsTtsReloading = isReloading;
				updated = true;
				break;
			}

			if (updated)
			{
				RefreshVisible();
			}
		}

		/// <summary>
		/// Sets TTS playing state for a message matched by message id and refreshes visible bubbles.
		/// </summary>
		/// <param name="messageId">Target message id.</param>
		/// <param name="isPlaying">Playing state.</param>
		public void SetMessageTtsPlaying(string messageId, bool isPlaying)
		{
			if (string.IsNullOrWhiteSpace(messageId))
			{
				return;
			}

			var updated = false;
			for (var i = 0; i < _messages.Count; i++)
			{
				var message = _messages[i];
				if (message == null)
				{
					continue;
				}

				if (message.MessageId != messageId)
				{
					continue;
				}

				if (message.IsTtsPlaying == isPlaying)
				{
					break;
				}

				message.IsTtsPlaying = isPlaying;
				updated = true;
				break;
			}

			if (updated)
			{
				RefreshVisible();
			}
		}

		/// <summary>
		/// Updates the text of a message identified by its message id.
		/// </summary>
		/// <param name="messageId">Message identifier.</param>
		/// <param name="newText">New text to display.</param>
		public void UpdateMessageText(string messageId, string newText)
		{
			if (string.IsNullOrWhiteSpace(messageId))
			{
				return;
			}

			var updated = false;
			for (var i = 0; i < _messages.Count; i++)
			{
				var message = _messages[i];
				if (message == null)
				{
					continue;
				}

				if (message.MessageId != messageId)
				{
					continue;
				}

				message.Message = newText ?? string.Empty;
				message.OriginalMessage = newText ?? string.Empty;
				message.IsTranslationExpanded = false;
				ApplyDefaultMessageDisplay(message);
				updated = true;
				break;
			}

			if (updated)
			{
				RebuildMetrics();
				RefreshVisible();
			}
		}

		/// <summary>
		/// Inserts one message at the beginning (older message).
		/// </summary>
		/// <param name="message">Message text.</param>
		public void AddOldMessage(MessageBubbleData message)
		{
			var mapped = message ?? new MessageBubbleData();
			ApplyDefaultMessageDisplay(mapped);
			_messages.Insert(0, mapped);
			SyncMessageIndices();
			RebuildMetrics();
			if (_messageHeights.Count > 0)
			{
				_scrollOffset += _messageHeights[0];
			}

			RefreshVisible();
		}

		/// <summary>
		/// Handles drag start state.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnBeginDrag(PointerEventData eventData)
		{
			_isDragging = true;
		}

		/// <summary>
		/// Handles drag scrolling.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			ScrollBy(eventData.delta.y * _dragSensitivity);
		}

		/// <summary>
		/// Handles drag end state.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnEndDrag(PointerEventData eventData)
		{
			_isDragging = false;
		}

		/// <summary>
		/// Handles mouse wheel scrolling.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnScroll(PointerEventData eventData)
		{
			ScrollBy(-eventData.scrollDelta.y * _mouseWheelStep);
		}

		/// <summary>
		/// Scrolls by pixel delta and refreshes visible messages.
		/// </summary>
		/// <param name="deltaPixels">Positive value scrolls down; negative value scrolls up.</param>
		public void ScrollBy(float deltaPixels)
		{
			_scrollOffset = Mathf.Clamp(_scrollOffset + deltaPixels, 0f, GetMaxScrollOffset());
			RefreshVisible();
		}

		/// <summary>
		/// Scrolls directly to the newest message at the bottom.
		/// </summary>
		public void ScrollToBottom()
		{
			_scrollOffset = GetMaxScrollOffset();
			RefreshVisible();
		}

		/// <summary>
		/// Scrolls viewport to make a specific message index visible.
		/// </summary>
		/// <param name="messageIndex">Target message index.</param>
		public void ScrollToMessage(int messageIndex)
		{
			if (_messages.Count == 0 || messageIndex < 0 || messageIndex >= _messages.Count)
			{
				return;
			}

			if (_prefixHeights.Count <= messageIndex + 1)
			{
				RebuildMetrics();
			}

			if (_viewport == null || _prefixHeights.Count <= messageIndex + 1)
			{
				return;
			}

			var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
			var itemTop = _prefixHeights[messageIndex];
			var itemBottom = _prefixHeights[messageIndex + 1];

			if (itemTop < _scrollOffset)
			{
				_scrollOffset = itemTop;
			}
			else if (itemBottom > _scrollOffset + viewportHeight)
			{
				_scrollOffset = itemBottom - viewportHeight;
			}

			_scrollOffset = Mathf.Clamp(_scrollOffset, 0f, GetMaxScrollOffset());
			RefreshVisible();
		}

		private void EnsureReferences()
		{
			if (_viewport == null)
			{
				_viewport = transform as RectTransform;
			}

			if (_itemsRoot == null)
			{
				_itemsRoot = _viewport;
			}
		}

		private void EnsurePoolCapacity(int requiredCount)
		{
			if (_viewport == null || _itemsRoot == null || _characterMessagePrefab == null || _userMessagePrefab == null)
			{
				return;
			}

			var safeRequiredCount = Mathf.Clamp(requiredCount, 0, Mathf.Max(1, _maxSpawnedObjects));
			while (_characterPool.Count < safeRequiredCount)
			{
				_characterPool.Add(CreatePooledBubble(_characterMessagePrefab, "Character"));
				_userPool.Add(CreatePooledBubble(_userMessagePrefab, "User"));
			}
		}

		private int GetDesiredPoolCount()
		{
			if (_viewport == null)
			{
				return 0;
			}

			var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
			var visibleCount = Mathf.CeilToInt(viewportHeight / 72f);
			var desiredCount = Mathf.Max(0, visibleCount + _extraBufferedItems);
			return Mathf.Min(Mathf.Max(1, _maxSpawnedObjects), desiredCount);
		}

		private float GetMaxScrollOffset()
		{
			if (_viewport == null)
			{
				return 0f;
			}

			var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
			return Mathf.Max(0f, _totalHeight - viewportHeight);
		}

		private void RefreshVisible()
		{
			if (_viewport == null || _itemsRoot == null)
			{
				return;
			}

			if (_messages.Count == 0)
			{
				SetAllPooledInactive();
				return;
			}

			_scrollOffset = Mathf.Clamp(_scrollOffset, 0f, GetMaxScrollOffset());
			var startIndex = FindStartIndex(_scrollOffset);
			var requiredSlotCount = Mathf.Min(GetDesiredPoolCount(), Mathf.Max(0, _messages.Count - startIndex));
			EnsurePoolCapacity(requiredSlotCount);

			if (_characterPool.Count == 0 || _userPool.Count == 0)
			{
				return;
			}

			for (var slot = 0; slot < _characterPool.Count; slot++)
			{
				var dataIndex = startIndex + slot;
				var userBubble = _userPool[slot];
				var characterBubble = _characterPool[slot];

				if (dataIndex < 0 || dataIndex >= _messages.Count)
				{
					if (userBubble != null)
					{
						userBubble.gameObject.SetActive(false);
					}

					if (characterBubble != null)
					{
						characterBubble.gameObject.SetActive(false);
					}

					continue;
				}

				var data = _messages[dataIndex];
				var targetBubble = data.Type == MessageBubbleType.User ? userBubble : characterBubble;
				var otherBubble = data.Type == MessageBubbleType.User ? characterBubble : userBubble;

				if (otherBubble != null)
				{
					otherBubble.gameObject.SetActive(false);
				}

				if (targetBubble == null)
				{
					continue;
				}

				targetBubble.gameObject.SetActive(true);
				targetBubble.ApplyData(data, _viewport.rect.width);

				var bubbleRect = targetBubble.RootRect;
				if (bubbleRect == null)
				{
					continue;
				}

				var y = -(_prefixHeights[dataIndex] - _scrollOffset);
				bubbleRect.anchoredPosition = new Vector2(0f, y);
			}
		}

		private void SetAllPooledInactive()
		{
			for (var i = 0; i < _characterPool.Count; i++)
			{
				if (_characterPool[i] != null)
				{
					_characterPool[i].gameObject.SetActive(false);
				}
			}

			for (var i = 0; i < _userPool.Count; i++)
			{
				if (_userPool[i] != null)
				{
					_userPool[i].gameObject.SetActive(false);
				}
			}
		}

		private MessageBubble CreatePooledBubble(MessageBubble prefab, string suffix)
		{
			var bubble = Instantiate(prefab, _itemsRoot);
			bubble.name = prefab.name + "_Virtualized_" + suffix;
			bubble.SetSpeakerClickHandler(HandleBubbleSpeakerClicked);
			bubble.SetSpeakerLongPressHandler(HandleBubbleSpeakerLongPressed);
			bubble.SetTranslateClickHandler(HandleBubbleTranslateClicked);
			bubble.SetVocabWordClickHandler(HandleBubbleVocabWordClicked);

			bubble.gameObject.SetActive(false);
			return bubble;
		}

		private void EnsureMeasureBubbles()
		{
			if (_characterMessagePrefab == null || _userMessagePrefab == null || _itemsRoot == null)
			{
				return;
			}

			if (_characterMeasureBubble == null)
			{
				_characterMeasureBubble = Instantiate(_characterMessagePrefab, _itemsRoot);
				PrepareMeasureBubble(_characterMeasureBubble, "Character");
			}

			if (_userMeasureBubble == null)
			{
				_userMeasureBubble = Instantiate(_userMessagePrefab, _itemsRoot);
				PrepareMeasureBubble(_userMeasureBubble, "User");
			}
		}

		private void PrepareMeasureBubble(MessageBubble bubble, string suffix)
		{
			if (bubble == null)
			{
				return;
			}

			bubble.name = bubble.name + "_Measure_" + suffix;
			bubble.SetSpeakerClickHandler(null);
			bubble.SetSpeakerLongPressHandler(null);
			bubble.SetTranslateClickHandler(null);
			bubble.SetVocabWordClickHandler(null);
			bubble.gameObject.SetActive(true);
			var rect = bubble.RootRect;
			if (rect != null)
			{
				rect.anchoredPosition = new Vector2(-100000f, -100000f);
			}

			var canvasGroup = bubble.GetComponent<CanvasGroup>();
			if (canvasGroup == null)
			{
				canvasGroup = bubble.gameObject.AddComponent<CanvasGroup>();
			}

			canvasGroup.alpha = 0f;
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}

		private void HandleBubbleSpeakerClicked(MessageBubbleData messageData)
		{
			OnMessageSpeakerClicked?.Invoke(messageData);
		}

		private void HandleBubbleSpeakerLongPressed(MessageBubbleData messageData)
		{
			OnMessageSpeakerLongPressed?.Invoke(messageData);
		}

		private void HandleBubbleTranslateClicked(MessageBubbleData messageData)
		{
			OnMessageTranslateClicked?.Invoke(messageData);
		}

		private void HandleBubbleVocabWordClicked(string word)
		{
			OnVocabWordClicked?.Invoke(word);
		}

		private void DestroyBubbles(List<MessageBubble> pool)
		{
			if (pool == null)
			{
				return;
			}

			for (var i = 0; i < pool.Count; i++)
			{
				DestroyBubble(pool[i]);
			}
		}

		private void DestroyBubble(MessageBubble bubble)
		{
			if (bubble == null)
			{
				return;
			}

			Destroy(bubble.gameObject);
		}

		private void RebuildMetrics()
		{
			SyncMessageIndices();
			_messageHeights.Clear();
			_prefixHeights.Clear();
			_prefixHeights.Add(_contentPaddingTop);
			_totalHeight = _contentPaddingTop;

			if (_viewport == null)
			{
				return;
			}

			// Mark metrics stale when measured while the hierarchy is inactive;
			// OnEnable will rebuild with correct layout data.
			if (!gameObject.activeInHierarchy)
			{
				_metricsStale = true;
			}

			EnsureMeasureBubbles();

			for (var i = 0; i < _messages.Count; i++)
			{
				var itemHeight = MeasureMessageHeight(_messages[i]);
				_messageHeights.Add(itemHeight);
				_totalHeight += itemHeight;
				_prefixHeights.Add(_totalHeight);
			}

			_totalHeight += _contentPaddingBottom;
		}

		private void SyncMessageIndices()
		{
			for (var i = 0; i < _messages.Count; i++)
			{
				if (_messages[i] == null)
				{
					continue;
				}

				_messages[i].MessageIndex = i;
			}
		}

		private float MeasureMessageHeight(MessageBubbleData data)
		{
			if (data == null || _viewport == null)
			{
				return 1f;
			}

			var measureBubble = data.Type == MessageBubbleType.User ? _userMeasureBubble : _characterMeasureBubble;
			if (measureBubble == null)
			{
				return 72f;
			}

			var height = measureBubble.MeasureHeight(data, _viewport.rect.width);
			return Mathf.Max(1f, height);
		}

		private int FindStartIndex(float scroll)
		{
			if (_messages.Count == 0)
			{
				return 0;
			}

			var left = 0;
			var right = _messages.Count - 1;
			var result = 0;

			while (left <= right)
			{
				var middle = (left + right) / 2;
				var start = _prefixHeights[middle];
				var end = _prefixHeights[middle + 1];

				if (scroll < start)
				{
					right = middle - 1;
				}
				else if (scroll >= end)
				{
					left = middle + 1;
					result = Mathf.Min(_messages.Count - 1, left);
				}
				else
				{
					return middle;
				}
			}

			return Mathf.Clamp(result, 0, _messages.Count - 1);
		}
	}
}